using System.Net;
using System.Text;
using ERP.Application.Fiscal.Focus;
using ERP.Infrastructure.HttpClients;
using System.Net.Http;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// 4A-0: garantia arquitetural (nao reproducao de bug). A consulta de um
/// documento NFCE usa SEMPRE /v2/nfce/{ref}, e NFE usa /v2/nfe/{ref}. O fato
/// de a Focus aceitar uma NFC-e em /v2/nfe (capturado em 07/10) e irrelevante
/// e nao pode ser usado como comportamento.
/// Sem rede: HttpMessageHandler falso.
/// </summary>
public class FocusReferenceClientTests
{
    private const string Ref = "5c8078ba-ed08-4c08-87ff-0c08b4a9f758";
    private const string Token = "token-de-teste";

    private sealed record Chamada(HttpMethod Metodo, string Url, string? Esquema, string? Parametro);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public List<Chamada> Chamadas { get; } = new();

        public FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Captura o que importa AGORA: o cliente descarta a requisicao depois.
            Chamadas.Add(new Chamada(
                request.Method,
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter));

            return _responder(request, cancellationToken);
        }
    }

    private static FakeHandler Responde(HttpStatusCode status, string corpo) =>
        new((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json")
        }));

    private static FocusReferenceClient NovoCliente(FakeHandler handler) =>
        new(new HttpClient(handler));

    // ── 4A-0: endpoint determinado pelo tipo ─────────────────────────────

    [Fact(DisplayName = "4A-0: NFCE consulta /v2/nfce/{ref} e NUNCA /v2/nfe")]
    public async Task Nfce_UsaEndpointNfce_NuncaNfe()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, isProducao: true);

        handler.Chamadas.Should().ContainSingle();
        handler.Chamadas[0].Url.Should().Be($"https://api.focusnfe.com.br/v2/nfce/{Ref}");
        handler.Chamadas[0].Url.Should().NotContain("/v2/nfe/");
    }

    [Fact(DisplayName = "4A-0: NFE consulta /v2/nfe/{ref} e nao /v2/nfce")]
    public async Task Nfe_UsaEndpointNfe()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfe, Ref, Token, isProducao: true);

        handler.Chamadas.Should().ContainSingle();
        handler.Chamadas[0].Url.Should().Be($"https://api.focusnfe.com.br/v2/nfe/{Ref}");
        handler.Chamadas[0].Url.Should().NotContain("/v2/nfce/");
    }

    [Fact(DisplayName = "4A-0: um 404 em NFCE NAO dispara tentativa em outro endpoint (sem fallback)")]
    public async Task Nfce_404_NaoFazFallback()
    {
        var handler = Responde(HttpStatusCode.NotFound, FocusFixtures.NaoEncontrado);

        var resposta = await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true);

        handler.Chamadas.Should().ContainSingle("o fallback NF-e/NFC-e por texto de erro era o mecanismo que esta etapa elimina");
        resposta.HttpStatus.Should().Be(404);
        resposta.Codigo.Should().Be("nao_encontrado");
    }

    [Fact(DisplayName = "Homologacao usa o host de homologacao")]
    public async Task Homologacao_UsaHostDeHomologacao()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, isProducao: false);

        handler.Chamadas[0].Url.Should().StartWith("https://homologacao.focusnfe.com.br/v2/nfce/");
    }

    // ── Requisicao ───────────────────────────────────────────────────────

    [Fact(DisplayName = "E um GET com Basic auth = base64(token + ':'), por requisicao")]
    public async Task EhGet_ComBasicAuth()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true);

        var chamada = handler.Chamadas.Single();
        chamada.Metodo.Should().Be(HttpMethod.Get);
        chamada.Esquema.Should().Be("Basic");
        chamada.Parametro.Should().Be(Convert.ToBase64String(Encoding.ASCII.GetBytes(Token + ":")));
    }

    [Fact(DisplayName = "Tokens diferentes em chamadas seguidas nao vazam entre si (sem estado compartilhado)")]
    public async Task Tokens_NaoVazamEntreChamadas()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);
        var cliente = NovoCliente(handler);

        await cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, "tenant-a", true);
        await cliente.ConsultarAsync(FocusDocumentType.Nfce, Ref, "tenant-b", true);

        handler.Chamadas.Select(c => c.Parametro).Should().Equal(
            Convert.ToBase64String(Encoding.ASCII.GetBytes("tenant-a:")),
            Convert.ToBase64String(Encoding.ASCII.GetBytes("tenant-b:")));
    }

    // ── Respostas ────────────────────────────────────────────────────────

    [Fact(DisplayName = "200: devolve a resposta estruturada (chave normalizada, status)")]
    public async Task Resposta200_Estruturada()
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        var resposta = await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true);

        resposta.HttpStatus.Should().Be(200);
        resposta.Status.Should().Be("autorizado");
        resposta.ChaveNfe.Should().Be(FocusFixtures.ChaveNormalizada);
        FocusResponseClassifier.Classify(resposta, FocusOperation.Get).Kind
            .Should().Be(FocusVerdictKind.Autorizado);
    }

    [Fact(DisplayName = "401: devolve o corpo de erro, classificado como ErroDeConfiguracao")]
    public async Task Resposta401()
    {
        var handler = Responde(HttpStatusCode.Unauthorized, FocusFixtures.PermissaoNegada);

        var resposta = await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true);

        resposta.Codigo.Should().Be("permissao_negada");
        FocusResponseClassifier.Classify(resposta, FocusOperation.Get).Kind
            .Should().Be(FocusVerdictKind.ErroDeConfiguracao);
    }

    [Fact(DisplayName = "404 real: classificado como NaoEncontrado")]
    public async Task Resposta404_NaoEncontrado()
    {
        var handler = Responde(HttpStatusCode.NotFound, FocusFixtures.NaoEncontrado);

        var resposta = await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true);

        FocusResponseClassifier.Classify(resposta, FocusOperation.Get).Kind
            .Should().Be(FocusVerdictKind.NaoEncontrado);
    }

    [Fact(DisplayName = "502 com HTML: nao lanca, vira Transitorio")]
    public async Task Resposta502_Html_Transitorio()
    {
        var handler = Responde(HttpStatusCode.BadGateway, FocusFixtures.Html502);

        var resposta = await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true);

        resposta.HttpStatus.Should().Be(502);
        resposta.RawBody.Should().Be(FocusFixtures.Html502);
        FocusResponseClassifier.Classify(resposta, FocusOperation.Get).Kind
            .Should().Be(FocusVerdictKind.Transitorio);
    }

    // ── Falhas de transporte ─────────────────────────────────────────────

    [Fact(DisplayName = "HttpRequestException vira falha de transporte (HttpStatus 0), nao excecao")]
    public async Task HttpRequestException_ViraTransporte()
    {
        var handler = new FakeHandler((_, _) => throw new HttpRequestException("conexao recusada"));

        var resposta = await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true);

        resposta.HttpStatus.Should().Be(0);
        resposta.TransportError.Should().Contain("conexao recusada");
        resposta.HouveRespostaHttp.Should().BeFalse();
        FocusResponseClassifier.Classify(resposta, FocusOperation.Get).Kind
            .Should().Be(FocusVerdictKind.Transitorio);
    }

    [Fact(DisplayName = "Timeout (TaskCanceledException sem cancelamento do chamador) vira falha de transporte")]
    public async Task Timeout_ViraTransporte()
    {
        var handler = new FakeHandler((_, _) => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));

        var resposta = await NovoCliente(handler).ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true);

        resposta.HttpStatus.Should().Be(0);
        resposta.TransportError.Should().StartWith("Timeout");
    }

    [Fact(DisplayName = "Cancelamento pelo chamador propaga OperationCanceledException (nao vira transporte)")]
    public async Task CancelamentoDoChamador_Propaga()
    {
        var handler = new FakeHandler((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await NovoCliente(handler)
            .ConsultarAsync(FocusDocumentType.Nfce, Ref, Token, true, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Erros do chamador (antes de qualquer I/O) ────────────────────────

    [Theory(DisplayName = "Token vazio lanca ArgumentException e nao faz requisicao")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TokenVazio_Lanca_SemRequisicao(string? token)
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        Func<Task> act = async () => await NovoCliente(handler)
            .ConsultarAsync(FocusDocumentType.Nfce, Ref, token!, true);

        await act.Should().ThrowAsync<ArgumentException>();
        handler.Chamadas.Should().BeEmpty();
    }

    [Theory(DisplayName = "Referencia vazia lanca ArgumentException e nao faz requisicao")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ReferenciaVazia_Lanca_SemRequisicao(string? referencia)
    {
        var handler = Responde(HttpStatusCode.OK, FocusFixtures.Autorizada);

        Func<Task> act = async () => await NovoCliente(handler)
            .ConsultarAsync(FocusDocumentType.Nfce, referencia!, Token, true);

        await act.Should().ThrowAsync<ArgumentException>();
        handler.Chamadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Construtor rejeita HttpClient nulo")]
    public void Construtor_HttpClientNulo()
    {
        var act = () => new FocusReferenceClient(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
