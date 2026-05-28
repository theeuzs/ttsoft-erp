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
    /// Retorna a configuração atual dos marketplaces habilitados para a loja.
    /// </summary>
    [HttpGet("config")]
    [Authorize]
    public IActionResult GetConfig()
    {
        return Ok(new
        {
            Plataformas = new[] { "MercadoLivre", "Shopee" },
            WebhookUrlML     = $"{Request.Scheme}://{Request.Host}/api/marketplace/ml/webhook/{{tenantId}}",
            WebhookUrlShopee = $"{Request.Scheme}://{Request.Host}/api/marketplace/shopee/webhook/{{tenantId}}"
        });
    }

    /// <summary>
    /// Webhook do Mercado Livre — recebe notificações de pedidos e itens.
    /// Fase 1.5: tenantId na URL identifica a loja. Cada loja configura sua própria URL:
    ///   https://api.ttsoft.com.br/api/marketplace/ml/webhook/{tenantId}
    /// Isso permite multi-tenant sem JWT (ML não envia token do ERP).
    /// </summary>
    [HttpPost("ml/webhook/{tenantId:guid}")]
    [AllowAnonymous] // ML não envia JWT — tenant identificado pela URL
    public async Task<IActionResult> WebhookML(
        [FromRoute] Guid tenantId,
        [FromBody]  MLWebhookDto dto)
    {
        if (tenantId == Guid.Empty) return BadRequest("tenantId inválido na URL do webhook.");
        var token = _config["Marketplace:ML:AccessToken"] ?? "";
        var ok = await _service.ProcessarWebhookMLAsync(dto.Topic, dto.Resource, token, tenantId);
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
    /// Fase 1.5: tenantId na URL identifica a loja. Configurar na Shopee:
    ///   https://api.ttsoft.com.br/api/marketplace/shopee/webhook/{tenantId}
    /// </summary>
    [HttpPost("shopee/webhook/{tenantId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookShopee(
        [FromRoute] Guid tenantId,
        [FromBody]  ShopeeWebhookDto dto)
    {
        if (tenantId == Guid.Empty) return BadRequest("tenantId inválido na URL do webhook.");
        await _service.ProcessarWebhookShopeeAsync(dto, tenantId);
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