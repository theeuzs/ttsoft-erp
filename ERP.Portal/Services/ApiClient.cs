using Blazored.LocalStorage;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ERP.Portal.Services;

public class ApiClient
{
    private readonly HttpClient        _http;
    private readonly ILocalStorageService _storage;

    public ApiClient(HttpClient http, ILocalStorageService storage)
    {
        _http    = http;
        _storage = storage;
    }

    private async Task PrepararHeaderAsync()
    {
        var token = await _storage.GetItemAsStringAsync("erp_token");
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        await PrepararHeaderAsync();
        try { return await _http.GetFromJsonAsync<T>(url); }
        catch { return default; }
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string url, T data)
    {
        await PrepararHeaderAsync();
        return await _http.PostAsJsonAsync(url, data);
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string url, T data)
    {
        await PrepararHeaderAsync();
        return await _http.PutAsJsonAsync(url, data);
    }

    public async Task<HttpResponseMessage> DeleteAsync(string url)
    {
        await PrepararHeaderAsync();
        return await _http.DeleteAsync(url);
    }

    /// <summary>BaseAddress do HttpClient — usada para construir URLs absolutas (ex: romaneio HTML).</summary>
    public string GetBaseUrl() => _http.BaseAddress?.ToString() ?? "/";
}

public class AuthService
{
    private readonly HttpClient           _http;
    private readonly ILocalStorageService _storage;

    public string? Token     { get; private set; }
    public string? Usuario   { get; private set; }
    public string? Cargo     { get; private set; }
    public string? TenantId  { get; private set; }
    public bool    LoggedIn  => !string.IsNullOrEmpty(Token);

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

        await _storage.SetItemAsStringAsync("erp_token",   Token);
        await _storage.SetItemAsStringAsync("erp_usuario", Usuario);
        // Extrai TenantId do JWT para uso no chat SignalR
        try
        {
            var parts  = result.AccessToken.Split('.');
            if (parts.Length >= 2)
            {
                var payload = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=')));
                var doc = System.Text.Json.JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("tenant_id", out var tid))
                    TenantId = tid.GetString();
            }
        }
        catch { TenantId = Usuario; } // fallback: usa nome como ID
        await _storage.SetItemAsStringAsync("erp_cargo",   Cargo);
        return true;
    }

    public async Task LogoutAsync()
    {
        Token = null; Usuario = null; Cargo = null;
        await _storage.RemoveItemAsync("erp_token");
        await _storage.RemoveItemAsync("erp_usuario");
        await _storage.RemoveItemAsync("erp_cargo");
    }

    public async Task CarregarSessaoAsync()
    {
        Token   = await _storage.GetItemAsStringAsync("erp_token");
        Usuario  = await _storage.GetItemAsStringAsync("erp_usuario");
        TenantId = Usuario; // simplificado: restaurar do storage se necessário
        Cargo   = await _storage.GetItemAsStringAsync("erp_cargo");
    }

    private record LoginResponse(string AccessToken, int ExpiresIn, string Usuario, string Cargo);
}
