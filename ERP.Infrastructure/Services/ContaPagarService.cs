// I.3: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync em PagarAsync e CancelarAsync.
// Todos os outros métodos idênticos ao original.
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class ContaPagarService : IContaPagarService
{
    private readonly AppDbContext   _ctx;
    private readonly IUnitOfWork    _uow;
    private readonly IRequestTenant _tenant;

    public ContaPagarService(AppDbContext ctx, IUnitOfWork uow, IRequestTenant tenant)
    {
        _ctx    = ctx;
        _uow    = uow;
        _tenant = tenant;
    }

    public async Task<int> CountVencendoHojeAsync()
    {
        var contas = await _uow.ContasPagar.GetAllAsync();
        return contas.Count(c =>
            c.DataVencimento.Date == DateTime.Today && c.Status == "Pendente");
    }

    public async Task<IEnumerable<(string Descricao, decimal Valor)>> GetVencendoHojeAsync()
    {
        var contas = await _uow.ContasPagar.GetAllAsync();
        return contas
            .Where(c => c.DataVencimento.Date == DateTime.Today && c.Status == "Pendente")
            .Select(c => (c.Descricao, c.Valor));
    }

    public async Task<IReadOnlyList<ContaPagarDto>> GetPendentesAsync(CancellationToken ct = default)
    {
        return await _ctx.ContasPagar
            .AsNoTracking()
            .Where(c => c.Status != "Pago" && c.Status != "Cancelado")
            .OrderBy(c => c.DataVencimento)
            .Select(c => new ContaPagarDto
            {
                Id             = c.Id,
                Descricao      = c.Descricao,
                Valor          = c.Valor,
                Categoria      = c.Categoria,
                DataVencimento = c.DataVencimento,
                DataPagamento  = c.DataPagamento,
                Status         = c.Status
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ContaPagarDto>> GetVencidasAsync(CancellationToken ct = default)
    {
        var hoje = DateTime.Today;
        return await _ctx.ContasPagar
            .AsNoTracking()
            .Where(c => c.Status == "Pendente" && c.DataVencimento < hoje)
            .OrderBy(c => c.DataVencimento)
            .Select(c => new ContaPagarDto
            {
                Id             = c.Id,
                Descricao      = c.Descricao,
                Valor          = c.Valor,
                Categoria      = c.Categoria,
                DataVencimento = c.DataVencimento,
                DataPagamento  = c.DataPagamento,
                Status         = c.Status
            })
            .ToListAsync(ct);
    }

    public async Task<ContaPagarResumoDto> GetResumoAsync(CancellationToken ct = default)
    {
        var hoje  = DateTime.Today;
        var resumo = await _ctx.ContasPagar
            .AsNoTracking()
            .Where(c => c.Status == "Pendente")
            .GroupBy(_ => 1)
            .Select(g => new ContaPagarResumoDto
            {
                TotalPendente = g.Sum(c => c.Valor),
                TotalVencido  = g.Where(c => c.DataVencimento < hoje).Sum(c => c.Valor),
                QtdContas     = g.Count(),
                QtdVencidas   = g.Count(c => c.DataVencimento < hoje)
            })
            .FirstOrDefaultAsync(ct);

        return resumo ?? new ContaPagarResumoDto();
    }

    public async Task<ContaPagarDto> CreateAsync(
        CreateContaPagarDto dto, CancellationToken ct = default)
    {
        var conta = new ContaPagar
        {
            TenantId       = _tenant.TenantId,
            Descricao      = dto.Descricao,
            Valor          = dto.Valor,
            DataVencimento = dto.DataVencimento,
            Categoria      = dto.Categoria ?? "Variável",
            Status         = "Pendente"
        };

        _ctx.ContasPagar.Add(conta);
        await _ctx.SaveChangesAsync(ct);
        return MapToDto(conta);
    }

    /// <summary>
    /// I.3: ExecuteSqlInterpolatedAsync — safety by design.
    /// AND TenantId mantido — defesa em profundidade contra IDOR (S1.4).
    /// </summary>
    public async Task PagarAsync(Guid id, CancellationToken ct = default)
    {
        var hoje      = DateTime.Today;
        var agora     = DateTime.UtcNow;
        var tenantId  = _tenant.TenantId;
        var statusPago = "Pago";

        var rows = await _ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ContasPagar SET Status={statusPago}, DataPagamento={hoje}, UpdatedAt={agora} WHERE Id={id} AND TenantId={tenantId}");

        if (rows == 0)
            throw new KeyNotFoundException(
                $"Conta {id} não encontrada ou não pertence ao tenant atual.");
    }

    /// <summary>I.3: ExecuteSqlInterpolatedAsync. AND TenantId mantido (S1.4).</summary>
    public async Task CancelarAsync(Guid id, CancellationToken ct = default)
    {
        var agora          = DateTime.UtcNow;
        var tenantId       = _tenant.TenantId;
        var statusCancelado = "Cancelado";

        var rows = await _ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ContasPagar SET Status={statusCancelado}, UpdatedAt={agora} WHERE Id={id} AND TenantId={tenantId}");

        if (rows == 0)
            throw new KeyNotFoundException(
                $"Conta {id} não encontrada ou não pertence ao tenant atual.");
    }

    private static ContaPagarDto MapToDto(ContaPagar c) => new()
    {
        Id             = c.Id,
        Descricao      = c.Descricao,
        Valor          = c.Valor,
        Categoria      = c.Categoria,
        DataVencimento = c.DataVencimento,
        DataPagamento  = c.DataPagamento,
        Status         = c.Status
    };
}