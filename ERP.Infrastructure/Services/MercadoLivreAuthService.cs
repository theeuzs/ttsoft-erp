// ── ERP.Infrastructure/Services/MercadoLivreAuthService.cs ────────────────────
using System.Text.Json;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementa o fluxo OAuth Authorization Code do Mercado Livre. Endpoints e
/// formato confirmados na doc oficial (developers.mercadolivre.com.br —
/// Autenticação e Autorização): POST form-urlencoded em
/// https://api.mercadolibre.com/oauth/token, com grant_type
/// authorization_code ou refresh_token.
/// </summary>
public class MercadoLivreAuthService : IMercadoLivreAuthService
{
    private const string AuthUrlBase  = "https://auth.mercadolivre.com.br/authorization";
    private const string TokenUrl     = "https://api.mercadolibre.com/oauth/token";

    private readonly HttpClient       _http;
    private readonly IConfiguration   _config;
    private readonly IUnitOfWork      _uow;

    public MercadoLivreAuthService(HttpClient http, IConfiguration config, IUnitOfWork uow)
    {
        _http   = http;
        _config = config;
        _uow    = uow;
    }

    public string ObterUrlAutorizacao(Guid salesChannelId)
    {
        var clientId    = _config["Marketplace:ML:ClientId"];
        var redirectUri = _config["Marketplace:ML:RedirectUri"];

        // state = salesChannelId: é assim que sabemos, no callback, pra qual
        // loja esse "code" pertence — o Mercado Livre devolve esse valor de volta sem alterar.
        return $"{AuthUrlBase}?response_type=code&client_id={clientId}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri ?? "")}" +
               $"&state={salesChannelId}";
    }

    public async Task<(bool Sucesso, string Mensagem)> TrocarCodigoPorTokenAsync(string code, Guid salesChannelId)
    {
        // GetCanalPorIdSemFiltroAsync, não GetCanalByIdAsync: este método roda a partir do
        // callback [AllowAnonymous] — não existe tenant no contexto ainda pra aplicar o
        // filtro normal (ver comentário na interface do repositório).
        var canal = await _uow.OrderSync.GetCanalPorIdSemFiltroAsync(salesChannelId);
        if (canal is null)
        {
            // DIAGNÓSTICO TEMPORÁRIO — remover depois de resolver o bug do callback.
            var diagnostico = await _uow.OrderSync.DiagnosticoConexaoESalesChannelsAsync();
            Log.Warning("OAuth callback: SalesChannel {Id} não encontrado. Diagnóstico: {Diagnostico}",
                salesChannelId, diagnostico);
            return (false, $"SalesChannel {salesChannelId} não encontrado.");
        }

        var body = new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["client_id"]     = _config["Marketplace:ML:ClientId"] ?? "",
            ["client_secret"] = _config["Marketplace:ML:ClientSecret"] ?? "",
            ["code"]          = code,
            ["redirect_uri"]  = _config["Marketplace:ML:RedirectUri"] ?? ""
        };

        return await ExecutarTrocaAsync(canal, body, "autorização inicial");
    }

    public async Task<(bool Sucesso, string Mensagem)> RenovarTokenAsync(SalesChannel canal)
    {
        if (string.IsNullOrEmpty(canal.RefreshToken))
            return (false, "Canal não tem refresh_token — precisa reautorizar do zero.");

        var body = new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["client_id"]     = _config["Marketplace:ML:ClientId"] ?? "",
            ["client_secret"] = _config["Marketplace:ML:ClientSecret"] ?? "",
            ["refresh_token"] = canal.RefreshToken
        };

        return await ExecutarTrocaAsync(canal, body, "renovação de token");
    }

    private async Task<(bool Sucesso, string Mensagem)> ExecutarTrocaAsync(
        SalesChannel canal, Dictionary<string, string> body, string contexto)
    {
        try
        {
            using var content  = new FormUrlEncodedContent(body);
            content.Headers.ContentType!.MediaType = "application/x-www-form-urlencoded";
            _http.DefaultRequestHeaders.Accept.Clear();
            _http.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await _http.PostAsync(TokenUrl, content);
            var raw      = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("ML OAuth ({Contexto}) falhou: {Status} — {Body}", contexto, response.StatusCode, raw);
                return (false, $"Mercado Livre recusou a {contexto}: {raw}");
            }

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            canal.AccessToken      = root.GetProperty("access_token").GetString();
            canal.RefreshToken     = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : canal.RefreshToken;
            var expiresInSegundos  = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 21600; // 6h é o padrão documentado
            canal.TokenExpiraEm    = DateTime.UtcNow.AddSeconds(expiresInSegundos);

            // user_id só vem na troca inicial (authorization_code) — no refresh não muda, não sobrescreve.
            if (root.TryGetProperty("user_id", out var userId))
                canal.ExternalAccountId = userId.ToString();

            await _uow.OrderSync.SalvarAsync();
            return (true, $"Token do Mercado Livre atualizado com sucesso ({contexto}).");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro inesperado na {Contexto} OAuth do Mercado Livre para canal {CanalId}", contexto, canal.Id);
            return (false, $"Erro inesperado: {ex.Message}");
        }
    }
}