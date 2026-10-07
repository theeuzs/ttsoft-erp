using ERP.Application.Fiscal.Focus;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

public class FocusResponseClassifierTests
{
    private static FocusVerdict Classify(int http, string? corpo, FocusOperation op) =>
        FocusResponseClassifier.Classify(FocusResponseParser.FromHttp(http, corpo), op);

    // ── Respostas reais capturadas ───────────────────────────────────────

    [Theory(DisplayName = "Caso 2/3 reais: NFC-e autorizada e Autorizado (GET)")]
    [InlineData(200)]
    [InlineData(201)]
    public void Autorizada_Real(int http)
    {
        Classify(http, FocusFixtures.Autorizada, FocusOperation.Get).Kind
            .Should().Be(FocusVerdictKind.Autorizado);
    }

    [Fact(DisplayName = "Caso 1 real: erro_autorizacao + 704 e RejeicaoFiscal com EhRejeicao704")]
    public void Rejeitada704_Real()
    {
        var v = Classify(200, FocusFixtures.Rejeitada704, FocusOperation.Get);

        v.Kind.Should().Be(FocusVerdictKind.RejeicaoFiscal);
        v.StatusSefaz.Should().Be("704");
        v.EhRejeicao704.Should().BeTrue();
    }

    [Fact(DisplayName = "Caso 4 real: 404 + nao_encontrado no GET e NaoEncontrado")]
    public void NaoEncontrado_Real_Get()
    {
        Classify(404, FocusFixtures.NaoEncontrado, FocusOperation.Get).Kind
            .Should().Be(FocusVerdictKind.NaoEncontrado);
    }

    [Theory(DisplayName = "Caso 5 real: 401 e 403 sao ErroDeConfiguracao, em GET e POST")]
    [InlineData(401, FocusOperation.Get)]
    [InlineData(401, FocusOperation.Post)]
    [InlineData(403, FocusOperation.Get)]
    [InlineData(403, FocusOperation.Post)]
    public void ErroDeConfiguracao(int http, FocusOperation op)
    {
        Classify(http, FocusFixtures.PermissaoNegada, op).Kind
            .Should().Be(FocusVerdictKind.ErroDeConfiguracao);
    }

    [Fact(DisplayName = "Suporte: 422 + already_processed no POST e JaProcessado")]
    public void AlreadyProcessed_Post()
    {
        Classify(422, FocusFixtures.AlreadyProcessed, FocusOperation.Post).Kind
            .Should().Be(FocusVerdictKind.JaProcessado);
    }

    [Fact(DisplayName = "Suporte: 422 + pending_operation no POST e OperacaoPendente")]
    public void PendingOperation_Post()
    {
        Classify(422, FocusFixtures.PendingOperation, FocusOperation.Post).Kind
            .Should().Be(FocusVerdictKind.OperacaoPendente);
    }

    // ── Regressao do bug do 422 ──────────────────────────────────────────

    [Theory(DisplayName = "REGRESSAO: 422 de nota ja autorizada ou pendente NUNCA e rejeicao definitiva")]
    [InlineData(FocusFixtures.AlreadyProcessed)]
    [InlineData(FocusFixtures.PendingOperation)]
    public void Regressao422_NaoEhRejeicaoDefinitiva(string corpo)
    {
        // O HostedService atual trata qualquer 422 ("UnprocessableEntity") como
        // rejeicao definitiva e marca a venda Rejeitada, mesmo com a NFC-e
        // autorizada na Focus. O classificador nao pode repetir isso.
        var v = Classify(422, corpo, FocusOperation.Post);

        v.Kind.Should().NotBe(FocusVerdictKind.RejeicaoDefinitiva);
        v.Kind.Should().NotBe(FocusVerdictKind.RejeicaoFiscal);
    }

    [Fact(DisplayName = "Codigos sao comparados sem diferenciar maiusculas")]
    public void Codigo_SemDiferenciarMaiusculas()
    {
        Classify(422, "{ \"codigo\": \"ALREADY_PROCESSED\" }", FocusOperation.Post).Kind
            .Should().Be(FocusVerdictKind.JaProcessado);
    }

    // ── 200: status do documento ─────────────────────────────────────────

    [Theory(DisplayName = "200 com processando_autorizacao e Processando (200 e 201)")]
    [InlineData(200)]
    [InlineData(201)]
    public void Processando(int http)
    {
        Classify(http, FocusFixtures.Processando, FocusOperation.Get).Kind
            .Should().Be(FocusVerdictKind.Processando);
    }

    [Fact(DisplayName = "200 com denegado e Denegado")]
    public void Denegado()
    {
        var v = Classify(200, FocusFixtures.Denegado, FocusOperation.Get);

        v.Kind.Should().Be(FocusVerdictKind.Denegado);
        v.StatusSefaz.Should().Be("301");
    }

    [Fact(DisplayName = "erro_autorizacao com outro codigo SEFAZ e RejeicaoFiscal, mas nao 704")]
    public void RejeicaoFiscal_Outro()
    {
        var v = Classify(200, FocusFixtures.RejeicaoOutroCodigoSefaz, FocusOperation.Get);

        v.Kind.Should().Be(FocusVerdictKind.RejeicaoFiscal);
        v.StatusSefaz.Should().Be("778");
        v.EhRejeicao704.Should().BeFalse();
    }

