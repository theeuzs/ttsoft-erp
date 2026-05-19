using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Registra cada devolução parcial feita em um item de venda.
/// Permite somar o total devolvido e bloquear devoluções além da quantidade vendida.
/// </summary>
public class SaleItemDevolucao : BaseEntity
{
    public Guid   SaleId              { get; set; }
    public Guid   ProductId           { get; set; }
    public string ProductName         { get; set; } = string.Empty;
    public decimal QuantidadeDevolvida { get; set; }
    public decimal ValorDevolvido      { get; set; }
    public string? Motivo              { get; set; }
    public string? OperadorNome        { get; set; }
    public DateTime DataDevolucao      { get; set; } = DateTime.Now;
}
