// ── ERP.Domain/Entities/ExternalOrder.cs ──────────────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Um pedido recebido de um SalesChannel. ExternalStatus guarda o texto cru do
/// marketplace (sem tradução) — só para exibição/debug. InternalStatus é o
/// vocabulário nosso, usado por toda a lógica de processamento.
///
/// CorrelationId: um GUID que amarra este pedido a todo OrderEvent, OrderAction
/// e OrderConflict gerados por ele — um SELECT por CorrelationId mostra a vida
/// inteira do pedido, do recebimento até a Sale gerada (ou o cancelamento).
/// Por padrão nasce igual ao Id, mas é campo próprio porque um reenvio do mesmo
/// pedido pelo canal (nova ExternalOrder) pode precisar herdar a correlação antiga.
/// </summary>
public class ExternalOrder : BaseEntity
{
    public Guid          SalesChannelId { get; set; }
    public SalesChannel?  SalesChannel   { get; set; }

    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    public string ExternalOrderId { get; set; } = string.Empty; // id do pedido no marketplace
    public string ExternalStatus  { get; set; } = string.Empty; // texto cru do canal, sem tradução

    public ExternalOrderStatus InternalStatus { get; set; } = ExternalOrderStatus.Recebido;

    /// <summary>Preenchido quando InternalStatus vira VendaGerada.</summary>
    public Guid?  VendaId { get; set; }
    public Sale?  Venda   { get; set; }

    public DateTime DataPedidoExterno { get; set; }
    public decimal  ValorTotal        { get; set; }

    /// <summary>Payload cru recebido do canal — guardado pra debug/auditoria, nunca parseado de novo.</summary>
    public string? RawPayloadJson { get; set; }

    public List<ExternalOrderItem> Itens { get; set; } = new();
}
