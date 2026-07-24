// ── ERP.Infrastructure/Repositories/OrderSyncRepository.cs ────────────────────
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public class OrderSyncRepository : IOrderSyncRepository
{
    private readonly AppDbContext _ctx;
    public OrderSyncRepository(AppDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<SalesChannel>> GetCanaisAtivosAsync()
        => await _ctx.SalesChannels.Where(c => c.IsAtivo).ToListAsync();

    public async Task<SalesChannel?> GetCanalByIdAsync(Guid id)
        => await _ctx.SalesChannels.AsTracking().FirstOrDefaultAsync(c => c.Id == id);

    public async Task<SalesChannel?> GetCanalPorIdSemFiltroAsync(Guid id)
        => await _ctx.SalesChannels.IgnoreQueryFilters().AsTracking().FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

    public async Task AtualizarTokensAsync(Guid salesChannelId, string? accessToken, string? refreshToken,
        DateTime tokenExpiraEm, string? externalAccountId)
    {
        var canal = await _ctx.SalesChannels.IgnoreQueryFilters().AsTracking()
            .FirstOrDefaultAsync(c => c.Id == salesChannelId && !c.IsDeleted)
            ?? throw new InvalidOperationException($"SalesChannel {salesChannelId} não encontrado ao atualizar tokens.");

        canal.AccessToken   = accessToken;
        canal.RefreshToken  = refreshToken;
        canal.TokenExpiraEm = tokenExpiraEm;
        if (externalAccountId is not null) canal.ExternalAccountId = externalAccountId;

        await _ctx.SaveChangesAsync();
    }

    public async Task MarcarVendaGeradaAsync(Guid externalOrderId, Guid vendaId)
    {
        var pedido = await _ctx.ExternalOrders.AsTracking().FirstOrDefaultAsync(o => o.Id == externalOrderId)
            ?? throw new InvalidOperationException($"ExternalOrder {externalOrderId} não encontrado ao marcar venda gerada.");
        pedido.VendaId = vendaId;
        pedido.InternalStatus = ExternalOrderStatus.VendaGerada;
        await _ctx.SaveChangesAsync();
    }

    public string NomeDoBancoConectado()
        => _ctx.Database.GetDbConnection().Database;

    // ⚠️ ÚNICA exceção ao isolamento de tenant em todo o módulo de marketplace.
    // No momento do webhook não existe JWT/tenant ainda — é isso que estamos
    // descobrindo. Só é seguro porque quem chama este método (MarketplaceController)
    // já validou a assinatura HMAC do webhook ANTES de chamar isso — ou seja,
    // o external_account_id usado aqui vem de um payload que o próprio Mercado
    // Livre/Shopee assinou, não de entrada arbitrária de um chamador não autenticado.
    // (SalesChannelType, ExternalAccountId) é único no mundo real — só uma loja
    // pode ser dona de uma conta de vendedor específica.
    public async Task<SalesChannel?> GetCanalPorContaExternaAsync(SalesChannelType tipo, string externalAccountId)
        => await _ctx.SalesChannels
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Tipo == tipo && c.ExternalAccountId == externalAccountId && !c.IsDeleted);

    public async Task<ExternalOrder?> GetExternalOrderAsync(Guid salesChannelId, string externalOrderId)
        => await _ctx.ExternalOrders
            .Include(o => o.Itens)
            .AsTracking()
            .FirstOrDefaultAsync(o => o.SalesChannelId == salesChannelId && o.ExternalOrderId == externalOrderId);

    public async Task<bool> TentarInserirExternalOrderAsync(ExternalOrder pedido)
    {
        await _ctx.ExternalOrders.AddAsync(pedido);
        try
        {
            await _ctx.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            // Corrida: outra requisição concorrente (mesmo webhook, entrega duplicada
            // quase simultânea) já inseriu esse (SalesChannelId, ExternalOrderId).
            // Destrava o rastreamento dessa tentativa perdida antes de devolver false —
            // senão essas entidades "Added" ficam penduradas no ChangeTracker.
            _ctx.Entry(pedido).State = EntityState.Detached;
            foreach (var item in pedido.Itens)
                _ctx.Entry(item).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<SkuMapping?> GetSkuMappingAsync(Guid salesChannelId, string skuExterno)
        => await _ctx.SkuMappings
            .FirstOrDefaultAsync(m => m.SalesChannelId == salesChannelId && m.SkuExterno == skuExterno);

    public async Task<decimal> GetTotalReservadoAsync(Guid productId)
        => await _ctx.ShadowStockReservations
            .Where(r => r.ProductId == productId && r.Status == StatusReservaEstoque.Reservada)
            .SumAsync(r => (decimal?)r.Quantidade) ?? 0m;

    public async Task AddShadowStockReservationAsync(ShadowStockReservation reserva)
        => await _ctx.ShadowStockReservations.AddAsync(reserva);

    public async Task<IReadOnlyList<ShadowStockReservation>> GetReservasAtivasPorPedidoAsync(Guid externalOrderId)
        => await _ctx.ShadowStockReservations
            .AsTracking()
            .Where(r => r.ExternalOrderId == externalOrderId && r.Status == StatusReservaEstoque.Reservada)
            .ToListAsync();

    public async Task AddOrderEventAsync(OrderEvent evento)
        => await _ctx.OrderEvents.AddAsync(evento);

    public async Task AddOrderActionAsync(OrderAction acao)
        => await _ctx.OrderActions.AddAsync(acao);

    public async Task AddOrderConflictAsync(OrderConflict conflito)
        => await _ctx.OrderConflicts.AddAsync(conflito);

    public async Task AddProcessingSessionAsync(ProcessingSession sessao)
        => await _ctx.ProcessingSessions.AddAsync(sessao);

    public async Task<Customer?> GetClienteRepasseAsync(Guid salesChannelId)
    {
        var canal = await _ctx.SalesChannels.FirstOrDefaultAsync(c => c.Id == salesChannelId);
        if (canal?.ClienteRepasseId is null) return null;
        return await _ctx.Customers.FirstOrDefaultAsync(c => c.Id == canal.ClienteRepasseId);
    }

    /// <summary>
    /// Cria (uma única vez, na primeira venda do canal) o Customer sintético
    /// que representa o marketplace como devedor do repasse. Document é um
    /// placeholder identificável, não um CNPJ real — se algum dia precisar
    /// emitir nota fiscal contra esse "cliente", o CNPJ de verdade da Shopee/
    /// Mercado Livre precisa ser preenchido manualmente antes.
    /// </summary>
    public async Task<Customer> CriarClienteRepasseAsync(SalesChannel canal)
    {
        // Document precisa caber em nvarchar(18) — usa o valor numérico do enum
        // (1 ou 2 dígitos), não o nome por extenso ("MERCADOLIVRE" já estoura sozinho).
        var document = $"MKT{(int)canal.Tipo}-{canal.Id.ToString()[..8]}";
        var cliente = new Customer
        {
            Name     = $"{canal.Tipo} — Repasse ({canal.Nome})",
            Document = document,
        };
        await _ctx.Customers.AddAsync(cliente);

        try
        {
            await _ctx.SaveChangesAsync(); // precisa do Id gerado antes de linkar no SalesChannel
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627))
        {
            // Corrida: dois pedidos do mesmo canal chegando quase juntos, ambos
            // tentando criar o cliente de repasse pela primeira vez. Diferente da
            // corrida no ExternalOrder, aqui é seguro reaproveitar — é o mesmo
            // cliente sintético compartilhado por todos os pedidos do canal,
            // não importa qual pedido disparou a criação dele.
            _ctx.Entry(cliente).State = EntityState.Detached;
            var jaExiste = await _ctx.Customers.AsTracking().FirstOrDefaultAsync(c => c.Document == document)
                ?? throw new InvalidOperationException(
                    $"Violação de chave única em Customers.Document='{document}', mas não achei a linha ao reconsultar.");
            canal.ClienteRepasseId = jaExiste.Id;
            return jaExiste;
        }

        canal.ClienteRepasseId = cliente.Id;
        return cliente;
    }

    public async Task<int> SalvarAsync() => await _ctx.SaveChangesAsync();
}