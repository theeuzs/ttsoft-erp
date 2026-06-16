using System.Security.Cryptography;
using System.Text;

namespace ERP.Api.Security;

/// <summary>
/// Valida assinaturas de webhooks de marketplace.
///
/// ML  — x-signature: ts={timestamp_ms}&amp;v1={hmac_sha256_hex}
///       HMAC_SHA256(clientSecret, ts + "." + rawBody)
///       + replay protection: rejeita ts com diff > 5 min do relógio atual (1.7.3)
///
/// Shopee — Authorization: {partnerId}|{timestamp_s}|{hmac_sha256_hex}
///          HMAC_SHA256(partnerKey, fullUrl + "|" + rawBody)
///          + body obrigatório na assinatura (1.7.2 — fix crítico)
///          + replay protection: rejeita timestamp com diff > 5 min (1.7.2)
/// </summary>
public static class WebhookSignatureValidator
{
    private const long MlReplayWindowMs     = 5 * 60 * 1000L; // 5 min em ms
    private const long ShopeeReplayWindowS  = 5 * 60L;        // 5 min em s

    // ── Mercado Livre ─────────────────────────────────────────────────────────

    /// <summary>
    /// Valida o header x-signature do ML.
    /// Retorna false se: header ausente/mal-formado, HMAC inválido, ou timestamp > 5 min.
    ///
    /// 1.7.3 — Replay protection: o ML documenta validade de 5 min por webhook.
    /// Sem essa checagem, um par (ts, v1, body) capturado uma vez é válido para sempre.
    /// </summary>
    public static bool ValidateML(string? xSignature, string rawBody, string clientSecret)
    {
        if (string.IsNullOrEmpty(xSignature) || string.IsNullOrEmpty(clientSecret))
            return false;

        // Parseia "ts=1234567890&v1=abcdef..."
        var parts = xSignature
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());

        if (!parts.TryGetValue("ts", out var ts) ||
            !parts.TryGetValue("v1", out var v1) ||
            string.IsNullOrEmpty(ts)             ||
            string.IsNullOrEmpty(v1))
            return false;

        // ── 1.7.3: replay protection ─────────────────────────────────────────
        // ML envia ts em milissegundos (Unix epoch ms).
        if (!long.TryParse(ts, out var tsMs)) return false;
        var diffMs = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - tsMs);
        if (diffMs > MlReplayWindowMs) return false;

        // ── HMAC-SHA256 ──────────────────────────────────────────────────────
        // Replica o cálculo do ML: HMAC_SHA256(secret, ts + "." + rawBody)
        var message = $"{ts}.{rawBody}";
        using var hmac    = new HMACSHA256(Encoding.UTF8.GetBytes(clientSecret));
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var expected      = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        // Comparação constant-time para evitar timing attack
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(v1.ToLowerInvariant()));
    }

    // ── Shopee ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Valida o header Authorization da Shopee.
    /// Formato esperado: {partnerId}|{timestamp_s}|{hmac_sha256_hex}
    ///
    /// 1.7.2 — Fix crítico: o body agora é incluído na assinatura.
    /// Assinatura = HMAC-SHA256(partnerKey, fullUrl + "|" + rawBody)
    ///
    /// Antes dessa correção: o body podia ser completamente forjado com um header
    /// capturado de qualquer request legítimo anterior (replay + body forgery).
    ///
    /// 1.7.2 — Replay protection: rejeita timestamps com diff > 5 min.
    /// </summary>
    public static bool ValidateShopee(
        string? authorization,
        string  fullUrl,    // scheme://host/path — ex: "https://api.ttsoft.com.br/api/marketplace/shopee/webhook/{id}"
        string  rawBody,    // body bruto da requisição — OBRIGATÓRIO na assinatura
        string  partnerId,  // de config Marketplace:Shopee:PartnerId
        string  partnerKey) // de config Marketplace:Shopee:PartnerKey
    {
        if (string.IsNullOrEmpty(authorization)) return false;

        var parts = authorization.Split('|');
        if (parts.Length != 3) return false;

        var (headerPartnerId, timestamp, signature) = (parts[0], parts[1], parts[2]);

        // PartnerId do header deve bater com o configurado
        if (headerPartnerId != partnerId) return false;

        // ── Replay protection ─────────────────────────────────────────────────
        // Shopee envia timestamp em segundos (Unix epoch s).
        if (!long.TryParse(timestamp, out var tsSeconds)) return false;
        var diffS = Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - tsSeconds);
        if (diffS > ShopeeReplayWindowS) return false;

        // ── HMAC-SHA256 com body ──────────────────────────────────────────────
        // Shopee Push Notification: HMAC-SHA256(partnerKey, fullUrl + "|" + rawBody)
        // O body PRECISA estar na mensagem assinada — sem ele, qualquer body
        // pode ser forjado reutilizando um Authorization header capturado.
        var message      = fullUrl + "|" + rawBody;
        using var hmac   = new HMACSHA256(Encoding.UTF8.GetBytes(partnerKey));
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var expected      = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature.ToLowerInvariant()));
    }
}