using ERP.Domain.Entities;
using ERP.Domain.Enums;

namespace ERP.Domain.Interfaces;

public interface IPedidoCompraRepository : IRepository<PedidoCompra>
{
    Task<PedidoCompra?> GetWithItensAsync(Guid id);
    Task<IEnumerable<PedidoCompra>> GetByStatusAsync(StatusPedidoCompra status);
    Task<string> GerarProximoNumeroAsync();
}
