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
    /// Valida x-signature (HMAC-SHA256 + replay protection 5 min) antes de processar.
    /// 1.7.3: rejeita ts com diff > 5 min do relógio atual.
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

        // ── 2. Validar assinatura + replay protection ─────────────────────────
        var xSignature   = Request.Headers["x-signature"].FirstOrDefault();
        var clientSecret = _config["Marketplace:ML:ClientSecret"] ?? "";

        if (!WebhookSignatureValidator.ValidateML(xSignature, rawBody, clientSecret))
        {
            Log.Warning("ML webhook: assinatura inválida, ausente ou replay detectado. TenantId={TenantId}", tenantId);
            return Ok(); // Retorna 200 para ML não retentar — mas não processa
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
    /// 1.7.2: body agora é lido raw e incluído na assinatura HMAC-SHA256.
    /// Formato Authorization: {partnerId}|{timestamp_s}|{hmac_hex}
    /// Assinatura = HMAC-SHA256(partnerKey, fullUrl + "|" + rawBody)
    /// + replay protection: rejeita timestamp > 5 min.
    ///
    /// ANTES: SHA-256 puro sobre (partnerId + path + timestamp + partnerKey) SEM body.
    ///        Qualquer Authorization capturado permitia forjar qualquer body → zerar estoque.
    /// DEPOIS: body obrigatório na assinatura → replay com body diferente é rejeitado.
    /// </summary>
    [HttpPost("shopee/webhook/{tenantId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookShopee([FromRoute] Guid tenantId)
    {
        if (tenantId == Guid.Empty) return BadRequest("tenantId inválido na URL do webhook.");

        // ── 1. Ler body raw (necessário para incluir na validação da assinatura) ─
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var rawBody      = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        // ── 2. Validar assinatura Shopee com body + replay protection ──────────
        var authorization = Request.Headers["Authorization"].FirstOrDefault();
        var partnerId     = _config["Marketplace:Shopee:PartnerId"]  ?? "";
        var partnerKey    = _config["Marketplace:Shopee:PartnerKey"] ?? "";

        // fullUrl é obrigatório na mensagem assinada (scheme://host/path)
        var fullUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}";

        if (!WebhookSignatureValidator.ValidateShopee(
                authorization, fullUrl, rawBody, partnerId, partnerKey))
        {
            Log.Warning(
                "Shopee webhook: assinatura inválida, body forjado ou replay detectado. TenantId={TenantId}",
                tenantId);
            return Ok(); // 200 para Shopee não retentar — mas não processa
        }

        // ── 3. Deserializar após validação ────────────────────────────────────
        ShopeeWebhookDto? dto;
        try
        {
            dto = System.Text.Json.JsonSerializer.Deserialize<ShopeeWebhookDto>(
                rawBody,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return BadRequest("Payload Shopee inválido.");
        }

        if (dto is null) return BadRequest();

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