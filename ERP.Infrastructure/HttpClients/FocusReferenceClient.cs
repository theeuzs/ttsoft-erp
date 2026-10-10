using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using ERP.Application.Fiscal.Focus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.HttpClients;

/// <summary>
/// Implementacao do IFocusReferenceClient. Ao contrario do FocusNfeHttpClient,
/// NAO guarda o token em estado compartilhado do HttpClient: a autenticacao e
/// montada por requisicao (sem corrida entre tenants).
///
/// REGISTRO DE TENTATIVAS (observabilidade, 4A-6c-0): cada tentativa de consulta ou envio gera uma linha "iniciada" e EXATAMENTE UM desfecho
/// ("concluida", "sem resposta" ou "cancelada"). E ESTRITAMENTE OBSERVACIONAL: nao altera requisicao, resposta, excecao nem retry, e uma falha do
/// proprio log nunca altera o fluxo. NUNCA registra token, cabecalho de autenticacao, corpo da requisicao ou da resposta, URL completa nem a
/// mensagem bruta de excecoes: so o host (que distingue homologacao de producao), a referencia e o tipo da excecao.
///  - iniciada: o codigo chegou a invocar SendAsync (nao garante que bytes tenham saido).
///  - concluida: o cliente OBTEVE a resposta HTTP (o status). O desfecho e fixado assim que SendAsync devolve, ANTES de ler o corpo; nao prova, por si so,
///    o que a Focus processou internamente.
///  - sem resposta: o cliente NAO obteve uma resposta HTTP; NAO e possivel afirmar se a requisicao chegou ou foi processada. Atencao: com o SendAsync
///    padrao (ResponseContentRead) o corpo e lido DENTRO do SendAsync; uma falha ou timeout ali impede o cliente de ter o status e entra aqui.
/// Cada tentativa tem NO MAXIMO um desfecho (travado estruturalmente): uma falha depois de "concluida" nao gera um segundo.
///  - cancelada: o cancelamento do CHAMADOR foi identificado (nao e timeout) e a excecao continua propagando.
/// </summary>
public sealed class FocusReferenceClient : IFocusReferenceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<FocusReferenceClient> _logger;

    public FocusReferenceClient(HttpClient http, ILogger<FocusReferenceClient>? logger = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? NullLogger<FocusReferenceClient>.Instance;
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

        var tentativa = new Tentativa("GET", tipo.ToString(), referencia, url, isProducao);
        RegistrarIniciada(tentativa);

        try
        {
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

            // A resposta HTTP foi RECEBIDA: o desfecho da tentativa e fixado AGORA, antes de ler o corpo. Uma falha posterior (leitura, decodificacao
            // ou parse) continua se comportando exatamente como antes, mas NAO gera um segundo desfecho nem reclassifica a tentativa como "sem resposta".
            RegistrarConcluida(tentativa, (int)response.StatusCode);

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FocusResponseParser.FromHttp((int)response.StatusCode, body);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // O chamador cancelou: nao e falha de transporte, propaga.
            RegistrarCancelada(tentativa);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Timeout do HttpClient (o chamador nao cancelou).
            RegistrarSemResposta(tentativa, "Timeout", ex);
            return FocusResponseParser.FromTransportError($"Timeout: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            RegistrarSemResposta(tentativa, "Comunicacao", ex);
            return FocusResponseParser.FromTransportError($"Falha de comunicacao: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Qualquer outra excecao continua propagando exatamente como antes; so garante o desfecho unico da tentativa.
            RegistrarSemResposta(tentativa, "Excecao", ex);
            throw;
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

        var tentativa = new Tentativa("POST", "Nfce", referencia, url, isProducao);
        RegistrarIniciada(tentativa);

        try
        {
            // Um unico envio. Nenhum retry aqui, nem por handler, nem por politica.
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

            // A resposta HTTP foi RECEBIDA: o desfecho da tentativa e fixado AGORA, antes de ler o corpo. Uma falha posterior (leitura, decodificacao
            // ou parse) continua se comportando exatamente como antes, mas NAO gera um segundo desfecho nem reclassifica a tentativa como "sem resposta".
            RegistrarConcluida(tentativa, (int)response.StatusCode);

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return FocusResponseParser.FromHttp((int)response.StatusCode, body);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // O chamador cancelou: nao e falha de transporte, propaga.
            RegistrarCancelada(tentativa);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Timeout do HttpClient (o chamador nao cancelou).
            RegistrarSemResposta(tentativa, "Timeout", ex);
            return FocusResponseParser.FromTransportError($"Timeout: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            RegistrarSemResposta(tentativa, "Comunicacao", ex);
            return FocusResponseParser.FromTransportError($"Falha de comunicacao: {ex.Message}");
        }
        catch (Exception ex)
        {
            RegistrarSemResposta(tentativa, "Excecao", ex);
            throw;
        }
    }

    // ── Registro de tentativas (somente observacao) ──────────────────────

    /// <summary>Dados NAO sensiveis de uma tentativa: so o que o log pode conter.</summary>
    private sealed class Tentativa
    {
        public Tentativa(string operacao, string documento, string referencia, string url, bool isProducao)
        {
            Operacao = operacao;
            Documento = documento;
            Referencia = referencia.Trim();
            Host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : string.Empty;
            Ambiente = isProducao ? "producao" : "homologacao";
        }

        public Guid Id { get; } = Guid.NewGuid();
        public string Operacao { get; }
        public string Documento { get; }
        public string Referencia { get; }
        public string Host { get; }
        public string Ambiente { get; }
        public Stopwatch Cronometro { get; } = Stopwatch.StartNew();

        private int _desfechoFixado;

        /// <summary>Devolve true so na PRIMEIRA chamada: garante no maximo um desfecho por tentativa.</summary>
        public bool FixarDesfecho() => Interlocked.Exchange(ref _desfechoFixado, 1) == 0;
    }

    private void RegistrarIniciada(Tentativa t) => Observar(() =>
        _logger.LogInformation(
            "Focus tentativa iniciada: {Operacao} {Documento} ref {Referencia} em {Host} ({Ambiente}) [{TentativaId}].",
            t.Operacao, t.Documento, t.Referencia, t.Host, t.Ambiente, t.Id));

    private void RegistrarConcluida(Tentativa t, int statusHttp)
    {
        if (!t.FixarDesfecho())
            return;

        Observar(() => _logger.LogInformation(
            "Focus tentativa concluida (resposta HTTP recebida): {Operacao} {Documento} ref {Referencia} em {Host} ({Ambiente}) " +
            "status {StatusHttp} em {DuracaoMs} ms [{TentativaId}].",
            t.Operacao, t.Documento, t.Referencia, t.Host, t.Ambiente, statusHttp, t.Cronometro.ElapsedMilliseconds, t.Id));
    }

    private void RegistrarSemResposta(Tentativa t, string falha, Exception ex)
    {
        if (!t.FixarDesfecho())
            return;

        Observar(() => _logger.LogWarning(
            "Focus tentativa sem resposta HTTP (nao e possivel afirmar se a requisicao chegou ou foi processada): " +
            "{Operacao} {Documento} ref {Referencia} em {Host} ({Ambiente}) falha {Falha} ({TipoExcecao}) em {DuracaoMs} ms [{TentativaId}].",
            t.Operacao, t.Documento, t.Referencia, t.Host, t.Ambiente, falha, ex.GetType().Name, t.Cronometro.ElapsedMilliseconds, t.Id));
    }

    private void RegistrarCancelada(Tentativa t)
    {
        if (!t.FixarDesfecho())
            return;

        Observar(() => _logger.LogWarning(
            "Focus tentativa cancelada pelo chamador: {Operacao} {Documento} ref {Referencia} em {Host} ({Ambiente}) em {DuracaoMs} ms [{TentativaId}].",
            t.Operacao, t.Documento, t.Referencia, t.Host, t.Ambiente, t.Cronometro.ElapsedMilliseconds, t.Id));
    }

    /// <summary>A observabilidade nunca altera o fluxo fiscal: uma falha do proprio log e engolida.</summary>
    private static void Observar(Action registrar)
    {
        try
        {
            registrar();
        }
        catch
        {
            // Intencionalmente vazio.
        }
    }
}
