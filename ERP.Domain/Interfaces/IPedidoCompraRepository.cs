using ERP.Domain.Entities;
using ERP.Domain.Enums;

namespace ERP.Domain.Interfaces;

public interface IPedidoCompraRepository : IRepository<PedidoCompra>
{
    Task<PedidoCompra?> GetWithItensAsync(Guid id);
    Task<IEnumerable<PedidoCompra>> GetByStatusAsync(StatusPedidoCompra status);
    Task<string> GerarProximoNumeroAsync();

    /// <summary>
    /// Remove todos os itens de um pedido via DELETE direto (ExecuteDeleteAsync) —
    /// necessário porque o contexto usa QueryTrackingBehavior.NoTracking global.
    /// Um simples "Itens.Clear() + Update(pedido)" não apagaria os itens antigos,
    /// já que o EF não tem estado "antes" pra comparar num grafo destacado.
    /// </summary>
    Task RemoverItensAsync(Guid pedidoCompraId);

    /// <summary>
    /// Insere itens novos explicitamente como Added. Necessário porque
    /// BaseEntity.Id já vem preenchido (Guid.NewGuid()) no momento da criação —
    /// se esses itens fossem adicionados via pedido.Itens + Update(pedido), o EF
    /// veria um Guid não-vazio e assumiria (errado) que a linha já existe,
    /// gerando UPDATE em vez de INSERT.
    /// </summary>
    Task AdicionarItensAsync(IEnumerable<PedidoCompraItem> itens);

    /// <summary>
    /// Item 1.3 do roadmap: todo item de pedido de compra já lançado para um
    /// produto específico, com o pedido (fornecedor, data, número) incluído —
    /// para montar o histórico de compras por produto, por fornecedor.
    /// </summary>
    Task<IEnumerable<PedidoCompraItem>> GetHistoricoPorProdutoAsync(Guid productId);
}