using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace ERP.Infrastructure.Services;

/// <summary>
/// DTO da resposta da BrasilAPI para /api/cnpj/v1/{cnpj}.
/// Documentação: https://brasilapi.com.br/docs#tag/CNPJ
/// </summary>
public class BrasilApiCnpjResponse
{
    [JsonPropertyName("cnpj")]
    public string Cnpj { get; set; } = string.Empty;

    [JsonPropertyName("razao_social")]
    public string RazaoSocial { get; set; } = string.Empty;

    [JsonPropertyName("nome_fantasia")]
    public string? NomeFantasia { get; set; }

    /// <summary>"ATIVA", "SUSPENSA", "INAPTA", "BAIXADA" etc.</summary>
    [JsonPropertyName("descricao_situacao_cadastral")]
    public string DescricaoSituacaoCadastral { get; set; } = string.Empty;

    [JsonPropertyName("municipio")]
    public string? Municipio { get; set; }

    [JsonPropertyName("uf")]
    public string? Uf { get; set; }

    /// <summary>E-mail registrado na Receita Federal (pode ser nulo ou vazio).</summary>
    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

/// <summary>
/// S11 — Integração com BrasilAPI para validar CNPJ contra a Receita Federal.
/// Nível 1: rejeita CNPJs inativos (situacao != ATIVA).
/// Nível 2: cruza e-mail informado com e-mail da RFB (aviso, não bloqueio).
/// Fail-open: se a BrasilAPI estiver indisponível, o cadastro prossegue (disponibilidade > segurança neste caso).
/// </summary>
public class BrasilApiService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://brasilapi.com.br/api/cnpj/v1/";

    public BrasilApiService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.Add("User-Agent", "TTSoft-ERP/1.0");
    }

    /// <summary>
    /// Consulta dados do CNPJ na Receita Federal via BrasilAPI.
    /// Retorna null se a API estiver indisponível (fail-open).
    /// </summary>
    public async Task<BrasilApiCnpjResponse?> ConsultarCnpjAsync(string cnpjLimpo)
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}{cnpjLimpo}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null; // CNPJ não encontrado na RFB

            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("BrasilAPI retornou {Status} para CNPJ {Cnpj}", response.StatusCode, cnpjLimpo);
                return null; // fail-open
            }

            return await response.Content.ReadFromJsonAsync<BrasilApiCnpjResponse>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "BrasilAPI indisponível para CNPJ {Cnpj} — prosseguindo sem validação RFB", cnpjLimpo);
            return null; // fail-open: não bloqueia o cadastro se a API estiver down
        }
    }
}
