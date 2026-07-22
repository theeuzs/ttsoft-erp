// ── ERP.Domain/Entities/OrderEvent.cs ──────────────────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Log append-only de tudo que aconteceu com um ExternalOrder — nunca é
/// atualizado nem apagado, só inserido. É o histórico legível por humano;
/// OrderAction é o lado "o que o sistema decidiu fazer a respeito".
/// </summary>
public class OrderEvent : BaseEntity
{
    public Guid           ExternalOrderId { get; set; }
    public ExternalOrder?  ExternalOrder   { get; set; }
    public Guid           CorrelationId   { get; set; }

    public OrderEventType     Tipo      { get; set; }
    public OrderEventSeverity Severity  { get; set; } = OrderEventSeverity.Info;
    public string             Descricao { get; set; } = string.Empty;
    public DateTime           DataHora  { get; set; } = DateTime.UtcNow;

    /// <summary>Detalhe estruturado do evento, quando fizer sentido (opcional).</summary>
    public string? PayloadJson { get; set; }
}
