// ── ERP.Domain/Entities/OrderAction.cs ─────────────────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Uma ação que o motor de processamento tentou executar para um ExternalOrder
/// (resolver SKU, reservar estoque, gerar venda, etc.) e o resultado dela.
/// Status é sempre um de três valores concretos — nunca um estado intermediário
/// ambíguo tipo "processando parcialmente"; se algo ficou pela metade, isso vira
/// um OrderConflict explícito, não um status de Action em limbo.
/// </summary>
public class OrderAction : BaseEntity
{
    public Guid           ExternalOrderId { get; set; }
    public ExternalOrder?  ExternalOrder   { get; set; }
    public Guid           CorrelationId   { get; set; }

    public OrderActionType   Tipo   { get; set; }
    public OrderActionStatus Status { get; set; } = OrderActionStatus.Pendente;

    public ProcessingErrorCode? ErroCodigo   { get; set; }
    public string?              ErroMensagem { get; set; }

    public DateTime  DataHora     { get; set; } = DateTime.UtcNow;
    public DateTime? ConcluidaEm  { get; set; }
}
