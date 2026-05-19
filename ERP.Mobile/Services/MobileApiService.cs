using Blazored.LocalStorage;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ERP.Mobile.Services;

public class MobileApiService
{
    private readonly HttpClient           _http;
    private readonly ILocalStorageService _storage;

    public MobileApiService(HttpClient http, ILocalStorageService storage)
    {
        _http    = http;
        _storage = storage;
    }

    private async Task PrepararAsync()
    {
        var token = await _storage.GetItemAsStringAsync("token");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T?> GetAsync<T>(string url)
    {
        await PrepararAsync();
        try { return await _http.GetFromJsonAsync<T>(url); }
        catch { return default; }
    }

    public async Task<bool> PostAsync<T>(string url, T data)
    {
        await PrepararAsync();
        var r = await _http.PostAsJsonAsync(url, data);
        return r.IsSuccessStatusCode;
    }
}

public class MobileAuthService
{
    private readonly HttpClient           _http;
    private readonly ILocalStorageService _storage;

    public bool    IsLoggedIn { get; private set; }
    public string? Username   { get; private set; }

    public MobileAuthService(HttpClient http, ILocalStorageService storage)
    {
        _http    = http;
        _storage = storage;
    }

    public async Task<bool> LoginAsync(string cnpj, string user, string senha)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "api/Auth/login");
        req.Headers.Add("X-Tenant-CNPJ", cnpj);
        req.Content = JsonContent.Create(new { username = user, password = senha });

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return false;

        var result = await resp.Content.ReadFromJsonAsync<LoginResp>();
        if (result == null) return false;

        await _storage.SetItemAsStringAsync("token",    result.AccessToken);
        await _storage.SetItemAsStringAsync("username", result.Usuario);
        IsLoggedIn = true;
        Username   = result.Usuario;
        return true;
    }

    private record LoginResp(string AccessToken, string Usuario, string Cargo);
}
