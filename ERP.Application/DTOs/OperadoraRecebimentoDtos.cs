// ── ERP.Application/DTOs/OperadoraRecebimentoDtos.cs ──────────────────────────
namespace ERP.Application.DTOs;

public class OperadoraRecebimentoDto
{
    public Guid   Id                        { get; set; }
    public string Nome                      { get; set; } = string.Empty;
    public int    PrazoDebitoDias           { get; set; }
    public int    PrazoCreditoVistaDias     { get; set; }
    public int    PrazoCreditoParceladoDias { get; set; }
    public bool   AntecipacaoAutomatica     { get; set; }
    public decimal TaxaDebitoPercentual           { get; set; }
    public decimal TaxaCreditoVistaPercentual     { get; set; }
    public decimal TaxaCreditoParceladoPercentual { get; set; }
    public Guid?  ContaDestinoId            { get; set; }
    public string? ContaDestinoApelido      { get; set; }
    public bool   IsAtiva                   { get; set; } = true;
    public bool   OperadoraPadrao           { get; set; } = false;
}

public class CriarOperadoraRecebimentoDto
{
    public string Nome                      { get; set; } = string.Empty;
    public int    PrazoDebitoDias           { get; set; } = 1;
    public int    PrazoCreditoVistaDias     { get; set; } = 1;
    public int    PrazoCreditoParceladoDias { get; set; } = 30;
    public bool   AntecipacaoAutomatica     { get; set; } = false;
    public decimal TaxaDebitoPercentual           { get; set; } = 0m;
    public decimal TaxaCreditoVistaPercentual     { get; set; } = 0m;
    public decimal TaxaCreditoParceladoPercentual { get; set; } = 0m;
    public Guid?  ContaDestinoId            { get; set; }
}