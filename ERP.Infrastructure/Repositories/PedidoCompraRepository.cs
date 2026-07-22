using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public class PedidoCompraRepository : Repository<PedidoCompra>, IPedidoCompraRepository
{
    public PedidoCompraRepository(AppDbContext ctx) : base(ctx) { }

    // 🔧 FIX COMPRAS: O GetAllAsync do Repository base NÃO carrega os Itens.
    // Sem esse override, o pedido aparece com 0 produtos e R$ 0,00.
    public override async Task<IEnumerable<PedidoCompra>> GetAllAsync()
        => await _ctx.PedidosCompra
            .AsNoTracking()
            .Include(p => p.Itens)
            .Include(p => p.Supplier)
            .OrderByDescending(p => p.DataPedido)
            .ToListAsync();

    public async Task<PedidoCompra?> GetWithItensAsync(Guid id)
        => await _ctx.PedidosCompra
            .Include(p => p.Itens).ThenInclude(i => i.Product)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task RemoverItensAsync(Guid pedidoCompraId)
        => await _ctx.PedidosCompraItens
            .Where(i => i.PedidoCompraId == pedidoCompraId)
            .ExecuteDeleteAsync();

    public async Task AdicionarItensAsync(IEnumerable<PedidoCompraItem> itens)
        => await _ctx.PedidosCompraItens.AddRangeAsync(itens);

    public async Task<IEnumerable<PedidoCompraItem>> GetHistoricoPorProdutoAsync(Guid productId)
        => await _ctx.PedidosCompraItens
            .AsNoTracking()
            .Include(i => i.PedidoCompra)
            .Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.PedidoCompra.DataPedido)
            .ToListAsync();

    public async Task<IEnumerable<PedidoCompra>> GetByStatusAsync(StatusPedidoCompra status)
        => await _ctx.PedidosCompra
            .AsNoTracking()
            .Include(p => p.Itens)
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.DataPedido)
            .ToListAsync();

    public async Task<string> GerarProximoNumeroAsync()
    {
        int ano = DateTime.Now.Year;
        int count = await _ctx.PedidosCompra
            .Where(p => p.DataPedido.Year == ano)
            .CountAsync();

        return $"PC-{ano}-{(count + 1):D3}";
    }
}