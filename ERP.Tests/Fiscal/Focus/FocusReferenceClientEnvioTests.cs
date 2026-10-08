using System.Net;
using System.Net.Http;
using System.Text;
using ERP.Application.Fiscal.Focus;
using ERP.Infrastructure.HttpClients;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// 4A-5b: POST estruturado de emissao de NFC-e (FocusReferenceClient.EnviarNfceAsync).
/// Sem rede: HttpMessageHandler falso. O handler le o corpo recebido em bytes, para provar
/// o que de fato seria enviado. Nenhum teste chama a Focus.
/// </summary>
public class FocusReferenceClientEnvioTests
{
    private const string Ref = "5c8078ba-ed08-4c08-87ff-0c08b4a9f758";
    private const string Token = "token-de-teste";
    private const string Corpo = "{\"ref\":\"x\"}";

    private sealed record Chamada(
        HttpMethod Metodo, string Url, string Query, string? Esquema, string? Parametro,
        byte[] Corpo, string? MediaType, string? Charset, long? ContentLength);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
        private readonly object _trava = new();
        private readonly List<Chamada> _chamadas = new();

        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        public IReadOnlyList<Chamada> Chamadas
        {
            get { lock (_trava) { return _chamadas.ToList(); } }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Captura AGORA: o cliente descarta a requisicao (e o conteudo) depois.
            var corpo = request.Content is null
                ? Array.Empty<byte>()
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);

            var chamada = new Chamada(
                request.Method,
                request.RequestUri!.AbsoluteUri,
                request.RequestUri.Query,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                corpo,
                request.Content?.Headers.ContentType?.MediaType,
                request.Content?.Headers.ContentType?.CharSet,
                request.Content?.Headers.ContentLength);

            lock (_trava) { _chamadas.Add(chamada); }