    [Theory(DisplayName = "200 com status cancelado, vazio, ausente ou desconhecido e Desconhecido")]
    [InlineData(FocusFixtures.Cancelado)]
    [InlineData("{ \"status\": \"\" }")]
    [InlineData("{ \"status\": \"algo_novo\" }")]
    [InlineData("{}")]
    [InlineData("")]
    public void Status200_Desconhecido(string corpo)
    {
        Classify(200, corpo, FocusOperation.Get).Kind
            .Should().Be(FocusVerdictKind.Desconhecido);
    }

    // ── 404 ──────────────────────────────────────────────────────────────

    [Theory(DisplayName = "404 so e NaoEncontrado no GET com o codigo documentado; demais sao Desconhecido")]
    [InlineData(404, "{ \"codigo\": \"nao_encontrado\" }", FocusOperation.Post)]
    [InlineData(404, "{ \"codigo\": \"outro_codigo\" }", FocusOperation.Get)]
    [InlineData(404, "{}", FocusOperation.Get)]
    [InlineData(404, FocusFixtures.Html502, FocusOperation.Get)]
    [InlineData(404, "", FocusOperation.Get)]
    public void NotFound_Ambiguo_EhDesconhecido(int http, string corpo, FocusOperation op)
    {
        Classify(http, corpo, op).Kind.Should().Be(FocusVerdictKind.Desconhecido);
    }

    // ── 409 / 422 ────────────────────────────────────────────────────────

    [Fact(DisplayName = "422 com outro codigo no POST e RejeicaoDefinitiva (validacao do payload)")]
    public void Outro422_Post_RejeicaoDefinitiva()
    {
        var v = Classify(422, "{ \"codigo\": \"erro_validacao_schema\", \"mensagem\": \"x\" }", FocusOperation.Post);

        v.Kind.Should().Be(FocusVerdictKind.RejeicaoDefinitiva);
        v.Detalhe.Should().Contain("erro_validacao_schema");
    }

    [Theory(DisplayName = "422/409 sem codigo, ou no GET, ou 409 com outro codigo: Desconhecido")]
    [InlineData(422, "", FocusOperation.Post)]
    [InlineData(422, FocusFixtures.Html502, FocusOperation.Post)]
    [InlineData(422, "{ \"codigo\": \"erro_validacao_schema\" }", FocusOperation.Get)]
    [InlineData(409, "{ \"codigo\": \"qualquer\" }", FocusOperation.Post)]
    [InlineData(409, "", FocusOperation.Post)]
    public void Conflito_Ambiguo_EhDesconhecido(int http, string corpo, FocusOperation op)
    {
        Classify(http, corpo, op).Kind.Should().Be(FocusVerdictKind.Desconhecido);
    }

    [Theory(DisplayName = "409 com already_processed/pending_operation no POST segue o corpo")]
    [InlineData("already_processed", FocusVerdictKind.JaProcessado)]
    [InlineData("pending_operation", FocusVerdictKind.OperacaoPendente)]
    public void Conflito409_SegueCorpo(string codigo, FocusVerdictKind esperado)
    {
        Classify(409, $"{{ \"codigo\": \"{codigo}\" }}", FocusOperation.Post).Kind.Should().Be(esperado);
    }

    // ── Erros por status HTTP ────────────────────────────────────────────

    [Fact(DisplayName = "400 e ErroDeRequisicao")]
    public void Http400() =>
        Classify(400, "{ \"codigo\": \"requisicao_invalida\" }", FocusOperation.Post).Kind
            .Should().Be(FocusVerdictKind.ErroDeRequisicao);

    [Theory(DisplayName = "408, 429 e 5xx sao Transitorio, com ou sem corpo")]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    [InlineData(599)]
    public void Transitorios(int http)
    {
        Classify(http, FocusFixtures.Html502, FocusOperation.Get).Kind.Should().Be(FocusVerdictKind.Transitorio);
        Classify(http, null, FocusOperation.Post).Kind.Should().Be(FocusVerdictKind.Transitorio);
    }

    [Theory(DisplayName = "Falha de transporte e Transitorio, em GET e POST")]
    [InlineData(FocusOperation.Get)]
    [InlineData(FocusOperation.Post)]
    public void FalhaDeTransporte_Transitorio(FocusOperation op)
    {
        var resposta = FocusResponseParser.FromTransportError("Timeout");

        FocusResponseClassifier.Classify(resposta, op).Kind.Should().Be(FocusVerdictKind.Transitorio);
    }

    [Theory(DisplayName = "Status HTTP nao previsto (3xx, 418, 600) e Desconhecido")]
    [InlineData(301)]
    [InlineData(418)]
    [InlineData(451)]
    [InlineData(600)]
    public void StatusNaoPrevisto_Desconhecido(int http)
    {
        Classify(http, "{}", FocusOperation.Get).Kind.Should().Be(FocusVerdictKind.Desconhecido);
    }

    [Fact(DisplayName = "Resposta nula lanca ArgumentNullException")]
    public void RespostaNula_Lanca()
    {
        var act = () => FocusResponseClassifier.Classify(null!, FocusOperation.Get);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "EhRejeicao704 e falso para qualquer outro veredito")]
    public void EhRejeicao704_SoParaRejeicaoFiscal()
    {
        new FocusVerdict(FocusVerdictKind.Autorizado, "704").EhRejeicao704.Should().BeFalse();
        new FocusVerdict(FocusVerdictKind.RejeicaoFiscal, "703").EhRejeicao704.Should().BeFalse();
        new FocusVerdict(FocusVerdictKind.RejeicaoFiscal, null).EhRejeicao704.Should().BeFalse();
    }
}
