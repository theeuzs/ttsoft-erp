using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// 1.6.4: IServiceProvider removido — injeção direta de AppDbContext e IRequestTenant.
/// O padrão CreateScope criava um novo scope sem herdar o tenant do request corrente,
/// fazendo queries sem filtro de tenant ou com tenant errado.
/// </summary>
public class ContaReceberService : IContaReceberService
{
    private readonly IUnitOfWork    _uow;
    private readonly AppDbContext   _ctx;
    private readonly IRequestTenant _tenant;
    private readonly AsaasService   _asaas;

    public ContaReceberService(
        IUnitOfWork uow, AppDbContext ctx, IRequestTenant tenant, AsaasService asaas)
    {
        _uow    = uow;
        _ctx    = ctx;
        _tenant = tenant;
        _asaas  = asaas;
    }

    public async Task GerarContaAPrazoAsync(Guid clienteId, Guid? vendaId, decimal valor, string descricao)
    {
        await _uow.ContasReceber.AddAsync(new ContaReceber
        {
            CustomerId     = clienteId,
            SaleId         = vendaId,
            ValorTotal     = valor,
            ValorRecebido  = 0,
            DataEmissao    = DateTime.Now,
            DataVencimento = DateTime.Now.AddDays(30),
            Status         = "Pendente",
            Descricao      = descricao
        });
        await _uow.CommitAsync();
    }

