// ── ERP.Infrastructure/Services/MercadoLivreDispatcher.cs ─────────────────────
using System.Net.Http.Headers;
using System.Text.Json;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using Serilog;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementação real do IChannelDispatcher pro Mercado Livre.
///
/// Confirmado na documentação oficial (múltiplas fontes consistentes):
///   GET https://api.mercadolibre.com/orders/search?seller={id}&order.status=paid&sort=date_desc
///   Header: Authorization: Bearer {access_token}
///
/// NÃO confirmado (documentação não deixou claro um parâmetro de filtro por
/// data direto na query): o filtro por "desde" é feito no lado de cá,
/// descartando pedidos com date_created anterior — funciona, mas busca mais
/// dado do que precisaria. Vale revisar contra o Postman/sandbox antes de ir
/// pra produção, e trocar por um filtro de servidor se existir um.
/// </summary>
public class MercadoLivreDispatcher : IChannelDispatcher
{
    private const string OrdersSearchUrl = "https://api.mercadolibre.com/orders/search";

    private readonly HttpClient                _http;
    private readonly IMercadoLivreAuthService   _auth;

    public SalesChannelType  Tipo        => SalesChannelType.MercadoLivre;
    public ChannelCapability Capacidades => ChannelCapability.RecebePedidos; // AtualizaStatus/SincronizaEstoque: ver stubs abaixo

    public MercadoLivreDispatcher(HttpClient http, IMercadoLivreAuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<(bool Sucesso, string Mensagem, IReadOnlyList<ExternalOrderDto> Pedidos)> BuscarPedidosNovosAsync(
        SalesChannel canal, DateTime desde)
    {
        var garantiu = await GarantirTokenValidoAsync(canal);
        if (!garantiu.Sucesso) return (false, garantiu.Mensagem, Array.Empty<ExternalOrderDto>());

        try
        {
            var url = $"{OrdersSearchUrl}?seller={canal.ExternalAccountId}&order.status=paid&sort=date_desc";
            using var request = NovaRequisicao(HttpMethod.Get, url, canal.AccessToken!);
            var response = await _http.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, $"Mercado Livre retornou {response.StatusCode}: {raw}", Array.Empty<ExternalOrderDto>());

            using var doc = JsonDocument.Parse(raw);
            var pedidos = new List<ExternalOrderDto>();

            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var pedidoJson in results.EnumerateArray())
                {
                    var dto = MapearPedido(pedidoJson);
                    // Filtro por data feito aqui, não na API — ver ressalva na doc da classe.
                    if (dto is not null && dto.DataPedidoExterno >= desde)
                        pedidos.Add(dto);
                }
            }

