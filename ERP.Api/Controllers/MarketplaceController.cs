using ERP.Api.Security;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

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

    [HttpGet("config")]
    [HasPermission(Permissions.ConfigView)]
    public IActionResult GetConfig() => Ok(new
    {
        Plataformas      = new[] { "MercadoLivre", "Shopee" },
        WebhookUrlML     = $"{Request.Scheme}://{Request.Host}/api/marketplace/ml/webhook/{{tenantId}}",
        WebhookUrlShopee = $"{Request.Scheme}://{Request.Host}/api/marketplace/shopee/webhook/{{tenantId}}"
    });

    /// <summary>
    /// Webhook do Mercado Livre.
    /// Valida x-signature (HMAC-SHA256) antes de processar qualquer dado.
    /// Configura a URL no ML como: https://api.ttsoft.com.br/api/marketplace/ml/webhook/{tenantId}
    /// </summary>
    [HttpPost("ml/webhook/{tenantId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookML([FromRoute] Guid tenantId)
    {
        if (tenantId == Guid.Empty) return BadRequest("tenantId inválido na URL do webhook.");

        // ── 1. Ler body raw para validação de assinatura ──────────────────────
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody      = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        // ── 2. Validar assinatura HMAC-SHA256 ─────────────────────────────────
        var xSignature   = Request.Headers["x-signature"].FirstOrDefault();
        var clientSecret = _config["Marketplace:ML:ClientSecret"] ?? "";

        if (!WebhookSignatureValidator.ValidateML(xSignature, rawBody, clientSecret))
        {
            Log.Warning("ML webhook: assinatura inválida ou ausente. TenantId={TenantId}", tenantId);
            // Retorna 200 para o ML não retentar indefinidamente — mas não processa
            return Ok();
        }

        // ── 3. Deserializar e processar ───────────────────────────────────────
        MLWebhookDto? dto;
        try
        {
            dto = System.Text.Json.JsonSerializer.Deserialize<MLWebhookDto>(
                rawBody,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return BadRequest("Payload ML inválido.");
        }

        if (dto is null) return BadRequest();

        var token = _config["Marketplace:ML:AccessToken"] ?? "";
        var ok    = await _service.ProcessarWebhookMLAsync(dto.Topic, dto.Resource, token, tenantId);
        return ok ? Ok() : BadRequest();
    }

    [HttpPost("ml/sync-estoque")]
    [HasPermission(Permissions.ConfigView)]
    public async Task<IActionResult> SyncEstoqueML()
    {
        var token  = _config["Marketplace:ML:AccessToken"] ?? "";
        var result = await _service.SincronizarEstoqueAsync("mercadolivre", token);
        return Ok(result);
    }

    // ── Shopee ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Webhook da Shopee.
    /// Valida Authorization (SHA-256 com partner_key) antes de processar.
    /// Configura a URL na Shopee como: https://api.ttsoft.com.br/api/marketplace/shopee/webhook/{tenantId}
    /// </summary>
    [HttpPost("shopee/webhook/{tenantId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookShopee(
        [FromRoute] Guid              tenantId,
        [FromBody]  ShopeeWebhookDto  dto)
    {
        if (tenantId == Guid.Empty) return BadRequest("tenantId inválido na URL do webhook.");

        // ── Validar assinatura Shopee ─────────────────────────────────────────
        var authorization = Request.Headers["Authorization"].FirstOrDefault();
        var partnerId     = _config["Marketplace:Shopee:PartnerId"]  ?? "";
        var partnerKey    = _config["Marketplace:Shopee:PartnerKey"] ?? "";

        if (!WebhookSignatureValidator.ValidateShopee(
                authorization, Request.Path.Value ?? "", partnerId, partnerKey))
        {
            Log.Warning("Shopee webhook: assinatura inválida ou ausente. TenantId={TenantId}", tenantId);
            return Ok(); // 200 para Shopee não retentar — mas não processa
        }

        await _service.ProcessarWebhookShopeeAsync(dto, tenantId);
        return Ok();
    }

    [HttpGet("status")]
    [HasPermission(Permissions.ConfigView)]
    public IActionResult Status() => Ok(new
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

public record MLWebhookDto(string Topic, string Resource, long Sent, long Received, string? ApplicationId);
