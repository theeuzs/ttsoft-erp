using System.Net.Http.Headers;
using System.Text;
using ERP.Application.Fiscal.Focus;

namespace ERP.Infrastructure.HttpClients;

/// <summary>
/// Implementacao do IFocusReferenceClient. Ao contrario do FocusNfeHttpClient,
/// NAO guarda o token em estado compartilhado do HttpClient: a autenticacao e
/// montada por requisicao (sem corrida entre tenants).
/// Ainda nao e registrado no DI: sera ligado na Etapa 4A-5.
/// </summary>
public sealed class FocusReferenceClient : IFocusReferenceClient
{
    private readonly HttpClient _http;

    public FocusReferenceClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<FocusResponse> ConsultarAsync(
        FocusDocumentType tipo,
        string referencia,
        string token,
        bool isProducao,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token da Focus obrigatorio.", nameof(token));

        // Valida referencia e tipo antes de qualquer I/O.
        var url = FocusEndpoints.Consulta(tipo, referencia, isProducao);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(token + ":")));

        try
        {
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FocusResponseParser.FromHttp((int)response.StatusCode, body);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // O chamador cancelou: nao e falha de transporte, propaga.
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Timeout do HttpClient (o chamador nao cancelou).
            return FocusResponseParser.FromTransportError($"Timeout: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return FocusResponseParser.FromTransportError($"Falha de comunicacao: {ex.Message}");
        }
    }

    public async Task<FocusResponse> EnviarNfceAsync(
        string referencia,
        string jsonBody,
        string token,
        bool isProducao,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token da Focus obrigatorio.", nameof(token));

        if (string.IsNullOrWhiteSpace(jsonBody))
            throw new ArgumentException("Corpo da requisicao obrigatorio.", nameof(jsonBody));

        // Valida a referencia antes de qualquer I/O.
        var url = FocusEndpoints.EmissaoNfce(referencia, isProducao);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            // Corpo repassado como veio: sem desserializar nem reserializar (mesmo
            // StringContent UTF-8 + application/json que o FocusNfeHttpClient usa hoje).
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        // Autenticacao por requisicao, sem estado compartilhado no HttpClient.
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(token + ":")));

        try
        {
            // Um unico envio. Nenhum retry aqui, nem por handler, nem por politica.
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FocusResponseParser.FromHttp((int)response.StatusCode, body);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // O chamador cancelou: nao e falha de transporte, propaga.
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Timeout do HttpClient (o chamador nao cancelou).
            return FocusResponseParser.FromTransportError($"Timeout: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return FocusResponseParser.FromTransportError($"Falha de comunicacao: {ex.Message}");
        }
    }
}
