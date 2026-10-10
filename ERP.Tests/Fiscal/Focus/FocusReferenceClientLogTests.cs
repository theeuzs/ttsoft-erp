using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using ERP.Application.Fiscal.Focus;
using ERP.Infrastructure.HttpClients;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// 4A-6c-0: o registro das tentativas do FocusReferenceClient. O QUE PROVA: o ciclo de eventos (iniciada e exatamente UM desfecho) em todos os
/// cenarios, inclusive falha de comunicacao, timeout, cancelamento do chamador (distinto de timeout) e excecao inesperada; que NADA sensivel
/// vaza (token, cabecalho, corpo, mensagem bruta de excecao: aqui as mensagens das excecoes CONTEM o token de proposito); que so as propriedades
/// da lista branca existem; que uma requisicao rejeitada pelas validacoes nao gera linha; que o log e emitido ANTES do envio; e que uma falha do
/// proprio log nao altera o resultado. O comportamento do cliente em si continua coberto pelos 38 testes existentes, que NAO foram alterados.
/// </summary>
public class FocusReferenceClientLogTests
{
    private const string Ref = "5c8078ba-ed08-4c08-87ff-0c08b4a9f758";
    private const string Token = "SEGREDO-TOKEN-XYZ-123";
    private const string CorpoFiscal = "{\"cpf_destinatario\":\"11122233344\",\"observacao\":\"SEGREDO-CORPO-ABC\"}";

    private static readonly string[] PropriedadesPermitidas =
    {
        "Operacao", "Documento", "Referencia", "Host", "Ambiente", "TentativaId", "StatusHttp", "DuracaoMs", "Falha", "TipoExcecao"
    };

