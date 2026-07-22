// S3.7: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;

namespace ERP.Infrastructure.Services;

/// <summary>
/// ⚠️ ML: a lógica antiga daqui (ProcessarWebhookMLAsync, ProcessarPedidoMLAsync,
/// SincronizarEstoqueMLAsync) foi REMOVIDA — nunca chegou a rodar em produção
/// (auditado e confirmado), e o Mercado Livre agora é atendido pelo módulo novo
/// (SalesChannel multi-tenant + OAuth real + MercadoLivreDispatcher +
/// OrderProcessingService, com Sale/ContaReceber/auditoria completos em vez de
/// UPDATE direto no Stock). Ver MarketplaceController para os endpoints atuais de ML.
///
/// Shopee continua aqui por enquanto — dispatcher próprio ainda não foi construído
/// (está pausado no roadmap), então esse caminho permanece intocado por ora.
/// </summary>
public class MarketplaceService
{
    private readonly IServiceProvider  _sp;
    private readonly IProductService   _products; // não usado agora — Shopee ainda não sincroniza estoque; mantido pra quando isso for construído
    private readonly HttpClient        _http;

    public MarketplaceService(IServiceProvider sp, IProductService products, HttpClient http)
    {
        _sp       = sp;
        _products = products;
        _http     = http;
    }

    // ── Shopee ────────────────────────────────────────────────────────────────

    public async Task ProcessarWebhookShopeeAsync(ShopeeWebhookDto dto, Guid tenantId)
    {
        if (dto.Code != 3) return;

        using var scope = _sp.CreateScope();
        var ctx    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ERP.Application.Interfaces.IRequestTenant>();
        tenant.TenantId = tenantId;

        foreach (var item in dto.Data?.OrderList ?? [])
        {
            // ── Round-trip: confirmar pedido na Shopee antes de baixar estoque ──
            // Impede que webhook falso corrompa estoque sem que o pedido exista de fato.
            var orderConfirmado = await ConfirmarPedidoShopeeAsync(item.OrderSn);
            if (!orderConfirmado)
            {
                Log.Warning("Shopee: pedido {OrderSn} não confirmado via API — ignorando.", item.OrderSn);
                continue;
            }

            foreach (var produto in item.ItemList ?? [])
            {
                var qtd = produto.ModelQuantityPurchased;
                var sku = produto.ModelSku;

                await ctx.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE Products SET Stock = Stock - {qtd} WHERE SKU = {sku} AND TenantId = {tenantId} AND Stock > 0");

                Log.Information("Shopee [{TenantId}]: baixou {Qtd} do SKU {SKU}", tenantId, qtd, sku);
            }
        }
    }

    /// <summary>
    /// Confirma que o pedido existe e está pago via API da Shopee.
    /// Retorna false se o pedido não existir ou não estiver com status pago/pronto.
    /// </summary>
    private async Task<bool> ConfirmarPedidoShopeeAsync(string? orderSn)
    {
        if (string.IsNullOrEmpty(orderSn)) return false;
        try
        {
            // Shopee Get Order Detail: verifica status real do pedido
            var url      = $"https://partner.shopeemobile.com/api/v2/order/get_order_detail?order_sn_list={orderSn}";
            var response = await _http.GetStringAsync(url);
            using var doc = System.Text.Json.JsonDocument.Parse(response);

            // Aceita apenas pedidos com status READY_TO_SHIP ou SHIPPED
            var statusesValidos = new[] { "READY_TO_SHIP", "SHIPPED", "COMPLETED" };
            if (doc.RootElement.TryGetProperty("response", out var resp) &&
                resp.TryGetProperty("order_list", out var orders) &&
                orders.GetArrayLength() > 0)
            {
                var status = orders[0].TryGetProperty("order_status", out var s) ? s.GetString() : null;
                return statusesValidos.Contains(status, StringComparer.OrdinalIgnoreCase);
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Shopee: erro ao confirmar pedido {OrderSn}.", orderSn);
            return false; // fail-safe: em caso de erro, não baixa estoque
        }
    }

    // ── Sincronização em lote ─────────────────────────────────────────────────
    // Removida junto com o resto da lógica de ML — SincronizarEstoqueAsync do
    // IChannelDispatcher (MercadoLivreDispatcher) é o caminho novo, ainda não
    // implementado de propósito (ver comentário no dispatcher).

}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class ShopeeWebhookDto
{
    public int         Code      { get; set; }
    public ShopeeData? Data      { get; set; }
    public long        Timestamp { get; set; }
}

public class ShopeeData
{
    public List<ShopeeOrder>? OrderList { get; set; }
}

public class ShopeeOrder
{
    public string?           OrderSn  { get; set; }
    public List<ShopeeItem>? ItemList { get; set; }
}

public class ShopeeItem
{
    public string? ModelSku               { get; set; }
    public int     ModelQuantityPurchased { get; set; }
}