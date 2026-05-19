using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketplaceController : ControllerBase
{
    private readonly MarketplaceService _service;
    private readonly IConfiguration     _config;

    public MarketplaceController(MarketplaceService service, IConfiguration config)
    {
        _service = service;
        _config  = config;
    }

    // ── Mercado Livre ─────────────────────────────────────────────────────────

    /// <summary>
    /// Webhook do Mercado Livre — recebe notificações de pedidos e itens.
    /// URL para configurar no ML: https://seudominio.com/api/marketplace/ml/webhook
    /// </summary>
    [HttpPost("ml/webhook")]
    [AllowAnonymous] // ML não envia JWT — autenticado pelo token interno
    public async Task<IActionResult> WebhookML(
        [FromBody] MLWebhookDto dto,
        [FromQuery] string? userId)
    {
        var token = _config["Marketplace:ML:AccessToken"] ?? "";
        var ok = await _service.ProcessarWebhookMLAsync(dto.Topic, dto.Resource, token);
        return ok ? Ok() : BadRequest();
    }

    /// <summary>Sincroniza estoque atual com o Mercado Livre.</summary>
    [HttpPost("ml/sync-estoque")]
    [Authorize]
    public async Task<IActionResult> SyncEstoqueML()
    {
        var token = _config["Marketplace:ML:AccessToken"] ?? "";
        var result = await _service.SincronizarEstoqueAsync("mercadolivre", token);
        return Ok(result);
    }

    // ── Shopee ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Webhook da Shopee — recebe notificações de pedidos.
    /// URL para configurar na Shopee: https://seudominio.com/api/marketplace/shopee/webhook
    /// </summary>
    [HttpPost("shopee/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookShopee([FromBody] ShopeeWebhookDto dto)
    {
        await _service.ProcessarWebhookShopeeAsync(dto);
        return Ok();
    }

    /// <summary>Status das integrações configuradas.</summary>
    [HttpGet("status")]
    [Authorize]
    public IActionResult Status()
    {
        return Ok(new
        {
            MercadoLivre = new
            {
                Configurado = !string.IsNullOrEmpty(_config["Marketplace:ML:AccessToken"]),
                ClientId    = _config["Marketplace:ML:ClientId"] ?? "não configurado"
            },
            Shopee = new
            {
                Configurado = !string.IsNullOrEmpty(_config["Marketplace:Shopee:PartnerId"]),
                PartnerId   = _config["Marketplace:Shopee:PartnerId"] ?? "não configurado"
            },
            Amazon = new
            {
                Configurado = !string.IsNullOrEmpty(_config["Marketplace:Amazon:SellerId"]),
                SellerId    = _config["Marketplace:Amazon:SellerId"] ?? "não configurado"
            }
        });
    }
}

public record MLWebhookDto(string Topic, string Resource, long Sent, long Received, string? ApplicationId);
