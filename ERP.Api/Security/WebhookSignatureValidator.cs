using System.Security.Cryptography;
using System.Text;

namespace ERP.Api.Security;

/// <summary>
/// Valida assinaturas de webhooks de marketplace.
///
/// ML  — x-signature: ts={timestamp}&amp;v1={hmac_sha256_hex}
///       HMAC_SHA256(clientSecret, ts + "." + rawBody)
///
/// Shopee — Authorization: {partnerId}|{timestamp}|{sha256_hex}
///          SHA256(partnerId + "/" + urlPath + timestamp + partnerKey)
/// </summary>
public static class WebhookSignatureValidator
{
    // ── Mercado Livre ─────────────────────────────────────────────────────────

    /// <summary>
    /// Valida o header x-signature do ML.
    /// Retorna false se o header estiver ausente, mal-formado ou com HMAC inválido.
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

        if (!parts.TryGetValue("ts",  out var ts) ||
            !parts.TryGetValue("v1",  out var v1) ||
            string.IsNullOrEmpty(ts)              ||
            string.IsNullOrEmpty(v1))
            return false;

        // Replica o cálculo do ML: HMAC_SHA256(secret, ts + "." + rawBody)
        var message = $"{ts}.{rawBody}";
        using var hmac    = new HMACSHA256(Encoding.UTF8.GetBytes(clientSecret));
        var expectedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var expected      = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        // Comparação constant-time via CryptographicOperations para evitar timing attack
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(v1.ToLowerInvariant()));
    }

    // ── Shopee ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Valida o header Authorization da Shopee.
    /// Formato esperado: {partnerId}|{timestamp}|{sha256_hex}
    /// </summary>
    public static bool ValidateShopee(
        string? authorization,
        string  requestPath,
        string  partnerId,
        string  partnerKey)
    {
        if (string.IsNullOrEmpty(authorization)) return false;

        var parts = authorization.Split('|');
        if (parts.Length != 3) return false;

        var (headerPartnerId, timestamp, signature) = (parts[0], parts[1], parts[2]);

        // PartnerId do header deve bater com o configurado
        if (headerPartnerId != partnerId) return false;

        // SHA256(partnerId + requestPath + timestamp + partnerKey)
        var message      = partnerId + requestPath + timestamp + partnerKey;
        using var sha    = SHA256.Create();
        var hashBytes    = sha.ComputeHash(Encoding.UTF8.GetBytes(message));
        var expected     = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature.ToLowerInvariant()));
    }
}
