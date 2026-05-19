// I.3: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync em DarBaixaParcialAsync e DarBaixaTotalAsync.
// AND TenantId mantido em todos os UPDATE (defesa em profundidade S1.6).
// Todo o restante idêntico ao original.
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP.Infrastructure.Services;

public class ContaReceberService : IContaReceberService
{
    private readonly IUnitOfWork      _uow;
    private readonly IServiceProvider _serviceProvider;

    public ContaReceberService(IUnitOfWork uow, IServiceProvider serviceProvider)
    {
        _uow             = uow;
        _serviceProvider = serviceProvider;
    }

    public async Task GerarContaAPrazoAsync(Guid clienteId, Guid? vendaId, decimal valor, string descricao)
    {
        var conta = new ContaReceber
        {
            CustomerId     = clienteId,
            SaleId         = vendaId,
            ValorTotal     = valor,
            ValorRecebido  = 0,
            DataEmissao    = DateTime.Now,
            DataVencimento = DateTime.Now.AddDays(30),
            Status         = "Pendente",
            Descricao      = descricao
        };

        await _uow.ContasReceber.AddAsync(conta);
        await _uow.CommitAsync();
    }

    public async Task<IEnumerable<ContaReceber>> GetPendentesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.Status == "Pendente")
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();
    }

    public async Task<IEnumerable<ContaReceber>> GetPorClienteAsync(Guid clienteId)
    {
        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.CustomerId == clienteId)
            .OrderByDescending(c => c.DataEmissao)
            .ToListAsync();
    }

    public async Task<IEnumerable<ContaReceber>> GetInadimplentesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.Status == "Pendente" && c.DataVencimento.Date < DateTime.Today)
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();
    }

    public async Task DarBaixaParcialAsync(Guid contaId, decimal valorRecebido)
    {
        using var scope = _serviceProvider.CreateScope();
        var ctx    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<IRequestTenant>();

        var conta = await ctx.ContasReceber.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contaId)
            ?? throw new KeyNotFoundException("Conta não encontrada.");

        var novoValorRecebido = Math.Min(conta.ValorRecebido + valorRecebido, conta.ValorTotal);
        var novoStatus        = novoValorRecebido >= conta.ValorTotal ? "Pago" : "Pendente";
        var dataPagamento     = novoStatus == "Pago" ? DateTime.Now : (DateTime?)null;
        var agora             = DateTime.UtcNow;
        var tenantId          = tenant.TenantId;

        if (dataPagamento.HasValue)
        {
            // I.3: ExecuteSqlInterpolatedAsync — AND TenantId mantido (S1.6)
            var dp = dataPagamento.Value;
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ContasReceber SET ValorRecebido={novoValorRecebido}, Status={novoStatus}, DataPagamento={dp}, UpdatedAt={agora} WHERE Id={contaId} AND TenantId={tenantId}");
        }
        else
        {
            // I.3: ExecuteSqlInterpolatedAsync — AND TenantId mantido (S1.6)
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ContasReceber SET ValorRecebido={novoValorRecebido}, Status={novoStatus}, DataPagamento=NULL, UpdatedAt={agora} WHERE Id={contaId} AND TenantId={tenantId}");
        }
    }

    public async Task DarBaixaTotalAsync(Guid contaId)
    {
        using var scope = _serviceProvider.CreateScope();
        var ctx    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<IRequestTenant>();

        var conta = await ctx.ContasReceber.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contaId)
            ?? throw new KeyNotFoundException("Conta não encontrada.");

        var statusPago = "Pago";
        var agora      = DateTime.UtcNow;
        var hoje       = DateTime.Now;
        var tenantId   = tenant.TenantId;

        // I.3: ExecuteSqlInterpolatedAsync — AND TenantId mantido (S1.6)
        await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ContasReceber SET ValorRecebido={conta.ValorTotal}, Status={statusPago}, DataPagamento={hoje}, UpdatedAt={agora} WHERE Id={contaId} AND TenantId={tenantId}");
    }

    public async Task<(decimal TotalPendente, decimal TotalVencido, int QtdClientes)> GetResumoAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendentes = await ctx.ContasReceber.AsNoTracking()
            .Where(c => c.Status == "Pendente")
            .ToListAsync();

        decimal totalPendente = pendentes.Sum(c => c.ValorTotal - c.ValorRecebido);
        decimal totalVencido  = pendentes
            .Where(c => c.DataVencimento.Date < DateTime.Today)
            .Sum(c => c.ValorTotal - c.ValorRecebido);
        int qtdClientes = pendentes.Select(c => c.CustomerId).Distinct().Count();

        return (totalPendente, totalVencido, qtdClientes);
    }

    public async Task<int> CountInadimplentesAsync()
    {
        var contas = await _uow.ContasReceber.GetAllAsync();
        return contas.Count(c =>
            c.DataVencimento.Date < DateTime.Today && c.Status == "Pendente");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PARCELAMENTO
    // ═══════════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<ParcelaDto>> GerarParcelasAsync(GerarParcelasDto dto)
    {
        if (dto.NumeroParcelas < 1)
            throw new ArgumentException("Número de parcelas deve ser maior que zero.");

        var parcelamentoId = Guid.NewGuid();
        var valorParcela   = Math.Round(dto.ValorTotal / dto.NumeroParcelas, 2);
        var resto          = dto.ValorTotal - (valorParcela * dto.NumeroParcelas);

        using var scope = _serviceProvider.CreateScope();
        var ctx    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<IRequestTenant>();

        var parcelas = new List<ContaReceber>();
        for (int i = 1; i <= dto.NumeroParcelas; i++)
        {
            var valor      = i == dto.NumeroParcelas ? valorParcela + resto : valorParcela;
            var vencimento = dto.PrimeiroVencimento.AddDays(dto.IntervalosDias * (i - 1));

            parcelas.Add(new ContaReceber
            {
                Id             = Guid.NewGuid(),
                TenantId       = tenant.TenantId,
                CustomerId     = dto.CustomerId,
                SaleId         = dto.SaleId,
                ValorTotal     = valor,
                ValorRecebido  = 0m,
                DataEmissao    = DateTime.Now,
                DataVencimento = vencimento,
                Status         = "Pendente",
                NumeroParcela  = i,
                TotalParcelas  = dto.NumeroParcelas,
                ParcelamentoId = parcelamentoId,
                FormaPagamento = dto.FormaPagamento,
                Descricao      = string.IsNullOrWhiteSpace(dto.Descricao)
                    ? $"Parcela {i}/{dto.NumeroParcelas}"
                    : $"{dto.Descricao} — Parcela {i}/{dto.NumeroParcelas}"
            });
        }

        ctx.ContasReceber.AddRange(parcelas);
        await ctx.SaveChangesAsync();

        return parcelas.Select(MapToParcelaDto).ToList();
    }

    public async Task<IEnumerable<ParcelaDto>> GetParcelasByParcelamentoAsync(Guid parcelamentoId)
    {
        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var parcelas = await ctx.ContasReceber.AsNoTracking()
            .Where(c => c.ParcelamentoId == parcelamentoId)
            .OrderBy(c => c.NumeroParcela)
            .ToListAsync();

        return parcelas.Select(MapToParcelaDto);
    }

    public async Task<IEnumerable<ParcelaDto>> GetParcelasByVendaAsync(Guid vendaId)
    {
        using var scope = _serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var parcelas = await ctx.ContasReceber.AsNoTracking()
            .Where(c => c.SaleId == vendaId)
            .OrderBy(c => c.NumeroParcela)
            .ToListAsync();

        return parcelas.Select(MapToParcelaDto);
    }

    private static ParcelaDto MapToParcelaDto(ContaReceber c) => new()
    {
        Id             = c.Id,
        NumeroParcela  = c.NumeroParcela,
        TotalParcelas  = c.TotalParcelas,
        ValorTotal     = c.ValorTotal,
        ValorRecebido  = c.ValorRecebido,
        DataVencimento = c.DataVencimento,
        DataPagamento  = c.DataPagamento,
        Status         = c.Status,
        FormaPagamento = c.FormaPagamento,
        ParcelamentoId = c.ParcelamentoId
    };
}