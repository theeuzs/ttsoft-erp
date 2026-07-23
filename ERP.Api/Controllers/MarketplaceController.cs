using ERP.Api.Security;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MarketplaceController : ControllerBase
{
    private readonly MarketplaceService       _service;   // Shopee (ver nota na classe) — ML não usa mais isso
    private readonly IConfiguration           _config;
    private readonly IUnitOfWork              _uow;
    private readonly IOrderProcessingService  _orderProcessing;
    private readonly IMercadoLivreAuthService _mlAuth;
    private readonly IRequestTenant           _tenant;

    public MarketplaceController(
        MarketplaceService service, IConfiguration config, IUnitOfWork uow,
        IOrderProcessingService orderProcessing, IMercadoLivreAuthService mlAuth, IRequestTenant tenant)
    {
        _service         = service;
        _config          = config;
        _uow             = uow;
        _orderProcessing = orderProcessing;
        _mlAuth          = mlAuth;
        _tenant          = tenant;
    }

    // ── Mercado Livre ─────────────────────────────────────────────────────────

    [HttpGet("config")]
    [HasPermission(Permissions.ConfigView)]
    public IActionResult GetConfig() => Ok(new
    {
        Plataformas      = new[] { "MercadoLivre", "Shopee" },
        // URL única pro app inteiro — o Mercado Livre não permite uma por loja.
        // O tenant é resolvido pelo user_id que vem dentro do payload, não pela URL.
        WebhookUrlML     = $"{Request.Scheme}://{Request.Host}/api/marketplace/ml/webhook",
        WebhookUrlShopee = $"{Request.Scheme}://{Request.Host}/api/marketplace/shopee/webhook/{{tenantId}}"
    });

    /// <summary>Redireciona o dono da loja pra tela de autorização do Mercado Livre.</summary>
    [HttpGet("ml/autorizar/{salesChannelId:guid}")]
    [HasPermission(Permissions.ConfigView)]
    public IActionResult AutorizarML([FromRoute] Guid salesChannelId)
        => Redirect(_mlAuth.ObterUrlAutorizacao(salesChannelId));

    /// <summary>
    /// Callback do OAuth — o Mercado Livre redireciona o navegador pra cá depois
    /// que o vendedor autoriza. Precisa ser [AllowAnonymous]: quem chega aqui é o
    /// navegador do vendedor vindo do domínio do Mercado Livre, sem o nosso JWT.
    /// </summary>
    [HttpGet("ml/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> CallbackML([FromQuery] string? code, [FromQuery] Guid? state)
    {
        if (string.IsNullOrEmpty(code) || state is null || state == Guid.Empty)
            return BadRequest("Callback do Mercado Livre sem 'code' ou 'state' — autorização não pôde ser concluída.");

        var (sucesso, mensagem) = await _mlAuth.TrocarCodigoPorTokenAsync(code, state.Value);
        return sucesso ? Ok(new { mensagem }) : BadRequest(new { mensagem });
    }

    /// <summary>
    /// Webhook do Mercado Livre — URL ÚNICA pro app inteiro (não por loja/tenant).
    /// Valida x-signature (HMAC-SHA256 + replay protection 5 min) e SÓ DEPOIS
    /// disso resolve qual loja é, pelo user_id que vem dentro do payload —
    /// nunca por um segmento de URL (isso não existe na notificação real do ML).
    /// </summary>
    [HttpPost("ml/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> WebhookML()
    {
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
            // DIAGNÓSTICO TEMPORÁRIO — remover depois de confirmar o formato real do ML.
            var todosHeaders = string.Join(" | ", Request.Headers.Select(h => $"{h.Key}={h.Value}"));
            Log.Warning(
                "ML webhook: assinatura inválida, ausente ou replay detectado. " +
                "x-signature={XSig} BodyLen={Len} BodyPreview={Preview} TodosHeaders=({Headers})",
                xSignature ?? "(ausente)", rawBody.Length,
                rawBody.Length > 200 ? rawBody[..200] : rawBody, todosHeaders);
            return Ok(); // Retorna 200 para ML não retentar — mas não processa
        }

        // ── 3. Deserializar (só depois da assinatura validada) ────────────────
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

        if (dto is null || dto.Topic != "orders") return Ok(); // outros tópicos: ignorados por enquanto

        // ── 4. Resolver a loja pelo user_id — SÓ APÓS a assinatura validada ────
        var canal = await _uow.OrderSync.GetCanalPorContaExternaAsync(SalesChannelType.MercadoLivre, dto.UserId.ToString());
        if (canal is null)
        {
            Log.Warning("ML webhook: user_id {UserId} não corresponde a nenhum SalesChannel cadastrado.", dto.UserId);
            return Ok(); // 200 pro ML não retentar — não é erro dele, é loja não conectada aqui
        }

        _tenant.TenantId = canal.TenantId; // necessário pro HasQueryFilter funcionar no resto do request

        // dto.Resource vem como "/orders/2195828494" — o id é o último segmento.
        var externalOrderId = dto.Resource.TrimEnd('/').Split('/').LastOrDefault();
        if (string.IsNullOrEmpty(externalOrderId)) return BadRequest("Resource do webhook sem id de pedido reconhecível.");

        try
        {
            await _orderProcessing.ProcessarPedidoIndividualAsync(canal.Id, externalOrderId);
            return Ok();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao processar pedido {PedidoId} vindo do webhook ML (canal {CanalId})", externalOrderId, canal.Id);
            return Ok(); // 200 mesmo em erro — o erro já ficou registrado em OrderAction/OrderConflict; retry do ML não ajudaria aqui
        }
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
    public async Task<IActionResult> Status()
    {
        var canaisML = await _uow.OrderSync.GetCanaisAtivosAsync();
        return Ok(new
        {
            MercadoLivre = new
            {
                // "Configurado" aqui é só o client_id/secret do app (parceiro) — não é
                // mais um único token global; cada SalesChannel tem o seu próprio, via OAuth.
                AppConfigurado = !string.IsNullOrEmpty(_config["Marketplace:ML:ClientId"]),
                ClientId       = _config["Marketplace:ML:ClientId"] ?? "não configurado",
                LojasConectadas = canaisML.Count(c => c.Tipo == SalesChannelType.MercadoLivre && !string.IsNullOrEmpty(c.AccessToken))
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

/// <summary>
/// UserId incluído — é a chave pra resolver qual SalesChannel esse webhook pertence.
/// [JsonPropertyName] explícito é necessário: o payload real do ML vem em snake_case
/// (user_id, application_id) — PropertyNameCaseInsensitive sozinho NÃO resolve isso,
/// só ignora maiúscula/minúscula, não a diferença de "_id" pra "Id".
/// UserId é long, não string: o Mercado Livre manda como número JSON sem aspas
/// (ex: "user_id": 8035443) — desserializar direto num campo string estouraria exceção.
/// </summary>
public record MLWebhookDto(
    string Topic,
    string Resource,
    long Sent,
    long Received,
    [property: System.Text.Json.Serialization.JsonPropertyName("application_id")] string? ApplicationId,
    [property: System.Text.Json.Serialization.JsonPropertyName("user_id")] long UserId);