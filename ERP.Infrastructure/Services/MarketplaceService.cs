// S3.7: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Text.Json;

namespace ERP.Infrastructure.Services;

public class MarketplaceService
{
    private readonly IServiceProvider  _sp;
    private readonly IProductService   _products;
    private readonly HttpClient        _http;

    public MarketplaceService(IServiceProvider sp, IProductService products, HttpClient http)
    {
        _sp       = sp;
        _products = products;
        _http     = http;
    }

    // ── Mercado Livre ─────────────────────────────────────────────────────────

    public async Task<bool> ProcessarWebhookMLAsync(string topico, string recurso, string accessToken, Guid tenantId)
    {
        try
        {
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var response = await _http.GetStringAsync($"https://api.mercadolibre.com{recurso}");
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (topico == "orders")
            {
                await ProcessarPedidoMLAsync(root, tenantId);
                return true;
            }

            if (topico == "items")
            {
                var sku = root.TryGetProperty("seller_custom_field", out var s) ? s.GetString() : null;
                if (sku != null)
                {
                    var produto = await _products.GetBySkuAsync(sku);
                    if (produto != null)
                        await SincronizarEstoqueMLAsync(root.GetProperty("id").GetString()!, (int)produto.Stock, accessToken);
                }
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao processar webhook ML. Tópico: {Topico}", topico);
            return false;
        }
    }

    // Fase 1.5 Fix: tenantId vem da URL do webhook (configurada por loja no ML).
    // O scope é necessário porque webhooks são [AllowAnonymous] — não há JWT.
    // Injetamos o tenant no IRequestTenant do scope para que HasQueryFilter funcione.
    private async Task ProcessarPedidoMLAsync(JsonElement pedido, Guid tenantId)
    {
        var status = pedido.TryGetProperty("status", out var s) ? s.GetString() : "";
        if (status != "paid") return;

        using var scope = _sp.CreateScope();
        var ctx    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenant = scope.ServiceProvider.GetRequiredService<ERP.Application.Interfaces.IRequestTenant>();
        tenant.TenantId = tenantId; // Seta o tenant no scope para HasQueryFilter funcionar

        if (!pedido.TryGetProperty("order_items", out var itens)) return;

        foreach (var item in itens.EnumerateArray())
        {
            var sku = item.TryGetProperty("item", out var i) &&
                      i.TryGetProperty("seller_custom_field", out var scf)
                ? scf.GetString() : null;

            if (sku == null) continue;

            var qtd = item.TryGetProperty("quantity", out var q) ? q.GetDecimal() : 0;

            // AND TenantId= impede baixa de estoque de outro tenant com mesmo SKU (EAN colidente).
            await ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET Stock = Stock - {qtd} WHERE SKU = {sku} AND TenantId = {tenantId} AND Stock > 0");

            Log.Information("ML Pedido [{TenantId}]: baixou {Qtd} do SKU {SKU}", tenantId, qtd, sku);
        }
    }

    private async Task SincronizarEstoqueMLAsync(string itemId, int novoEstoque, string token)
    {
        var payload = JsonSerializer.Serialize(new { available_quantity = novoEstoque });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        await _http.PutAsync($"https://api.mercadolibre.com/items/{itemId}", content);
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

    public async Task<SyncResultDto> SincronizarEstoqueAsync(string marketplace, string accessToken)
    {
        var produtos = await _products.GetAllAsync();
        int sucesso = 0, falha = 0;

        foreach (var p in produtos.Where(x => !string.IsNullOrEmpty(x.SKU)))
        {
            try
            {
                if (marketplace == "mercadolivre")
                {
                    _http.DefaultRequestHeaders.Clear();
                    _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                    var r = await _http.GetStringAsync(
                        $"https://api.mercadolibre.com/items?seller_sku={p.SKU}");
                    using var doc = JsonDocument.Parse(r);
                    if (doc.RootElement.TryGetProperty("results", out var results))
                    {
                        foreach (var itemId in results.EnumerateArray()
                            .Select(x => x.GetString()).Where(x => x != null))
                        {
                            await SincronizarEstoqueMLAsync(itemId!, (int)p.Stock, accessToken);
                            sucesso++;
                        }
                    }
                }
            }
            catch { falha++; }
        }

        return new SyncResultDto(sucesso, falha, DateTime.Now);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record SyncResultDto(int Sucesso, int Falha, DateTime Executado);

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