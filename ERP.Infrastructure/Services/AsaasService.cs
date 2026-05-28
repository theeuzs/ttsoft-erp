// ── ERP.Infrastructure/Services/AsaasService.cs ───────────────────────────────
// Integração com Asaas para geração de boleto bancário.
// Docs: https://docs.asaas.com/reference/criar-nova-cobrança
// Configurar no Azure App Service: Asaas__ApiKey e Asaas__Environment (sandbox/production)
// ─────────────────────────────────────────────────────────────────────────────
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace ERP.Infrastructure.Services;

public class AsaasService
{
    private readonly HttpClient    _http;
    private readonly string        _apiKey;
    private readonly string        _baseUrl;

    public AsaasService(HttpClient http, IConfiguration config)
    {
        _http    = http;
        _apiKey  = config["Asaas:ApiKey"] ?? "";
        var env  = config["Asaas:Environment"] ?? "sandbox";
        _baseUrl = env == "production"
            ? "https://api.asaas.com/v3"
            : "https://sandbox.asaas.com/api/v3";
    }

    // ── Encontra ou cria cliente no Asaas pelo CPF/CNPJ ──────────────────────
    public async Task<string?> ObterOuCriarClienteAsync(
        string nome, string cpfCnpj, string? email = null, string? telefone = null)
    {
        try
        {
            // Busca por CPF/CNPJ
            var cpfLimpo = new string(cpfCnpj.Where(char.IsDigit).ToArray());
            var resp = await GetAsync($"/customers?cpfCnpj={cpfLimpo}");
            if (resp.HasValue)
            {
                var total = resp.Value.GetProperty("totalCount").GetInt32();
                if (total > 0)
                    return resp.Value.GetProperty("data")[0].GetProperty("id").GetString();
            }

            // Cria cliente
            var payload = new Dictionary<string, object?> {
                ["name"]    = nome,
                ["cpfCnpj"] = cpfLimpo,
                ["email"]   = email,
                ["phone"]   = telefone
            };
            var criado = await PostAsync("/customers", payload);
            return criado.HasValue ? criado.Value.GetProperty("id").GetString() : null;
        }
        catch (Exception ex)
        {
            Log.Warning("Asaas: erro ao criar cliente {Nome}: {Msg}", nome, ex.Message);
            return null;
        }
    }

    // ── Gera boleto bancário ──────────────────────────────────────────────────
    public async Task<AsaasBoletoResult?> GerarBoletoAsync(
        string  asaasCustomerId,
        decimal valor,
        DateTime vencimento,
        string  descricao)
    {
        try
        {
            var payload = new Dictionary<string, object> {
                ["customer"]      = asaasCustomerId,
                ["billingType"]   = "BOLETO",
                ["value"]         = valor,
                ["dueDate"]       = vencimento.ToString("yyyy-MM-dd"),
                ["description"]   = descricao,
                ["externalReference"] = Guid.NewGuid().ToString("N")[..12]
            };

            var resp = await PostAsync("/payments", payload);
            if (resp is null) return null;

            return new AsaasBoletoResult
            {
                AsaasPaymentId = resp.Value.GetProperty("id").GetString() ?? "",
                BoletoUrl      = resp.Value.TryGetProperty("bankSlipUrl",  out var u) ? u.GetString() : null,
                InvoiceUrl     = resp.Value.TryGetProperty("invoiceUrl",   out var i) ? i.GetString() : null,
                BoletoBarCode  = resp.Value.TryGetProperty("nossoNumero",  out var b) ? b.GetString() : null,
                Status         = resp.Value.TryGetProperty("status",       out var s) ? s.GetString() : "PENDING"
            };
        }
        catch (Exception ex)
        {
            Log.Error("Asaas: erro ao gerar boleto — {Msg}", ex.Message);
            return null;
        }
    }

    // ── Helpers HTTP ──────────────────────────────────────────────────────────
    private async Task<JsonElement?> GetAsync(string path)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, _baseUrl + path);
        req.Headers.Add("access_token", _apiKey);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    private async Task<JsonElement?> PostAsync(string path, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("access_token", _apiKey);
        var resp = await _http.SendAsync(req);
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            Log.Warning("Asaas {Path} {Status}: {Body}", path, resp.StatusCode, body);
            return null;
        }
        return JsonDocument.Parse(body).RootElement;
    }
}

public class AsaasBoletoResult
{
    public string  AsaasPaymentId { get; set; } = "";
    public string? BoletoUrl      { get; set; }
    public string? InvoiceUrl     { get; set; }
    public string? BoletoBarCode  { get; set; }
    public string? Status         { get; set; }
}