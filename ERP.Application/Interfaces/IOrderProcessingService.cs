using ERP.Domain.Entities;

namespace ERP.Application.Interfaces;

/// <summary>
/// Motor de sincronização de pedidos de marketplace — método sequencial com
/// passos privados, estilo MotorFinanceiroService. Sem framework de pipeline:
/// um IChannelDispatcher busca os pedidos crus, este serviço decide o que
/// fazer com cada um (resolver SKU, reservar estoque, gerar venda) e registra
/// cada passo em OrderEvent/OrderAction, com OrderConflict pra tudo que não
/// se resolve sozinho.
/// </summary>
public interface IOrderProcessingService
{
    /// <summary>
    /// Processa um único pedido, identificado pelo id externo — usado pelo
    /// webhook (que informa "o pedido X mudou", não uma rodada de lote). Não
    /// cria ProcessingSession — isso é reservado pra rodadas de polling em lote.
    /// </summary>
    Task ProcessarPedidoIndividualAsync(Guid salesChannelId, string externalOrderId);

    /// <summary>
    /// Processa uma rodada de sincronização pra um canal específico. Cria e
    /// fecha um ProcessingSession, buscando pedidos desde a última rodada
    /// bem-sucedida (ou desde a data informada, se for a primeira vez).
    /// </summary>
    Task<ProcessingSession> ProcessarCanalAsync(Guid salesChannelId, DateTime desde);
}