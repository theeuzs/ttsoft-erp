// ERP.WPF/Services/ApiHttp.cs
using ERP.Application.Exceptions;
using ERP.WPF.State;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ERP.WPF.Services;

/// <summary>
/// Fase C da migração WPF→API (09/2026) — peças comuns a TODOS os Http*Service
/// novos (Cliente, Produto, Caixa, Financeiro, Fiscal). Extraído do
/// HttpSaleService em vez de copiado 5 vezes: o comportamento de 401/403/erro
/// de negócio precisa ser idêntico em todos os domínios, e cópia divergiria na
/// primeira correção.
///
/// HttpSaleService NÃO foi migrado pra usar isto ainda, de propósito — está em
/// produção com testes próprios; trocar a base dele é uma mudança separada, sem
/// ganho funcional, que pode ser feita depois que o padrão rodar na loja.
///
/// Mesma regra do HttpSaleService: HttpRequestException/TaskCanceledException
/// NÃO são capturadas aqui — o ConnectivityExceptionClassifier (camada de cima)
/// é quem decide se falha de rede vira modo offline.
/// </summary>
internal static class ApiHttp
{
    public static readonly JsonSerializerOptions JsonOpcoes = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static HttpClient CriarHttpClient(HttpMessageHandler? handler, int timeoutSegundos = 30)
    {
        // disposeHandler: false — mesmo motivo do HttpSaleService: handler de
        // teste é compartilhado entre chamadas.
        var http = handler != null
            ? new HttpClient(handler, disposeHandler: false)
            : new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(timeoutSegundos);
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AppSession.JwtToken);
        return http;
    }

    public static string Url(string caminhoRelativo) => $"{AppSession.ApiBaseUrl}{caminhoRelativo}";

    /// <summary>401 = token vazio/expirado/revogado (TokenVersion). Nunca pode
    /// virar HttpRequestException genérica — pareceria "sem internet".</summary>
    public static void LancarSeSessaoExpirada(HttpResponseMessage resp)
    {
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new SessaoExpiradaException();
    }

    /// <summary>
    /// 403 = servidor sabe quem é o usuário e NEGOU por permissão
    /// ([HasPermission]). Diferente do HttpSaleService (que deixa 403 cair no
    /// EnsureSuccessStatusCode genérico), aqui vira UnauthorizedAccessException
    /// com mensagem legível: na Fase C isso deixa de ser teórico — antes o WPF
    /// ia direto no banco e NUNCA checava permissão de API; agora cargos sem
    /// customers.edit/customers.delete etc. vão bater em 403 de verdade, e o
    /// operador precisa ler "sem permissão", não
    /// "Response status code does not indicate success: 403".
    /// UnauthorizedAccessException não é tratada como conectividade pelo
    /// classificador — correto, não é problema de rede.
    /// </summary>
    public static void LancarSeAcessoNegado(HttpResponseMessage resp, string operacao)
    {
        if (resp.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException(
                $"Seu perfil não tem permissão para {operacao}. Peça a um gerente/administrador.");
    }

    /// <summary>
    /// Lê a mensagem de erro de uma resposta 4xx. A API devolve 3 formatos
    /// diferentes hoje, todos tratados:
    ///   {"erro": "msg"}                 — erro de negócio (padrão dos controllers)
    ///   {"erros": ["a", "b"]}           — FluentValidation (ex: CustomersController.Create)
    ///   ProblemDetails {"errors": {...}} — validação automática do [ApiController]
    ///                                      (ex: campo string não-nulo mandado como null)
    /// Nunca devolve string vazia.
    /// </summary>
    public static async Task<List<string>> LerMensagensDeErroAsync(HttpResponseMessage resp)
    {
        var texto = await resp.Content.ReadAsStringAsync();
        var mensagens = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(texto);
            var raiz = doc.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object)
            {
                if (TryGetCaseInsensitive(raiz, "erro", out var erro) && erro.ValueKind == JsonValueKind.String)
                    mensagens.Add(erro.GetString()!);

                if (TryGetCaseInsensitive(raiz, "erros", out var erros) && erros.ValueKind == JsonValueKind.Array)
                    mensagens.AddRange(erros.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!));

                if (TryGetCaseInsensitive(raiz, "errors", out var pd) && pd.ValueKind == JsonValueKind.Object)
                    foreach (var campo in pd.EnumerateObject())
                        if (campo.Value.ValueKind == JsonValueKind.Array)
                            mensagens.AddRange(campo.Value.EnumerateArray()
                                .Where(e => e.ValueKind == JsonValueKind.String)
                                .Select(e => e.GetString()!));

                if (mensagens.Count == 0 && TryGetCaseInsensitive(raiz, "title", out var titulo)
                    && titulo.ValueKind == JsonValueKind.String)
                    mensagens.Add(titulo.GetString()!);
            }
        }
        catch (JsonException) { /* corpo não-JSON — cai no fallback abaixo */ }

        mensagens.RemoveAll(string.IsNullOrWhiteSpace);
        if (mensagens.Count == 0)
            mensagens.Add(string.IsNullOrWhiteSpace(texto) ? $"Erro HTTP {(int)resp.StatusCode}" : texto);
        return mensagens;
    }

    public static async Task<string> LerMensagemDeErroAsync(HttpResponseMessage resp)
        => string.Join(Environment.NewLine, await LerMensagensDeErroAsync(resp));

    public static async Task<T> LerCorpoObrigatorioAsync<T>(HttpResponseMessage resp, string contexto)
    {
        var corpo = await resp.Content.ReadFromJsonAsync<T>(JsonOpcoes);
        return corpo ?? throw new InvalidOperationException($"API retornou sucesso sem corpo ({contexto}).");
    }

    private static bool TryGetCaseInsensitive(JsonElement obj, string nome, out JsonElement valor)
    {
        foreach (var p in obj.EnumerateObject())
            if (string.Equals(p.Name, nome, StringComparison.OrdinalIgnoreCase))
            {
                valor = p.Value;
                return true;
            }
        valor = default;
        return false;
    }
}