    public async Task<IEnumerable<ContaReceber>> GetPendentesAsync()
        => await _ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.Status == "Pendente")
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();

    public async Task<IEnumerable<ContaReceber>> GetPorClienteAsync(Guid clienteId)
        => await _ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.CustomerId == clienteId)
            .OrderByDescending(c => c.DataEmissao)
            .ToListAsync();

    public async Task<IEnumerable<ContaReceber>> GetInadimplentesAsync()
        => await _ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.Status == "Pendente" && c.DataVencimento.Date < DateTime.Today)
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();

    public async Task DarBaixaParcialAsync(Guid contaId, decimal valorRecebido)
    {
        var conta = await _ctx.ContasReceber.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contaId)
            ?? throw new KeyNotFoundException("Conta não encontrada.");

        var novoValorRecebido = Math.Min(conta.ValorRecebido + valorRecebido, conta.ValorTotal);
        var novoStatus        = novoValorRecebido >= conta.ValorTotal ? "Pago" : "Pendente";
        var dataPagamento     = novoStatus == "Pago" ? DateTime.Now : (DateTime?)null;
        var agora             = DateTime.UtcNow;
        var tenantId          = _tenant.TenantId;

        if (dataPagamento.HasValue)
        {
            var dp = dataPagamento.Value;
            await _ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ContasReceber SET ValorRecebido={novoValorRecebido}, Status={novoStatus}, DataPagamento={dp}, UpdatedAt={agora} WHERE Id={contaId} AND TenantId={tenantId}");
        }
        else
        {
            await _ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ContasReceber SET ValorRecebido={novoValorRecebido}, Status={novoStatus}, DataPagamento=NULL, UpdatedAt={agora} WHERE Id={contaId} AND TenantId={tenantId}");
        }
    }

    public async Task DarBaixaTotalAsync(Guid contaId)
    {
        var conta = await _ctx.ContasReceber.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contaId)
            ?? throw new KeyNotFoundException("Conta não encontrada.");

        await _ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE ContasReceber SET ValorRecebido={conta.ValorTotal}, Status={"Pago"}, DataPagamento={DateTime.Now}, UpdatedAt={DateTime.UtcNow} WHERE Id={contaId} AND TenantId={_tenant.TenantId}");
    }

    public async Task<(decimal TotalPendente, decimal TotalVencido, int QtdClientes)> GetResumoAsync()
    {
        var pendentes = await _ctx.ContasReceber.AsNoTracking()
            .Where(c => c.Status == "Pendente")
            .ToListAsync();

        return (
            pendentes.Sum(c => c.ValorTotal - c.ValorRecebido),
            pendentes.Where(c => c.DataVencimento.Date < DateTime.Today).Sum(c => c.ValorTotal - c.ValorRecebido),
            pendentes.Select(c => c.CustomerId).Distinct().Count()
        );
    }

    public async Task<int> CountInadimplentesAsync()
    {
        var contas = await _uow.ContasReceber.GetAllAsync();
        return contas.Count(c => c.DataVencimento.Date < DateTime.Today && c.Status == "Pendente");
    }

    public async Task<IEnumerable<ParcelaDto>> GerarParcelasAsync(GerarParcelasDto dto)
    {
        if (dto.NumeroParcelas < 1)
            throw new ArgumentException("Número de parcelas deve ser maior que zero.");

        var parcelamentoId = Guid.NewGuid();
        var valorParcela   = Math.Round(dto.ValorTotal / dto.NumeroParcelas, 2);
        var resto          = dto.ValorTotal - (valorParcela * dto.NumeroParcelas);

        var parcelas = Enumerable.Range(1, dto.NumeroParcelas).Select(i => new ContaReceber
        {
            Id             = Guid.NewGuid(),
            TenantId       = _tenant.TenantId,
            CustomerId     = dto.CustomerId,
            SaleId         = dto.SaleId,
            ValorTotal     = i == dto.NumeroParcelas ? valorParcela + resto : valorParcela,
            ValorRecebido  = 0m,
            DataEmissao    = DateTime.Now,
            DataVencimento = dto.PrimeiroVencimento.AddDays(dto.IntervalosDias * (i - 1)),
            Status         = "Pendente",
            NumeroParcela  = i,
            TotalParcelas  = dto.NumeroParcelas,
            ParcelamentoId = parcelamentoId,
            FormaPagamento = dto.FormaPagamento,
            Descricao      = string.IsNullOrWhiteSpace(dto.Descricao)
                ? $"Parcela {i}/{dto.NumeroParcelas}"
                : $"{dto.Descricao} — Parcela {i}/{dto.NumeroParcelas}"
        }).ToList();

        _ctx.ContasReceber.AddRange(parcelas);
        await _ctx.SaveChangesAsync();

        return parcelas.Select(MapToParcelaDto);
    }

    public async Task<IEnumerable<ParcelaDto>> GetParcelasByParcelamentoAsync(Guid parcelamentoId)
        => (await _ctx.ContasReceber.AsNoTracking()
            .Where(c => c.ParcelamentoId == parcelamentoId)
            .OrderBy(c => c.NumeroParcela)
            .ToListAsync()).Select(MapToParcelaDto);

    public async Task<IEnumerable<ParcelaDto>> GetParcelasByVendaAsync(Guid vendaId)
        => (await _ctx.ContasReceber.AsNoTracking()
            .Where(c => c.SaleId == vendaId)
            .OrderBy(c => c.NumeroParcela)
            .ToListAsync()).Select(MapToParcelaDto);

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

    // S15 FIX: movido de ContasReceberController.GerarBoleto — lógica idêntica,
    // só trocando IActionResult por um resultado tipado que o controller mapeia.
    public async Task<GerarBoletoResultado> GerarBoletoAsync(Guid contaId)
    {
        var conta = await _ctx.ContasReceber
            .Include(c => c.Customer)
            .Where(c => c.Id == contaId)
            .FirstOrDefaultAsync();

        if (conta is null)
            return new GerarBoletoResultado(GerarBoletoStatus.ContaNaoEncontrada);

        if (!string.IsNullOrEmpty(conta.AsaasPaymentId))
            return new GerarBoletoResultado(
                GerarBoletoStatus.JaPossuiBoleto,
                BoletoUrl: conta.BoletoUrl, InvoiceUrl: conta.InvoiceUrl,
                BoletoBarCode: conta.BoletoBarCode, AsaasStatus: conta.AsaasStatus);

        if (conta.Customer is null)
            return new GerarBoletoResultado(
                GerarBoletoStatus.ClienteNaoVinculado, Erro: "Conta sem cliente vinculado.");

        // 1. Obter/criar cliente no Asaas
        var cpfCnpj = conta.Customer.Document ?? "";
        if (string.IsNullOrEmpty(cpfCnpj))
            return new GerarBoletoResultado(
                GerarBoletoStatus.ClienteSemDocumento,
                Erro: "Cliente sem CPF/CNPJ cadastrado. Preencha antes de gerar boleto.");

        var asaasClientId = await _asaas.ObterOuCriarClienteAsync(
            conta.Customer.Name, cpfCnpj, conta.Customer.Email, conta.Customer.Phone);

        if (asaasClientId is null)
            return new GerarBoletoResultado(
                GerarBoletoStatus.FalhaAoRegistrarClienteAsaas,
                Erro: "Não foi possível registrar o cliente no Asaas. Verifique a API Key.");

        // 2. Gerar boleto
        var resultado = await _asaas.GerarBoletoAsync(
            asaasClientId,
            conta.ValorTotal - conta.ValorRecebido,
            conta.DataVencimento,
            $"{conta.Descricao} — Parcela {conta.NumeroParcela}/{conta.TotalParcelas}");

        if (resultado is null)
            return new GerarBoletoResultado(
                GerarBoletoStatus.FalhaAoGerarBoleto, Erro: "Erro ao gerar boleto no Asaas.");

        // 3. Salvar IDs na conta
        conta.AsaasPaymentId = resultado.AsaasPaymentId;
        conta.BoletoUrl      = resultado.BoletoUrl;
        conta.InvoiceUrl     = resultado.InvoiceUrl;
        conta.BoletoBarCode  = resultado.BoletoBarCode;
        conta.AsaasStatus    = resultado.Status;
        await _ctx.SaveChangesAsync();

        return new GerarBoletoResultado(
            GerarBoletoStatus.Sucesso,
            BoletoUrl: resultado.BoletoUrl, InvoiceUrl: resultado.InvoiceUrl,
            BoletoBarCode: resultado.BoletoBarCode, AsaasStatus: resultado.Status);
    }
}