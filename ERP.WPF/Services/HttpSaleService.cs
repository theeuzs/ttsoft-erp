// ERP.WPF/Services/HttpSaleService.cs
using ERP.Application.DTOs;
using ERP.Application.Exceptions;
using ERP.Application.Interfaces;
using ERP.WPF.State;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.WPF.Services;

/// <summary>
/// Fase B da migração WPF→API (08/2026) — implementação HTTP de ISaleService.
/// REGISTRADA no DI desde 13/08 (App.xaml.cs: AddScoped&lt;ISaleService,
/// HttpSaleService&gt;) — é essa classe que roda em produção pro PDV inteiro
/// e pro SyncEngineService (mesma dependência injetada nos dois).
///
/// Objetivo explícito: HttpSaleService precisa ser SUBSTITUÍVEL pelo
/// SaleService local sem mudar o comportamento percebido pelo resto do WPF —
/// não é "fazer uma chamada HTTP", é reproduzir fielmente cada exceção, cada
/// caso de retorno nulo, cada código de status que o SaleService local já
/// tem hoje (comparado método a método contra ERP.Application/Services/
/// SaleService.cs e ERP.Api/Controllers/SalesController.cs antes de escrever
/// isto, não só copiado da assinatura da interface).
///
/// Não trata falha de rede/timeout aqui dentro — HttpRequestException e
/// TaskCanceledException propagam SEM catch, de propósito: é o
/// ConnectivityExceptionClassifier (na camada de cima, no
/// FinalizarVendaViewModel/SyncEngineService) quem decide se isso vira modo
/// offline. Se HttpSaleService engolisse essas exceções aqui, o classificador
/// nunca as veria.
/// </summary>
public class HttpSaleService : ISaleService
{
    private static readonly JsonSerializerOptions JsonOpcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // Revisão cruzada com GPT (08/2026) — parâmetro opcional só pra
    // testabilidade. Em produção ninguém passa nada (DI resolve com o
    // construtor sem argumento), comportamento idêntico ao de antes dessa
    // mudança. Testes passam o handler de um WebApplicationFactory real,
    // sem precisar de biblioteca de mock HTTP nem de IHttpClientFactory.
    private readonly HttpMessageHandler? _handler;

    public HttpSaleService(HttpMessageHandler? handler = null) => _handler = handler;

