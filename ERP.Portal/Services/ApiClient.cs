using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ERP.Portal.Services;

public class ApiClient
{
    private readonly HttpClient           _http;
    private readonly ILocalStorageService _storage;
    private readonly NavigationManager    _nav;

    public ApiClient(HttpClient http, ILocalStorageService storage, NavigationManager nav)
    {
        _http    = http;
        _storage = storage;
        _nav     = nav;
    }

    private async Task PrepararHeaderAsync()
    {
        var token = await _storage.GetItemAsStringAsync("erp_token");
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// 2.4 — Detecta a resposta 403 do MustChangePasswordMiddleware (API) e força
    /// redirect para /trocar-senha mesmo se o usuário navegou direto para uma página
    /// sem passar pela tela de Login (ex: favorito, link salvo, aba recuperada).
    /// </summary>
    private async Task<bool> TratarMustChangePasswordAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != System.Net.HttpStatusCode.Forbidden)
            return false;

        try
        {
            var body = await response.Content.ReadAsStringAsync();
            if (body.Contains("mustChangePassword", StringComparison.OrdinalIgnoreCase))
            {
                _nav.NavigateTo("/trocar-senha");
                return true;
            }
        }
        catch { /* corpo vazio ou não-JSON — ignora, não é o caso que tratamos aqui */ }

        return false;
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        await PrepararHeaderAsync();
        try
        {
            var response = await _http.GetAsync(url);
            if (await TratarMustChangePasswordAsync(response)) return default;
            if (!response.IsSuccessStatusCode) return default;
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch { return default; }
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string url, T data)
    {
        await PrepararHeaderAsync();
        var response = await _http.PostAsJsonAsync(url, data);
        await TratarMustChangePasswordAsync(response);
        return response;
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string url, T data)
    {
        await PrepararHeaderAsync();
        var response = await _http.PutAsJsonAsync(url, data);
        await TratarMustChangePasswordAsync(response);
        return response;
    }

    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        await PrepararHeaderAsync();
        var response = await _http.DeleteAsync(url);
        await TratarMustChangePasswordAsync(response);
        return response;
    }

    /// <summary>BaseAddress do HttpClient — usada para construir URLs absolutas (ex: romaneio HTML).</summary>
    public string GetBaseUrl() => _http.BaseAddress?.ToString() ?? "/";

    /// <summary>HttpClient exposto para operações multipart (upload de arquivo CSV).</summary>
    public HttpClient HttpClient => _http;
}

public class AuthService
{
    private readonly HttpClient           _http;
    private readonly ILocalStorageService _storage;

    public string? Token              { get; private set; }
    public string? Usuario            { get; private set; }
    public string? Cargo              { get; private set; }
    public string? TenantId           { get; private set; }
    /// <summary>2.4 — true quando o JWT exige troca de senha antes de qualquer outra ação.</summary>
    public bool    MustChangePassword { get; private set; }
    public bool    LoggedIn           => !string.IsNullOrEmpty(Token);

    public AuthService(HttpClient http, ILocalStorageService storage)
    {
        _http    = http;
        _storage = storage;
    }

    public async Task<bool> LoginAsync(string cnpj, string usuario, string senha)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/Auth/login");
        request.Headers.Add("X-Tenant-CNPJ", cnpj);
        request.Content = JsonContent.Create(new { username = usuario, password = senha });

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return false;

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (result == null) return false;

        Token   = result.AccessToken;
        Usuario = result.Usuario;
        Cargo   = result.Cargo;

        // 2.6 — TenantId e MustChangePassword vêm direto do claim do JWT, não do
        // campo solto da resposta. Isso garante que CarregarSessaoAsync (após F5)
        // reconstrua exatamente o mesmo estado, já que ambos usam ExtrairClaims().
        var (tenantId, mustChange) = ExtrairClaims(Token);
        TenantId           = tenantId ?? Usuario; // fallback se o claim não existir
        MustChangePassword = mustChange || result.MustChangePassword;

        await _storage.SetItemAsStringAsync("erp_token",   Token);
        await _storage.SetItemAsStringAsync("erp_usuario", Usuario);
        await _storage.SetItemAsStringAsync("erp_cargo",   Cargo);
        return true;
    }

    public async Task LogoutAsync()
    {
        Token = null; Usuario = null; Cargo = null; TenantId = null; MustChangePassword = false;
        await _storage.RemoveItemAsync("erp_token");
        await _storage.RemoveItemAsync("erp_usuario");
        await _storage.RemoveItemAsync("erp_cargo");
    }

    public async Task CarregarSessaoAsync()
    {
        Token = await _storage.GetItemAsStringAsync("erp_token");
        if (string.IsNullOrEmpty(Token)) return;

        Usuario = await _storage.GetItemAsStringAsync("erp_usuario");
        Cargo   = await _storage.GetItemAsStringAsync("erp_cargo");

        // 2.6 — FIX: antes era "TenantId = Usuario" (string do nome de usuário em vez
        // do GUID do tenant), o que quebrava o Chat/SignalR após F5 ou abrir aba nova.
        // Agora reextrai do JWT, igual ao login — mesma fonte de verdade.
        var (tenantId, mustChange) = ExtrairClaims(Token);
        TenantId           = tenantId ?? Usuario;
        MustChangePassword = mustChange;
    }

    /// <summary>
    /// Decodifica o payload do JWT (sem validar assinatura — só leitura local de claims
    /// já confiáveis, pois vieram da própria API) e extrai tenant_id e must_change_password.
    /// </summary>
    private static (string? TenantId, bool MustChangePassword) ExtrairClaims(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2) return (null, false);

            var payload = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(PadBase64(parts[1])));
            var doc = System.Text.Json.JsonDocument.Parse(payload);

            string? tenantId = doc.RootElement.TryGetProperty("tenant_id", out var tid)
                ? tid.GetString() : null;

            bool mustChange = doc.RootElement.TryGetProperty("must_change_password", out var mcp)
                && mcp.GetString() == "true";

            return (tenantId, mustChange);
        }
        catch
        {
            return (null, false);
        }
    }

    private static string PadBase64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return s.PadRight(s.Length + (4 - s.Length % 4) % 4, '=');
    }

    private record LoginResponse(
        string AccessToken,
        int    ExpiresIn,
        string Usuario,
        string Cargo,
        bool   MustChangePassword); // 2.4 — campo que faltava
}