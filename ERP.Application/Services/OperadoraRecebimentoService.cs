// ── ERP.Application/Services/OperadoraRecebimentoService.cs ──────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;

namespace ERP.Application.Services;

public class OperadoraRecebimentoService : IOperadoraRecebimentoService
{
    private readonly IUnitOfWork _uow;
    public OperadoraRecebimentoService(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<OperadoraRecebimentoDto>> ObterAtivasAsync()
    {
        var operadoras = await _uow.OperadorasRecebimento.GetAllAtivasAsync();
        return operadoras.Select(MapDto).ToList();
    }

    public async Task CriarAsync(CriarOperadoraRecebimentoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nome))
            throw new InvalidOperationException("Informe o nome da operadora (ex: Stone, Cielo).");

        var operadora = new OperadoraRecebimento
        {
            Nome                      = dto.Nome.Trim(),
            PrazoDebitoDias           = dto.PrazoDebitoDias,
            PrazoCreditoVistaDias     = dto.PrazoCreditoVistaDias,
            PrazoCreditoParceladoDias = dto.PrazoCreditoParceladoDias,
            AntecipacaoAutomatica     = dto.AntecipacaoAutomatica,
            TaxaDebitoPercentual           = dto.TaxaDebitoPercentual,
            TaxaCreditoVistaPercentual     = dto.TaxaCreditoVistaPercentual,
            TaxaCreditoParceladoPercentual = dto.TaxaCreditoParceladoPercentual,
            ContaDestinoId            = dto.ContaDestinoId,
            IsAtiva                   = true
        };

        await _uow.OperadorasRecebimento.AddAsync(operadora);
        await _uow.CommitAsync();
    }

    public async Task InativarAsync(Guid id)
    {
        var operadora = await _uow.OperadorasRecebimento.GetByIdAsync(id)
            ?? throw new InvalidOperationException("Operadora não encontrada.");

        operadora.IsAtiva = false;
        _uow.OperadorasRecebimento.Update(operadora);
        await _uow.CommitAsync();
    }

    public async Task DefinirComoPadraoAsync(Guid id)
        => await _uow.OperadorasRecebimento.DefinirPadraoAsync(id);

    private static OperadoraRecebimentoDto MapDto(OperadoraRecebimento o) => new()
    {
        Id                        = o.Id,
        Nome                      = o.Nome,
        PrazoDebitoDias           = o.PrazoDebitoDias,
        PrazoCreditoVistaDias     = o.PrazoCreditoVistaDias,
        PrazoCreditoParceladoDias = o.PrazoCreditoParceladoDias,
        AntecipacaoAutomatica     = o.AntecipacaoAutomatica,
        TaxaDebitoPercentual           = o.TaxaDebitoPercentual,
        TaxaCreditoVistaPercentual     = o.TaxaCreditoVistaPercentual,
        TaxaCreditoParceladoPercentual = o.TaxaCreditoParceladoPercentual,
        ContaDestinoId            = o.ContaDestinoId,
        ContaDestinoApelido       = o.ContaDestino?.Apelido,
        IsAtiva                   = o.IsAtiva,
        OperadoraPadrao           = o.OperadoraPadrao
    };
}