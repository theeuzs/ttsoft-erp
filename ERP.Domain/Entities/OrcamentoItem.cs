using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>Achado de auditoria (06/08/2026): mesma correção do NfePendente
/// — não tinha TenantId nenhum, dependia de filtro manual.</summary>
public class OrcamentoItem : BaseEntity
{
    public Guid OrcamentoId { get; set; }
    public Orcamento Orcamento { get; set; } = null!;

    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }

    // O banco já calcula o total do item na hora
    public decimal Total => Quantity * UnitPrice * (1 - DiscountPercent / 100);
}