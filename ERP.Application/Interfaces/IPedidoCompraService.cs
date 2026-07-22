using ERP.Application.DTOs;
using ERP.Domain.Enums;

namespace ERP.Application.Interfaces;

public interface IPedidoCompraService
{
    Task<IEnumerable<PedidoCompraDto>> GetAllAsync();
    Task<PedidoCompraDto?> GetByIdAsync(Guid id);
    Task<PedidoCompraDto> CriarAsync(CreatePedidoCompraDto dto);
    Task AtualizarAsync(Guid id, AtualizarPedidoCompraDto dto);
    Task EnviarAsync(Guid id);
    Task ReceberAsync(Guid id);    // Atualiza estoque automaticamente
    Task CancelarAsync(Guid id);
    Task DeletarAsync(Guid id);

    /// <summary>Item 1.3 do roadmap: histórico de compras de um produto, por fornecedor.</summary>
    Task<IReadOnlyList<HistoricoCompraProdutoDto>> GetHistoricoPorProdutoAsync(Guid productId);
}