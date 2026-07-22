// ── ERP.Domain/Entities/ExternalOrderItem.cs ──────────────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Item de um ExternalOrder. ProductId fica null até o SkuExterno ser resolvido
/// via SkuMapping — enquanto isso o ExternalOrder inteiro fica com InternalStatus
/// = AguardandoSku (ver ExternalOrderStatus).
/// </summary>
public class ExternalOrderItem : BaseEntity
{
    public Guid           ExternalOrderId { get; set; }
    public ExternalOrder?  ExternalOrder   { get; set; }

    public string  SkuExterno    { get; set; } = string.Empty;
    public string  DescricaoItem { get; set; } = string.Empty; // texto do canal, só exibição

    public Guid?    ProductId { get; set; }
    public Product? Product   { get; set; }

    public decimal Quantidade    { get; set; }
    public decimal ValorUnitario { get; set; }
}