    private sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) { _responder = responder; }

        public int Chamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            return _responder(request, cancellationToken);
        }
    }

    private sealed class LoggerQueLanca : ILogger<FocusReferenceClient>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("o sink de log falhou");
    }

    private static Task<HttpResponseMessage> Resposta(HttpStatusCode status, string corpo) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") });

    private static (FocusReferenceClient Cliente, Handler Handler, LoggerQueCapturaEstado<FocusReferenceClient> Log) Montar(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder, LoggerQueCapturaEstado<FocusReferenceClient>? log = null)
    {
        log ??= new LoggerQueCapturaEstado<FocusReferenceClient>();
        var handler = new Handler(responder);
        return (new FocusReferenceClient(new HttpClient(handler), log), handler, log);
    }

    private static bool EhIniciada(LoggerQueCapturaEstado<FocusReferenceClient>.Entrada e) => e.Template.StartsWith("Focus tentativa iniciada");

    private static bool EhConcluida(LoggerQueCapturaEstado<FocusReferenceClient>.Entrada e) => e.Template.StartsWith("Focus tentativa concluida");

    private static bool EhSemResposta(LoggerQueCapturaEstado<FocusReferenceClient>.Entrada e) => e.Template.StartsWith("Focus tentativa sem resposta");

    private static bool EhCancelada(LoggerQueCapturaEstado<FocusReferenceClient>.Entrada e) => e.Template.StartsWith("Focus tentativa cancelada");

    // ── Desfecho: resposta HTTP recebida ─────────────────────────────────

    [Theory(DisplayName = "GET com resposta 200: 'iniciada' e 'concluida' (resposta HTTP recebida), mesmo TentativaId, host e ambiente corretos")]
    [InlineData(true, "api.focusnfe.com.br", "producao")]
    [InlineData(false, "homologacao.focusnfe.com.br", "homologacao")]
    public async Task Get200_IniciadaEConcluida(bool producao, string host, string ambiente)
    {
        var (cliente, _, log) = Montar((_, _) => Resposta(HttpStatusCode.OK, "{\"status\":\"processando_autorizacao\"}"));

        await cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, producao);

        log.Entradas.Should().HaveCount(2);
        var (iniciada, concluida) = (log.Entradas[0], log.Entradas[1]);
        EhIniciada(iniciada).Should().BeTrue();
        EhConcluida(concluida).Should().BeTrue();
        iniciada.Nivel.Should().Be(LogLevel.Information);
        concluida.Nivel.Should().Be(LogLevel.Information);
        concluida.Propriedades["TentativaId"].Should().Be(iniciada.Propriedades["TentativaId"]);

        foreach (var e in log.Entradas)
        {
            e.Propriedades["Operacao"].Should().Be("GET");
            e.Propriedades["Documento"].Should().Be("Nfce");
            e.Propriedades["Referencia"].Should().Be(Ref);
            e.Propriedades["Host"].Should().Be(host);
            e.Propriedades["Ambiente"].Should().Be(ambiente);
        }

        concluida.Propriedades["StatusHttp"].Should().Be(200);
        ((long)concluida.Propriedades["DuracaoMs"]!).Should().BeGreaterThanOrEqualTo(0);
        iniciada.Propriedades.Should().NotContainKey("StatusHttp");
    }

    [Fact(DisplayName = "POST com resposta 422: 'iniciada' e 'concluida' com o status 422 (resposta de erro tambem e resposta HTTP recebida)")]
    public async Task Post422_IniciadaEConcluida()
    {
        var (cliente, _, log) = Montar((_, _) => Resposta(HttpStatusCode.UnprocessableEntity, "{\"codigo\":\"requisicao_invalida\"}"));

        await cliente.EnviarNfceAsync(Ref, CorpoFiscal, Token, false);

        log.Entradas.Should().HaveCount(2);
        EhIniciada(log.Entradas[0]).Should().BeTrue();
        EhConcluida(log.Entradas[1]).Should().BeTrue();
        log.Entradas[1].Propriedades["Operacao"].Should().Be("POST");
        log.Entradas[1].Propriedades["StatusHttp"].Should().Be(422);
    }

    // ── Desfecho: sem resposta HTTP / cancelamento / excecao ─────────────

    [Fact(DisplayName = "Falha de comunicacao: 'iniciada' e 'sem resposta' (Falha=Comunicacao, sem StatusHttp); o retorno ao chamador continua o mesmo")]
    public async Task FalhaDeComunicacao_SemResposta()
    {
        var (cliente, _, log) = Montar((_, _) => throw new HttpRequestException($"falhou com {Token} e {CorpoFiscal}"));

        var resposta = await cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false);

        resposta.HouveRespostaHttp.Should().BeFalse();
        log.Entradas.Should().HaveCount(2);
        EhIniciada(log.Entradas[0]).Should().BeTrue();
        EhSemResposta(log.Entradas[1]).Should().BeTrue();
        log.Entradas[1].Nivel.Should().Be(LogLevel.Warning);
        log.Entradas[1].Propriedades["Falha"].Should().Be("Comunicacao");
        log.Entradas[1].Propriedades["TipoExcecao"].Should().Be("HttpRequestException");
        log.Entradas[1].Propriedades.Should().NotContainKey("StatusHttp");
        log.Entradas[1].Mensagem.Should().Contain("nao e possivel afirmar se a requisicao chegou ou foi processada");
    }

    [Fact(DisplayName = "Timeout (sem cancelamento do chamador): 'sem resposta' com Falha=Timeout; NAO e classificado como 'cancelada'")]
    public async Task Timeout_NaoEClassificadoComoCancelamento()
    {
        var (cliente, _, log) = Montar((_, _) => throw new TaskCanceledException($"timeout com {Token}"));

        var resposta = await cliente.EnviarNfceAsync(Ref, CorpoFiscal, Token, false, CancellationToken.None);

        resposta.HouveRespostaHttp.Should().BeFalse();
        log.Entradas.Should().HaveCount(2);
        EhSemResposta(log.Entradas[1]).Should().BeTrue();
        log.Entradas[1].Propriedades["Falha"].Should().Be("Timeout");
        log.Entradas[1].Propriedades["TipoExcecao"].Should().Be("TaskCanceledException");
        log.Entradas.Should().NotContain(e => EhCancelada(e));
    }

    [Fact(DisplayName = "Cancelamento do CHAMADOR: 'iniciada' e 'cancelada'; a excecao continua propagando; NAO e classificado como 'sem resposta'")]
    public async Task CancelamentoDoChamador_Cancelada_EPropaga()
    {
        using var cts = new CancellationTokenSource();
        var (cliente, _, log) = Montar(async (_, ct) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        Func<Task> act = () => cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        log.Entradas.Should().HaveCount(2);
        EhIniciada(log.Entradas[0]).Should().BeTrue();
        EhCancelada(log.Entradas[1]).Should().BeTrue();
        log.Entradas[1].Nivel.Should().Be(LogLevel.Warning);
        log.Entradas.Should().NotContain(e => EhSemResposta(e));
    }

    [Fact(DisplayName = "Excecao inesperada: 'sem resposta' (Falha=Excecao) e a MESMA excecao continua propagando")]
    public async Task ExcecaoInesperada_RegistraEPropaga()
    {
        var (cliente, _, log) = Montar((_, _) => throw new InvalidOperationException($"boom {Token}"));

        Func<Task> act = () => cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false);

        await act.Should().ThrowAsync<InvalidOperationException>();
        log.Entradas.Should().HaveCount(2);
        log.Entradas[1].Propriedades["Falha"].Should().Be("Excecao");
        log.Entradas[1].Propriedades["TipoExcecao"].Should().Be("InvalidOperationException");
    }

    // ── Validacoes: sem tentativa, sem linha ─────────────────────────────

    [Fact(DisplayName = "Requisicao rejeitada pelas validacoes (token, referencia, corpo): NENHUMA linha de log e nenhuma chamada ao handler")]
    public async Task Validacoes_NaoGeramLinha()
    {
        var (cliente, handler, log) = Montar((_, _) => Resposta(HttpStatusCode.OK, "{}"));

        Func<Task> semTokenGet = () => cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, "   ", false);
        Func<Task> semRefGet = () => cliente.ConsultarAsync(FocusDocumentType.Nfce, "   ", Token, false);
        Func<Task> semTokenPost = () => cliente.EnviarNfceAsync(Ref, CorpoFiscal, "", false);
        Func<Task> semCorpoPost = () => cliente.EnviarNfceAsync(Ref, "  ", Token, false);
        Func<Task> semRefPost = () => cliente.EnviarNfceAsync("   ", CorpoFiscal, Token, false);

        await semTokenGet.Should().ThrowAsync<Exception>();
        await semRefGet.Should().ThrowAsync<Exception>();
        await semTokenPost.Should().ThrowAsync<Exception>();
        await semCorpoPost.Should().ThrowAsync<Exception>();
        await semRefPost.Should().ThrowAsync<Exception>();

        log.Entradas.Should().BeEmpty("nao houve tentativa");
        handler.Chamadas.Should().Be(0);
    }

    [Fact(DisplayName = "A linha 'iniciada' sai ANTES de o handler ser invocado (imediatamente antes do SendAsync)")]
    public async Task Iniciada_SaiAntesDoEnvio()
    {
        var log = new LoggerQueCapturaEstado<FocusReferenceClient>();
        var entradasQuandoEnviou = -1;
        var (cliente, _, _) = Montar((_, _) =>
        {
            entradasQuandoEnviou = log.Entradas.Count;
            return Resposta(HttpStatusCode.OK, "{}");
        }, log);

        await cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false);

        entradasQuandoEnviou.Should().Be(1, "so a linha 'iniciada' existia quando o envio aconteceu");
    }

    // ── Onde termina a "resposta recebida" ───────────────────────────────

    private sealed class ConteudoQueFalhaAoSerLido : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            throw new IOException("a conexao caiu enquanto o corpo era lido");

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }
    }

    [Fact(DisplayName = "CORPO que falha DENTRO do SendAsync (comportamento padrao do HttpClient): o cliente nao obtem status, entao e 'sem resposta' e NUNCA 'concluida'")]
    public async Task FalhaAoLerOCorpoDentroDoSendAsync_NaoHaStatus_EClassificadaComoSemResposta()
    {
        var (cliente, _, log) = Montar((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new ConteudoQueFalhaAoSerLido() }));

        try { await cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false); } catch { /* o desfecho depende de como o HttpClient embrulha a falha */ }

        log.Entradas.Should().HaveCount(2);
        EhIniciada(log.Entradas[0]).Should().BeTrue();
        EhSemResposta(log.Entradas[1]).Should().BeTrue();
        log.Entradas.Should().NotContain(e => EhConcluida(e), "o corpo e lido dentro do SendAsync: o cliente nunca chega a ter o status");
        log.Entradas[1].Propriedades.Should().NotContainKey("StatusHttp");
    }

    [Fact(DisplayName = "FALHA DEPOIS de receber a resposta (decodificacao do corpo): continua 'concluida' com o status e NAO gera um segundo desfecho")]
    public async Task FalhaDepoisDeReceberAResposta_ContinuaConcluida_SemSegundoDesfecho()
    {
        var (cliente, _, log) = Montar((_, _) =>
        {
            var resposta = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}", Encoding.UTF8) };
            resposta.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "charset-invalido-xyz" };
            return Task.FromResult(resposta);
        });

        // Se o runtime recusar o charset, ReadAsStringAsync lanca DEPOIS de o status ja ser conhecido; se aceitar, o fluxo e o normal.
        // Nos dois casos o log tem de ser o mesmo: UMA tentativa, UM desfecho, 'concluida' com o status 404.
        try { await cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false); } catch { /* a propagacao original e coberta pelos testes existentes do cliente */ }

        log.Entradas.Should().HaveCount(2);
        EhIniciada(log.Entradas[0]).Should().BeTrue();
        EhConcluida(log.Entradas[1]).Should().BeTrue();
        log.Entradas[1].Propriedades["StatusHttp"].Should().Be(404);
        log.Entradas.Should().NotContain(e => EhSemResposta(e) || EhCancelada(e));
    }

    // ── Seguranca e contrato ─────────────────────────────────────────────

    private static async Task<List<LoggerQueCapturaEstado<FocusReferenceClient>.Entrada>> ExecutarTodosOsCenarios()
    {
        var todas = new List<LoggerQueCapturaEstado<FocusReferenceClient>.Entrada>();

        async Task Cenario(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
            Func<FocusReferenceClient, CancellationToken, Task> executar, CancellationToken ct = default)
        {
            var (cliente, _, log) = Montar(responder);
            try { await executar(cliente, ct); } catch { /* o cenario pode propagar de proposito */ }
            todas.AddRange(log.Entradas);
        }

        await Cenario((_, _) => Resposta(HttpStatusCode.OK, "{\"status\":\"autorizado\"}"), (c, ct) => c.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true, ct));
        await Cenario((_, _) => Resposta(HttpStatusCode.OK, "{}"), (c, ct) => c.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false, ct));
        await Cenario((_, _) => Resposta(HttpStatusCode.UnprocessableEntity, "{\"codigo\":\"x\"}"), (c, ct) => c.EnviarNfceAsync(Ref, CorpoFiscal, Token, false, ct));
        await Cenario((_, _) => throw new HttpRequestException($"dns {Token} {CorpoFiscal}"), (c, ct) => c.EnviarNfceAsync(Ref, CorpoFiscal, Token, false, ct));
        await Cenario((_, _) => throw new TaskCanceledException($"timeout {Token} {CorpoFiscal}"), (c, ct) => c.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false, ct));
        await Cenario((_, _) => throw new InvalidOperationException($"boom {Token} {CorpoFiscal}"), (c, ct) => c.EnviarNfceAsync(Ref, CorpoFiscal, Token, false, ct));

        using var cts = new CancellationTokenSource();
        await Cenario(async (_, ct) => { cts.Cancel(); await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(HttpStatusCode.OK); },
            (c, ct) => c.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false, ct), cts.Token);

        return todas;
    }

    [Fact(DisplayName = "SEGREDOS: token, cabecalho de autenticacao, corpo fiscal e mensagem bruta de excecao NAO aparecem em nenhum template, mensagem ou propriedade (em todos os cenarios)")]
    public async Task NadaSensivelVaza()
    {
        var entradas = await ExecutarTodosOsCenarios();
        entradas.Should().NotBeEmpty();

        var proibidos = new[]
        {
            Token, Convert.ToBase64String(Encoding.ASCII.GetBytes(Token + ":")), "11122233344", "SEGREDO-CORPO", "SEGREDO-TOKEN",
            "Authorization", "Basic ", "cpf_destinatario", "boom"
        };

        foreach (var entrada in entradas)
        {
            var textos = new List<string> { entrada.Template, entrada.Mensagem };
            textos.AddRange(entrada.Propriedades.Keys);
            textos.AddRange(entrada.Propriedades.Values.Select(v => v?.ToString() ?? string.Empty));

            foreach (var texto in textos)
            foreach (var proibido in proibidos)
                texto.Contains(proibido, StringComparison.OrdinalIgnoreCase).Should().BeFalse($"'{proibido}' nao pode aparecer em: {texto}");
        }
    }

    [Fact(DisplayName = "CONTRATO: so as propriedades da lista branca existem (nada de Detalhe, Url, Corpo, Headers, Mensagem da excecao)")]
    public async Task SoPropriedadesDaListaBranca()
    {
        var entradas = await ExecutarTodosOsCenarios();

        foreach (var entrada in entradas)
            entrada.Propriedades.Keys.Should().BeSubsetOf(PropriedadesPermitidas);
    }

    [Fact(DisplayName = "INVARIANTE: toda tentativa 'iniciada' tem EXATAMENTE UM desfecho (concluida, sem resposta ou cancelada), em todos os cenarios")]
    public async Task UmUnicoDesfechoPorTentativa()
    {
        var entradas = await ExecutarTodosOsCenarios();

        var porTentativa = entradas.GroupBy(e => e.Propriedades["TentativaId"]).ToList();
        porTentativa.Should().HaveCount(7, "um por cenario");

        foreach (var tentativa in porTentativa)
        {
            tentativa.Count(EhIniciada).Should().Be(1);
            tentativa.Count(e => EhConcluida(e) || EhSemResposta(e) || EhCancelada(e)).Should().Be(1);
            tentativa.Should().HaveCount(2);
        }
    }

    // ── Observacional: uma falha do log nao altera o fluxo ───────────────

    [Fact(DisplayName = "OBSERVACIONAL: se o proprio log lancar excecao, a resposta, a falha de transporte e o cancelamento continuam IDENTICOS")]
    public async Task FalhaDoLog_NaoAlteraOFluxo()
    {
        var logQueLanca = new LoggerQueLanca();

        var ok = new FocusReferenceClient(new HttpClient(new Handler((_, _) => Resposta(HttpStatusCode.OK, "{\"status\":\"autorizado\"}"))), logQueLanca);
        var respostaOk = await ok.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false);
        respostaOk.HouveRespostaHttp.Should().BeTrue();
        respostaOk.HttpStatus.Should().Be(200);

        var transporte = new FocusReferenceClient(new HttpClient(new Handler((_, _) => throw new HttpRequestException("dns"))), logQueLanca);
        var respostaFalha = await transporte.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false);
        respostaFalha.HouveRespostaHttp.Should().BeFalse();

        using var cts = new CancellationTokenSource();
        var cancelando = new FocusReferenceClient(new HttpClient(new Handler(async (_, ct) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })), logQueLanca);
        Func<Task> act = () => cancelando.ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, false, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
