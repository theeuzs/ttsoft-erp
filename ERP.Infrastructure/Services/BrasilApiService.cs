using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace ERP.Infrastructure.Services;

public class BrasilApiCnpjResponse
{
    [JsonPropertyName("cnpj")]
    public string Cnpj { get; set; } = string.Empty;
    [JsonPropertyName("razao_social")]
    public string RazaoSocial { get; set; } = string.Empty;
    [JsonPropertyName("nome_fantasia")]
    public string? NomeFantasia { get; set; }
    [JsonPropertyName("descricao_situacao_cadastral")]
    public string DescricaoSituacaoCadastral { get; set; } = string.Empty;
    [JsonPropertyName("municipio")]
    public string? Municipio { get; set; }
    [JsonPropertyName("uf")]
    public string? Uf { get; set; }
    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

/// <summary>
/// S11: Integracao com BrasilAPI para validar CNPJ contra a Receita Federal.
/// S13: Circuit breaker — apos 5 falhas consecutivas, entra em modo fail-safe
///      pessimista (cria admin inativo) em vez de fail-open silencioso.
///
/// Circuit breaker:
///   FECHADO:    consulta normalmente.
///   ABERTO:     apos ThresholdFalhas falhas, para por JanelaCircuito minutos.
///               Retorna null + CircuitAberto=true — CadastroService trata como
///               divergencia (admin inativo, token de confirmacao gerado).
///   SEMI-ABERTO: apos janela, testa uma requisicao. OK fecha o circuit.
/// </summary>
public class BrasilApiService
{
    private readonly HttpClient _http;

    // Circuit breaker state (static = process-level, singleton pattern)
    private static int      _falhasConsecutivas = 0;
    private static DateTime _circuitAbertoAte   = DateTime.MinValue;
    private const  int      ThresholdFalhas     = 5;
    private static readonly TimeSpan JanelaCircuito = TimeSpan.FromMinutes(10);

    private const string BaseUrl = "https://brasilapi.com.br/api/cnpj/v1/";

    public BrasilApiService(HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.Add("User-Agent", "TTSoft-ERP/1.0");
    }

    /// <summary>True quando circuit breaker esta aberto (fail-safe mode).</summary>
    public bool CircuitAberto { get; private set; }

    public async Task<BrasilApiCnpjResponse?> ConsultarCnpjAsync(string cnpjLimpo)
    {
        // Circuit aberto — fail-safe pessimista
        if (DateTime.UtcNow < _circuitAbertoAte)
        {
            Log.Warning("BrasilAPI circuit ABERTO ate {Ate} — CNPJ {Cnpj} tratado como fail-safe (admin inativo)",
                _circuitAbertoAte, cnpjLimpo);
            CircuitAberto = true;
            return null;
        }

        if (_circuitAbertoAte != DateTime.MinValue)
            Log.Information("BrasilAPI circuit SEMI-ABERTO — testando para CNPJ {Cnpj}", cnpjLimpo);

        try
        {
            var response = await _http.GetAsync($"{BaseUrl}{cnpjLimpo}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // CNPJ nao encontrado e resposta valida — nao conta como falha
                _falhasConsecutivas = 0;
                CircuitAberto = false;
                return null;
            }

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"BrasilAPI retornou {response.StatusCode}");

            var result = await response.Content.ReadFromJsonAsync<BrasilApiCnpjResponse>();

            // Sucesso — fecha o circuit
            _falhasConsecutivas = 0;
            _circuitAbertoAte   = DateTime.MinValue;
            CircuitAberto       = false;
            return result;
        }
        catch (Exception ex)
        {
            _falhasConsecutivas++;

            if (_falhasConsecutivas >= ThresholdFalhas)
            {
                _circuitAbertoAte = DateTime.UtcNow.Add(JanelaCircuito);
                Log.Warning(ex,
                    "BrasilAPI circuit ABERTO por {Min} min apos {N} falhas. " +
                    "Cadastros serao tratados como fail-safe (admin inativo).",
                    JanelaCircuito.TotalMinutes, _falhasConsecutivas);
                CircuitAberto = true;
            }
            else
            {
                Log.Warning(ex,
                    "BrasilAPI falhou ({N}/{Threshold}) para CNPJ {Cnpj} — fail-open por ora",
                    _falhasConsecutivas, ThresholdFalhas, cnpjLimpo);
                CircuitAberto = false;
            }

            return null;
        }
    }
}