using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface ISugestaoComprasService
{
    /// <summary>
    /// Calcula sugestões de compra com base no giro dos últimos 30 dias.
    /// Retorna apenas produtos com ruptura em até 45 dias ou abaixo do estoque mínimo.
    /// Ordenado por urgência (DiasParaRuptura ASC).
    /// </summary>
    Task<IEnumerable<SugestaoCompraDto>> GetSugestoesAsync();

    /// <summary>
    /// Gera um PedidoCompra automaticamente com os itens selecionados.
    /// Agrupa por fornecedor — cada fornecedor gera um pedido separado.
    /// Retorna os IDs dos pedidos criados.
    /// </summary>
    Task<IEnumerable<Guid>> GerarPedidosCompraAsync(GerarPedidoCompraDto dto);
}