            return (true, "OK", pedidos);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao buscar pedidos do Mercado Livre pro canal {CanalId}", canal.Id);
            return (false, $"Erro inesperado: {ex.Message}", Array.Empty<ExternalOrderDto>());
        }
    }

    public async Task<(bool Sucesso, string Mensagem, ExternalOrderDto? Pedido)> BuscarPedidoPorIdAsync(
        SalesChannel canal, string externalOrderId)
    {
        var garantiu = await GarantirTokenValidoAsync(canal);
        if (!garantiu.Sucesso) return (false, garantiu.Mensagem, null);

        try
        {
            using var request = NovaRequisicao(HttpMethod.Get,
                $"https://api.mercadolibre.com/orders/{externalOrderId}", canal.AccessToken!);
            var response = await _http.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, $"Mercado Livre retornou {response.StatusCode}: {raw}", null);

            using var doc = JsonDocument.Parse(raw);
            return (true, "OK", MapearPedido(doc.RootElement));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao buscar pedido {PedidoId} do Mercado Livre", externalOrderId);
            return (false, $"Erro inesperado: {ex.Message}", null);
        }
    }

    /// <summary>
    /// NÃO IMPLEMENTADO DE PROPÓSITO: o Mercado Livre não tem um endpoint genérico
    /// de "PUT novo status no pedido" — o vendedor atualiza a ORDEM indiretamente,
    /// atualizando o ENVIO (shipment) associado a ela (endpoint de shipments,
    /// documentação própria em "Mercado Envios"). Modelar isso direito exige
    /// olhar a doc de shipments, que ainda não foi levantada — não vou inventar
    /// um contrato de API que talvez não exista.
    /// </summary>
    public Task<(bool Sucesso, string Mensagem)> AtualizarStatusPedidoAsync(
        SalesChannel canal, string externalOrderId, string novoStatusExterno)
        => throw new NotImplementedException(
            "Atualização de status do Mercado Livre acontece via endpoint de Shipments, não de Orders — precisa levantar a doc de Mercado Envios antes de implementar isso.");

    /// <summary>
    /// NÃO IMPLEMENTADO DE PROPÓSITO: sincronizar estoque no Mercado Livre exige
    /// o item_id/variation_id do anúncio (PUT /items/{item_id}), que não tem
    /// relação direta com o Product interno hoje — o SkuMapping liga
    /// SkuExterno ↔ Product, não item_id do ML especificamente. Precisa decidir
    /// se SkuExterno vira o item_id ou se é preciso um campo novo antes de implementar.
    /// </summary>
    public Task<(bool Sucesso, string Mensagem)> SincronizarEstoqueAsync(
        SalesChannel canal, IReadOnlyList<(string SkuExterno, decimal Quantidade)> estoques)
        => throw new NotImplementedException(
            "Falta decidir se SkuMapping.SkuExterno é o item_id do Mercado Livre, ou se precisa de um campo próprio, antes de implementar a sincronização de estoque.");

    /// <summary>
    /// Busca os anúncios ativos do vendedor. Dois passos, documentados
    /// separadamente pelo Mercado Livre:
    ///   1. GET /users/{seller_id}/items/search?status=active — só devolve os IDs.
    ///   2. GET /items?ids=id1,id2,... (até 20 por chamada) — os detalhes de fato.
    /// O passo 2 devolve um array de objetos com "code"/"body" (não o item
    /// direto) — formato de multiget do próprio Mercado Livre, não uma escolha nossa.
    /// </summary>
    public async Task<(bool Sucesso, string Mensagem, IReadOnlyList<AnuncioExternoDto> Anuncios)> BuscarAnunciosAsync(
        SalesChannel canal)
    {
        var garantiu = await GarantirTokenValidoAsync(canal);
        if (!garantiu.Sucesso) return (false, garantiu.Mensagem, Array.Empty<AnuncioExternoDto>());

        try
        {
            // ── Passo 1: IDs dos anúncios ativos ────────────────────────────
            var idsUrl = $"https://api.mercadolibre.com/users/{canal.ExternalAccountId}/items/search?status=active";
            using var requestIds = NovaRequisicao(HttpMethod.Get, idsUrl, canal.AccessToken!);
            var responseIds = await _http.SendAsync(requestIds);
            var rawIds = await responseIds.Content.ReadAsStringAsync();

            if (!responseIds.IsSuccessStatusCode)
                return (false, $"Mercado Livre retornou {responseIds.StatusCode}: {rawIds}", Array.Empty<AnuncioExternoDto>());

            using var docIds = JsonDocument.Parse(rawIds);
            var ids = new List<string>();
            if (docIds.RootElement.TryGetProperty("results", out var results))
                foreach (var idEl in results.EnumerateArray())
                    ids.Add(idEl.GetString() ?? "");

            if (ids.Count == 0) return (true, "OK", Array.Empty<AnuncioExternoDto>());

            // ── Passo 2: detalhes, em lotes de 20 (limite documentado do multiget) ──
            var anuncios = new List<AnuncioExternoDto>();
            foreach (var lote in ids.Chunk(20))
            {
                var itemsUrl = $"https://api.mercadolibre.com/items?ids={string.Join(",", lote)}";
                using var requestItems = NovaRequisicao(HttpMethod.Get, itemsUrl, canal.AccessToken!);
                var responseItems = await _http.SendAsync(requestItems);
                var rawItems = await responseItems.Content.ReadAsStringAsync();

                if (!responseItems.IsSuccessStatusCode)
                {
                    Log.Warning("Mercado Livre: falha ao buscar detalhes de um lote de anúncios ({Status}): {Body}",
                        responseItems.StatusCode, rawItems);
                    continue; // um lote falhar não deve derrubar os outros
                }

                using var docItems = JsonDocument.Parse(rawItems);
                foreach (var wrapper in docItems.RootElement.EnumerateArray())
                {
                    if (!wrapper.TryGetProperty("body", out var body)) continue;

                    anuncios.Add(new AnuncioExternoDto
                    {
                        ItemId     = body.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "",
                        Titulo     = body.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        SkuExterno = body.TryGetProperty("seller_custom_field", out var scf) ? scf.GetString() ?? "" : "",
                    });
                }
            }

            return (true, "OK", anuncios);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao buscar anúncios do Mercado Livre pro canal {CanalId}", canal.Id);
            return (false, $"Erro inesperado: {ex.Message}", Array.Empty<AnuncioExternoDto>());
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(bool Sucesso, string Mensagem)> GarantirTokenValidoAsync(SalesChannel canal)
    {
        if (string.IsNullOrEmpty(canal.AccessToken))
            return (false, "Canal ainda não foi autorizado (sem access_token) — falta completar o fluxo OAuth.");

        // Renova com uma folga de 5 min antes de expirar, não só quando já expirou.
        if (canal.TokenExpiraEm is null || canal.TokenExpiraEm <= DateTime.UtcNow.AddMinutes(5))
            return await _auth.RenovarTokenAsync(canal);

        return (true, "OK");
    }

    private static HttpRequestMessage NovaRequisicao(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static ExternalOrderDto? MapearPedido(JsonElement pedidoJson)
    {
        if (!pedidoJson.TryGetProperty("id", out var idProp)) return null;

        var dto = new ExternalOrderDto
        {
            ExternalOrderId   = idProp.ToString(),
            ExternalStatus    = pedidoJson.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "",
            DataPedidoExterno = pedidoJson.TryGetProperty("date_created", out var dc) &&
                                 DateTime.TryParse(dc.GetString(), out var data) ? data : DateTime.UtcNow,
            ValorTotal        = pedidoJson.TryGetProperty("total_amount", out var ta) ? ta.GetDecimal() : 0,
            RawPayloadJson    = pedidoJson.GetRawText()
        };

        if (pedidoJson.TryGetProperty("order_items", out var itens))
        {
            foreach (var item in itens.EnumerateArray())
            {
                if (!item.TryGetProperty("item", out var itemInfo)) continue;

                dto.Itens.Add(new ExternalOrderItemDto
                {
                    // seller_custom_field é o SKU que o vendedor cadastrou no anúncio —
                    // é isso que precisa bater com SkuMapping.SkuExterno.
                    SkuExterno    = itemInfo.TryGetProperty("seller_custom_field", out var scf) ? scf.GetString() ?? "" : "",
                    DescricaoItem = itemInfo.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    Quantidade    = item.TryGetProperty("quantity", out var q) ? q.GetDecimal() : 0,
                    ValorUnitario = item.TryGetProperty("unit_price", out var up) ? up.GetDecimal() : 0
                });
            }
        }

        return dto;
    }
}