    private HttpClient CriarHttpClient()
    {
        // disposeHandler: false — o handler de teste é compartilhado pelo
        // WebApplicationFactory inteiro; descartar aqui quebraria os
        // próximos testes que ainda vão usá-lo. O HttpClient em si continua
        // descartável normalmente (each using var http = ... já libera ele).
        var http = _handler != null
            ? new HttpClient(_handler, disposeHandler: false)
            : new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(30);
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AppSession.JwtToken);
        return http;
    }

    /// <summary>Revisão cruzada com GPT (08/2026) — 401 nunca deve virar
    /// HttpRequestException genérica (cairia no EnsureSuccessStatusCode e
    /// pareceria "sem internet" pro classificador). 403 é DIFERENTE — o
    /// servidor entendeu quem é o usuário, só não autorizou aquela operação
    /// (permissão/perfil/regra de negócio), então NÃO vira sessão expirada
    /// automaticamente aqui; segue pro EnsureSuccessStatusCode genérico de
    /// cada método, que lança HttpRequestException com o status no texto —
    /// aparece pro operador como erro, sem se disfarçar de "sem conexão" nem
    /// de "sessão expirou" (nenhum dos dois seria verdade).</summary>
    private static void LancarSeSessaoExpirada(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new SessaoExpiradaException();
    }

    public async Task<IEnumerable<SaleDto>> GetAllAsync(DateTime? from = null, DateTime? to = null, string? sellerId = null)
    {
        using var http = CriarHttpClient();
        var query = new List<string>();
        if (from != null)     query.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to != null)       query.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        if (sellerId != null) query.Add($"sellerId={Uri.EscapeDataString(sellerId)}");
        var qs = query.Count > 0 ? "?" + string.Join("&", query) : "";

        var resp = await http.GetAsync($"{AppSession.ApiBaseUrl}/api/sales{qs}");
        LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        var sales = await resp.Content.ReadFromJsonAsync<List<SaleDto>>(JsonOpcoes);
        return sales ?? new List<SaleDto>();
    }

    public async Task<SaleDetailDto?> GetDetailAsync(Guid id)
    {
        using var http = CriarHttpClient();
        var resp = await http.GetAsync($"{AppSession.ApiBaseUrl}/api/sales/{id}");
        LancarSeSessaoExpirada(resp);

        // Mesmo comportamento do SaleService local: venda inexistente = null,
        // não exceção — GetById do controller devolve 404 exatamente pra isso.
        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<SaleDetailDto>(JsonOpcoes);
    }

    public async Task<SaleDto> CreateAsync(CreateSaleDto dto)
    {
        using var http = CriarHttpClient();
        // dto vai como veio — é o MESMO objeto que também é serializado pro
        // Outbox no fallback offline (confirmado byte a byte antes de mexer
        // aqui), então o servidor recebe exatamente o que receberia em
        // qualquer um dos dois caminhos.
        var resp = await http.PostAsJsonAsync($"{AppSession.ApiBaseUrl}/api/sales", dto, JsonOpcoes);
        LancarSeSessaoExpirada(resp);

        if (resp.StatusCode == HttpStatusCode.BadRequest)
        {
            var erro = await LerMensagemDeErroAsync(resp);
            // SaleService local lança InvalidOperationException pra regra de
            // negócio (ex: LimiteCreditoExcedidoException, que por sua vez
            // também não é conectividade) — o classificador já trata
            // InvalidOperationException como "nunca é offline", preservado aqui.
            throw new InvalidOperationException(erro);
        }

        resp.EnsureSuccessStatusCode(); // qualquer outro erro (5xx etc.) sobe como HttpRequestException — propositalmente não capturado aqui
        var sale = await resp.Content.ReadFromJsonAsync<SaleDto>(JsonOpcoes);
        return sale ?? throw new InvalidOperationException("API retornou sucesso sem corpo na criação da venda.");
    }

    public async Task CancelAsync(Guid id, string reason)
    {
        using var http = CriarHttpClient();
        var resp = await http.PostAsJsonAsync(
            $"{AppSession.ApiBaseUrl}/api/sales/{id}/cancel",
            new { Motivo = reason }, JsonOpcoes);
        LancarSeSessaoExpirada(resp);

        // Mesmos dois tipos de exceção que SaleService.CancelAsync lança
        // local — preservado pra quem já captura esses tipos especificamente
        // (ex: SaleViewModel.CancelarVendaAsync).
        if (resp.StatusCode == HttpStatusCode.NotFound)
            throw new KeyNotFoundException($"Venda {id} não encontrada.");
        if (resp.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(await LerMensagemDeErroAsync(resp));

        resp.EnsureSuccessStatusCode();
    }

    public async Task AtualizarDadosNfceAsync(Guid vendaId, string urlDanfe, string status, string ambiente, string referencia, string? chave = null, string? numero = null)
    {
        using var http = CriarHttpClient();
        var resp = await http.PatchAsJsonAsync(
            $"{AppSession.ApiBaseUrl}/api/sales/{vendaId}/nfce",
            new { UrlDanfe = urlDanfe, Status = status, Ambiente = ambiente, Referencia = referencia, Chave = chave, Numero = numero },
            JsonOpcoes);
        LancarSeSessaoExpirada(resp);

        // Igual ao SaleService local: venda inexistente é no-op silencioso,
        // o endpoint PATCH devolve 204 nesse caso também — nada especial a
        // tratar aqui além de deixar falha de rede genuína propagar.
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<SalesReportItemDto>> GetSalesReportAsync(DateTime startDate, DateTime endDate, string? sellerName = null)
    {
        using var http = CriarHttpClient();
        var query = $"startDate={Uri.EscapeDataString(startDate.ToString("o"))}&endDate={Uri.EscapeDataString(endDate.ToString("o"))}";
        if (sellerName != null) query += $"&sellerName={Uri.EscapeDataString(sellerName)}";

        var resp = await http.GetAsync($"{AppSession.ApiBaseUrl}/api/sales/report?{query}");
        LancarSeSessaoExpirada(resp);
        resp.EnsureSuccessStatusCode();
        var report = await resp.Content.ReadFromJsonAsync<List<SalesReportItemDto>>(JsonOpcoes);
        return report ?? new List<SalesReportItemDto>();
    }

    /// <summary>A API devolve erros de negócio como {"erro": "mensagem"} —
    /// lê esse formato especificamente; se não conseguir (corpo vazio ou
    /// formato inesperado), usa o texto bruto como último recurso, nunca
    /// deixa a mensagem de erro vazia pro operador.</summary>
    private static async Task<string> LerMensagemDeErroAsync(HttpResponseMessage resp)
    {
        try
        {
            var corpo = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            if (corpo != null && corpo.TryGetValue("erro", out var msg) && !string.IsNullOrWhiteSpace(msg))
                return msg;
        }
        catch { /* corpo não era o JSON esperado — cai no fallback abaixo */ }

        var texto = await resp.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(texto) ? $"Erro HTTP {(int)resp.StatusCode}" : texto;
    }
}