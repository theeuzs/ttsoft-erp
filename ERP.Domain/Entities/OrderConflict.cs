// ── ERP.Domain/Entities/OrderConflict.cs ───────────────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Um problema de reconciliação que o motor não resolve sozinho (SKU sem
/// mapeamento, estoque insuficiente, pedido duplicado, preço divergente) —
/// fica visível numa fila de resolução manual até alguém marcar Resolvido.
/// </summary>
public class OrderConflict : BaseEntity
{
    public Guid           ExternalOrderId { get; set; }
    public ExternalOrder?  ExternalOrder   { get; set; }
    public Guid           CorrelationId   { get; set; }

    public OrderConflictType Tipo      { get; set; }
    public string             Descricao { get; set; } = string.Empty;

    public bool      Resolvido    { get; set; } = false;
    public DateTime? ResolvidoEm  { get; set; }
    public Guid?     ResolvidoPor { get; set; } // UserId

    /// <summary>Só preenchido quando Resolvido = true.</summary>
    public TipoResolucaoConflito? TipoResolucao { get; set; }
}
