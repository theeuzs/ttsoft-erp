using ERP.Application.DTOs;
using ERP.Domain.Enums;

namespace ERP.Application.Interfaces;

public interface IPedidoCompraService
{
    Task<IEnumerable<PedidoCompraDto>> GetAllAsync();
    Task<PedidoCompraDto?> GetByIdAsync(Guid id);
    Task<PedidoCompraDto> CriarAsync(CreatePedidoCompraDto dto);
    Task EnviarAsync(Guid id);
    Task ReceberAsync(Guid id);    // Atualiza estoque automaticamente
    Task CancelarAsync(Guid id);
    Task DeletarAsync(Guid id);
}
