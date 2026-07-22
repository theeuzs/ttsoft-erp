// ── ERP.Application/DTOs/RecebivelOperadoraDtos.cs ────────────────────────────
using ERP.Domain.Enums;

namespace ERP.Application.DTOs;

public class RecebivelOperadoraDto
{
    public Guid    Id                     { get; set; }
    public string  OperadoraNome          { get; set; } = string.Empty;
    public FormaRecebimentoOperadora FormaRecebimento { get; set; }
    public decimal ValorBruto             { get; set; }
    public decimal ValorTaxa              { get; set; }
    public decimal ValorLiquido           { get; set; }
    public DateTime DataVenda             { get; set; }
    public DateTime DataPrevistaLiquidacao{ get; set; }
    public StatusRecebivel Status         { get; set; }
    public string? Nsu                    { get; set; }
}