            return await _responder(request, cancellationToken);
        }
    }

    private static FakeHandler Responde(HttpStatusCode status, string corpo) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json")
        }));

    private static FocusReferenceClient NovoCliente(FakeHandler handler) => new(new HttpClient(handler));

    private static string TokenDe(string? basicParametro) =>
        Encoding.ASCII.GetString(Convert.FromBase64String(basicParametro!)).TrimEnd(':');

    // ── Forma da requisicao ──────────────────────────────────────────────

    [Fact(DisplayName = "POST em /v2/nfce?ref={ref}, com o mesmo formato de URL de antes")]
    public async Task Post_MetodoEUrl()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, isProducao: true);

        var c = handler.Chamadas.Should().ContainSingle().Subject;
        c.Metodo.Should().Be(HttpMethod.Post);
        c.Url.Should().Be($"https://api.focusnfe.com.br/v2/nfce?ref={Ref}");
    }

    [Fact(DisplayName = "Homologacao usa o host de homologacao")]
    public async Task Post_Homologacao()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, isProducao: false);

        handler.Chamadas.Single().Url.Should().Be($"https://homologacao.focusnfe.com.br/v2/nfce?ref={Ref}");
    }

    [Fact(DisplayName = "A referencia vai escapada como dado na query: reservados nao criam parametro novo")]
    public async Task Post_ReferenciaEscapada()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        await NovoCliente(handler).EnviarNfceAsync("a b/c?d&e=f#g", Corpo, Token, true);

        handler.Chamadas.Single().Query.Should().Be("?ref=a%20b%2Fc%3Fd%26e%3Df%23g");
    }

    [Fact(DisplayName = "Basic auth = base64(token + ':'), calculada por requisicao")]
    public async Task Post_BasicAuth()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true);

        var c = handler.Chamadas.Single();
        c.Esquema.Should().Be("Basic");
        c.Parametro.Should().Be(Convert.ToBase64String(Encoding.ASCII.GetBytes(Token + ":")));
    }

    [Fact(DisplayName = "Content-Type application/json em UTF-8 e Content-Length igual ao tamanho do corpo em bytes")]
    public async Task Post_ContentType()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);
        var corpo = "{\"descricao\":\"AÇÃO – 😀\"}";

        await NovoCliente(handler).EnviarNfceAsync(Ref, corpo, Token, true);

        var c = handler.Chamadas.Single();
        c.MediaType.Should().Be("application/json");
        c.Charset.Should().BeEquivalentTo("utf-8");
        c.ContentLength.Should().Be(Encoding.UTF8.GetByteCount(corpo));
    }

    // ── Condicao 3: o corpo chega identico, byte a byte ──────────────────

    [Theory(DisplayName = "O corpo recebido pelo handler e o JSON original em UTF-8, sem reserializar e sem BOM")]
    [InlineData("{ \"b\":1 , \"a\" : \"x\\u00e7\" }")]                       // espacos estranhos e escape \u
    [InlineData("{\"t\":\"😀 ç ã – “aspas” \\ud83d\\ude00\"}")]               // emoji, acentos, aspas tipograficas
    [InlineData("{\"b\":1,\"a\":2,\"c\":[3,2,1]}")]                           // ordem de campos preservada
    [InlineData("{\n\t\"a\": 1.50,\r\n\t\"b\": 1E3\n}")]                      // quebras de linha e numeros nao normalizados
    [InlineData("{\"a\":null,\"b\":\"\",\"c\":[]}")]                          // nulos, vazios
    [InlineData("isto-nao-e-json")]                                           // o cliente nao valida nem parseia
    public async Task Post_CorpoIdenticoEmBytes(string corpo)
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        await NovoCliente(handler).EnviarNfceAsync(Ref, corpo, Token, true);

        var recebido = handler.Chamadas.Single().Corpo;
        recebido.Should().Equal(Encoding.UTF8.GetBytes(corpo));
        recebido.Take(3).Should().NotEqual(new byte[] { 0xEF, 0xBB, 0xBF }, "nao pode haver BOM");
    }

    // ── Condicao 4: credenciais isoladas ─────────────────────────────────

    [Fact(DisplayName = "Chamadas seguidas com tokens diferentes: cada uma leva so o seu")]
    public async Task Auth_Sequencial()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);
        var cliente = NovoCliente(handler);

        await cliente.EnviarNfceAsync(Ref, Corpo, "tenant-a", true);
        await cliente.EnviarNfceAsync(Ref, Corpo, "tenant-b", true);
        await cliente.EnviarNfceAsync(Ref, Corpo, "tenant-a", true);

        handler.Chamadas.Select(c => TokenDe(c.Parametro)).Should().Equal("tenant-a", "tenant-b", "tenant-a");
    }

    [Fact(DisplayName = "60 chamadas CONCORRENTES com tokens e corpos diferentes: nenhum vazamento entre elas")]
    public async Task Auth_Concorrente()
    {
        var handler = new FakeHandler(async (_, ct) =>
        {
            await Task.Delay(5, ct);   // forca a intercalacao das chamadas
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FocusFixtures.Autorizada, Encoding.UTF8, "application/json")
            };
        });
        var cliente = NovoCliente(handler);
        const int n = 60;

        var tarefas = Enumerable.Range(0, n)
            .Select(i => Task.Run(() => cliente.EnviarNfceAsync($"ref-{i}", "{\"i\":" + i + "}", $"token-{i}", true)))
            .ToArray();
        await Task.WhenAll(tarefas);

        handler.Chamadas.Should().HaveCount(n);
        foreach (var c in handler.Chamadas)
        {
            var i = int.Parse(c.Query.Substring("?ref=ref-".Length));
            TokenDe(c.Parametro).Should().Be($"token-{i}", $"a chamada da ref-{i} nao pode levar o token de outra");
            Encoding.UTF8.GetString(c.Corpo).Should().Be("{\"i\":" + i + "}");
        }
    }

    [Fact(DisplayName = "GET e POST no MESMO cliente nao compartilham credencial, e o GET continua em /v2/nfce/{ref}")]
    public async Task Auth_GetEPost_NoMesmoCliente()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);
        var cliente = NovoCliente(handler);

        await cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, "tenant-a", true);
        await cliente.EnviarNfceAsync(Ref, Corpo, "tenant-b", true);
        await cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, "tenant-a", true);

        var chamadas = handler.Chamadas;
        chamadas.Select(c => c.Metodo).Should().Equal(HttpMethod.Get, HttpMethod.Post, HttpMethod.Get);
        chamadas.Select(c => TokenDe(c.Parametro)).Should().Equal("tenant-a", "tenant-b", "tenant-a");
        chamadas[0].Url.Should().Be($"https://api.focusnfe.com.br/v2/nfce/{Ref}");
        chamadas[1].Url.Should().Be($"https://api.focusnfe.com.br/v2/nfce?ref={Ref}");
    }

    // ── Condicao 2: nenhum retry automatico ──────────────────────────────

    [Theory(DisplayName = "Resposta de falha (5xx, 429, 408): UMA unica requisicao, sem retry")]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(429)]
    [InlineData(408)]
    public async Task SemRetry_RespostaDeFalha(int status)
    {
        var handler = Responde((HttpStatusCode)status, FocusFixtures.Html502);

        var resposta = await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true);

        handler.Chamadas.Should().ContainSingle();
        resposta.HttpStatus.Should().Be(status);
    }

    [Fact(DisplayName = "Erro de rede: UMA unica requisicao, sem retry")]
    public async Task SemRetry_ErroDeRede()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("conexao recusada"));

        await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true);

        handler.Chamadas.Should().ContainSingle();
    }

    [Fact(DisplayName = "Timeout: UMA unica requisicao, sem retry")]
    public async Task SemRetry_Timeout()
    {
        var handler = new FakeHandler((_, _) => throw new TaskCanceledException("timeout simulado"));

        await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true);

        handler.Chamadas.Should().ContainSingle();
    }

    // ── Condicao 5: falha de transporte x cancelamento do chamador ───────

    [Fact(DisplayName = "Erro de rede vira falha de transporte (HttpStatus 0), nao excecao, e e Transitorio")]
    public async Task Transporte_ErroDeRede()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("conexao recusada"));

        var resposta = await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true);

        resposta.HttpStatus.Should().Be(0);
        resposta.TransportError.Should().Contain("conexao recusada");
        FocusResponseClassifier.Classify(resposta, FocusOperation.Post).Kind.Should().Be(FocusVerdictKind.Transitorio);
    }

    [Fact(DisplayName = "Timeout (o chamador NAO cancelou) vira falha de transporte, e e Transitorio")]
    public async Task Transporte_Timeout()
    {
        var handler = new FakeHandler((_, _) => throw new TaskCanceledException("timeout simulado"));

        var resposta = await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true);

        resposta.HttpStatus.Should().Be(0);
        resposta.TransportError.Should().StartWith("Timeout");
        FocusResponseClassifier.Classify(resposta, FocusOperation.Post).Kind.Should().Be(FocusVerdictKind.Transitorio);
    }

    [Fact(DisplayName = "Cancelamento ja solicitado pelo chamador: propaga OperationCanceledException")]
    public async Task Cancelamento_JaSolicitado()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "Cancelamento DURANTE a requisicao: propaga, nunca vira FocusResponse de falha")]
    public async Task Cancelamento_DuranteARequisicao()
    {
        var handler = new FakeHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        Func<Task> act = async () => await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "TaskCanceledException COM cancelamento pedido pelo chamador nao e tratada como timeout")]
    public async Task Cancelamento_NaoEhTimeout()
    {
        using var cts = new CancellationTokenSource();
        var handler = new FakeHandler((_, ct) =>
        {
            cts.Cancel();
            throw new TaskCanceledException("cancelado", null, cts.Token);
        });

        Func<Task> act = async () => await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Condicao 6: respostas HTTP, sem excecao e sem perder o corpo bruto ─

    [Theory(DisplayName = "Cada resposta chega estruturada, com o corpo bruto preservado, e o classificador a entende (POST)")]
    [InlineData(200, FocusFixtures.Autorizada, FocusVerdictKind.Autorizado)]
    [InlineData(200, FocusFixtures.Processando, FocusVerdictKind.Processando)]
    [InlineData(200, FocusFixtures.Rejeitada704, FocusVerdictKind.RejeicaoFiscal)]
    [InlineData(200, FocusFixtures.Denegado, FocusVerdictKind.Denegado)]
    [InlineData(422, FocusFixtures.AlreadyProcessed, FocusVerdictKind.JaProcessado)]
    [InlineData(422, FocusFixtures.PendingOperation, FocusVerdictKind.OperacaoPendente)]
    [InlineData(422, "{\"codigo\":\"erro_validacao_schema\",\"mensagem\":\"campo invalido\"}", FocusVerdictKind.RejeicaoDefinitiva)]
    [InlineData(400, "{\"codigo\":\"requisicao_invalida\",\"mensagem\":\"x\"}", FocusVerdictKind.ErroDeRequisicao)]
    [InlineData(401, FocusFixtures.PermissaoNegada, FocusVerdictKind.ErroDeConfiguracao)]
    [InlineData(403, FocusFixtures.PermissaoNegada, FocusVerdictKind.ErroDeConfiguracao)]
    [InlineData(500, FocusFixtures.Html502, FocusVerdictKind.Transitorio)]
    [InlineData(502, FocusFixtures.Html502, FocusVerdictKind.Transitorio)]
    [InlineData(503, "", FocusVerdictKind.Transitorio)]
    [InlineData(429, "{\"codigo\":\"limite\"}", FocusVerdictKind.Transitorio)]
    [InlineData(404, FocusFixtures.NaoEncontrado, FocusVerdictKind.Desconhecido)]   // 404 em POST nao e "ref nao recebida"
    public async Task Resposta_Estruturada(int status, string corpo, FocusVerdictKind esperado)
    {
        var handler = Responde((HttpStatusCode)status, corpo);

        var resposta = await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true);

        resposta.HttpStatus.Should().Be(status);
        resposta.RawBody.Should().Be(corpo, "o corpo bruto nunca pode ser perdido");
        resposta.TransportError.Should().BeNull();
        FocusResponseClassifier.Classify(resposta, FocusOperation.Post).Kind.Should().Be(esperado);
    }

    [Theory(DisplayName = "Corpo vazio, HTML ou texto solto em qualquer status: sem excecao, corpo bruto preservado")]
    [InlineData(200, "")]
    [InlineData(200, "isto-nao-e-json")]
    [InlineData(200, "{")]
    [InlineData(422, "")]
    [InlineData(422, FocusFixtures.Html502)]
    [InlineData(502, FocusFixtures.Html502)]
    [InlineData(500, "")]
    public async Task Resposta_Inesperada_NaoLanca(int status, string corpo)
    {
        var handler = Responde((HttpStatusCode)status, corpo);

        var resposta = await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, Token, true);

        resposta.HttpStatus.Should().Be(status);
        resposta.RawBody.Should().Be(corpo);
        var veredito = FocusResponseClassifier.Classify(resposta, FocusOperation.Post);
        Enum.IsDefined(veredito.Kind).Should().BeTrue();
    }

    // ── Validacao de argumentos: antes de qualquer I/O ───────────────────

    [Theory(DisplayName = "Referencia vazia lanca ArgumentException e nao faz requisicao")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Argumentos_ReferenciaVazia(string? referencia)
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        Func<Task> act = async () => await NovoCliente(handler).EnviarNfceAsync(referencia!, Corpo, Token, true);

        await act.Should().ThrowAsync<ArgumentException>();
        handler.Chamadas.Should().BeEmpty();
    }

    [Theory(DisplayName = "Token vazio lanca ArgumentException e nao faz requisicao")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Argumentos_TokenVazio(string? token)
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        Func<Task> act = async () => await NovoCliente(handler).EnviarNfceAsync(Ref, Corpo, token!, true);

        await act.Should().ThrowAsync<ArgumentException>();
        handler.Chamadas.Should().BeEmpty();
    }

    [Theory(DisplayName = "Corpo vazio lanca ArgumentException e nao faz requisicao")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Argumentos_CorpoVazio(string? corpo)
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        Func<Task> act = async () => await NovoCliente(handler).EnviarNfceAsync(Ref, corpo!, Token, true);

        await act.Should().ThrowAsync<ArgumentException>();
        handler.Chamadas.Should().BeEmpty();
    }
}
