// ── ERP.Domain/Entities/VendaSuspensaItem.cs ──────────────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>Um item do carrinho no momento em que a venda foi suspensa.</summary>
public class VendaSuspensaItem : BaseEntity
{
    public Guid           VendaSuspensaId { get; set; }
    public VendaSuspensa? VendaSuspensa   { get; set; }

    public Guid   ProductId   { get; set; }
    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity        { get; set; }
    public decimal NormalUnitPrice { get; set; }
    public decimal UnitPrice       { get; set; }

    public string Observacao { get; set; } = string.Empty;

    public decimal FatorConversao    { get; set; } = 1;
    public string  UnidadeEstoque    { get; set; } = string.Empty;
    public string  LabelUnidadeVenda { get; set; } = string.Empty;

    public decimal? WholesalePrice       { get; set; }
    public decimal? WholesaleMinQuantity { get; set; }
}
