using ERP.Application.Fiscal.Focus;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

public class FocusResponseParserTests
{
    [Fact(DisplayName = "Autorizada: mapeia todos os campos e normaliza a chave (NFe + 44 digitos)")]
    public void Autorizada_MapeiaCampos()
    {
        var r = FocusResponseParser.FromHttp(200, FocusFixtures.Autorizada);

        r.HttpStatus.Should().Be(200);
        r.HouveRespostaHttp.Should().BeTrue();
        r.TransportError.Should().BeNull();
        r.Status.Should().Be("autorizado");
        r.StatusSefaz.Should().Be("100");
        r.MensagemSefaz.Should().Be("Autorizado o uso da NF-e");
        r.ChaveNfeBruta.Should().Be(FocusFixtures.ChaveBruta);
        r.ChaveNfe.Should().Be(FocusFixtures.ChaveNormalizada).And.HaveLength(44);
        r.Numero.Should().Be("3322");
        r.Serie.Should().Be("1");
        r.Protocolo.Should().Be("141261595914973");
        r.CaminhoXmlNotaFiscal.Should().EndWith("-nfe.xml");
        r.CaminhoDanfe.Should().StartWith("/notas_fiscais_consumidor/");
        r.RawBody.Should().Be(FocusFixtures.Autorizada);
    }

    [Fact(DisplayName = "Rejeicao 704: status e status_sefaz identificaveis, sem chave nem numero")]
    public void Rejeitada704_TemStatusSefaz_SemChave()
    {
        var r = FocusResponseParser.FromHttp(200, FocusFixtures.Rejeitada704);

        r.Status.Should().Be("erro_autorizacao");
        r.StatusSefaz.Should().Be("704");
        r.MensagemSefaz.Should().Contain("atrasada");
        r.ChaveNfe.Should().BeNull();
        r.ChaveNfeBruta.Should().BeNull();
        r.Numero.Should().BeNull();
    }

    [Fact(DisplayName = "404 real: codigo e mensagem")]
    public void NaoEncontrado_MapeiaCodigoEMensagem()
    {
        var r = FocusResponseParser.FromHttp(404, FocusFixtures.NaoEncontrado);

        r.HttpStatus.Should().Be(404);
        r.Codigo.Should().Be("nao_encontrado");
        r.Mensagem.Should().Be("Nota fiscal não encontrada");
        r.Status.Should().BeNull();
    }

    [Fact(DisplayName = "401 real: permissao_negada")]
    public void PermissaoNegada_MapeiaCodigo()
    {
        var r = FocusResponseParser.FromHttp(401, FocusFixtures.PermissaoNegada);

        r.Codigo.Should().Be("permissao_negada");
        r.Mensagem.Should().StartWith("Access token");
    }

    [Theory(DisplayName = "422 do suporte: codigo preservado")]
    [InlineData(FocusFixtures.PendingOperation, "pending_operation")]
    [InlineData(FocusFixtures.AlreadyProcessed, "already_processed")]
    public void Corpos422_MapeiamCodigo(string corpo, string codigoEsperado)
    {
        var r = FocusResponseParser.FromHttp(422, corpo);

        r.Codigo.Should().Be(codigoEsperado);
        r.Mensagem.Should().NotBeNullOrWhiteSpace();
    }

    [Theory(DisplayName = "Corpo ausente, vazio ou nao-JSON nunca lanca; RawBody preservado")]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData(FocusFixtures.Html502, FocusFixtures.Html502)]
    [InlineData("{", "{")]
    [InlineData("[1,2,3]", "[1,2,3]")]
    [InlineData("null", "null")]
    [InlineData("\"texto\"", "\"texto\"")]
    public void CorpoInvalido_NaoLanca_CamposNulos(string? corpo, string rawEsperado)
    {
        var r = FocusResponseParser.FromHttp(502, corpo);

        r.HttpStatus.Should().Be(502);
        r.RawBody.Should().Be(rawEsperado);
        r.Codigo.Should().BeNull();
        r.Status.Should().BeNull();
        r.ChaveNfe.Should().BeNull();
    }

    [Fact(DisplayName = "Campo numerico no JSON vira texto (numero: 3322)")]
    public void CampoNumerico_ViraTexto()
    {
        var r = FocusResponseParser.FromHttp(200, "{ \"numero\": 3322, \"serie\": 1 }");

        r.Numero.Should().Be("3322");
        r.Serie.Should().Be("1");
    }

    [Fact(DisplayName = "Campo com tipo inesperado (objeto/array/bool) e ignorado")]
    public void CampoComTipoInesperado_Ignorado()
    {
        var r = FocusResponseParser.FromHttp(200, "{ \"status\": {\"a\":1}, \"codigo\": [1], \"numero\": true }");

        r.Status.Should().BeNull();
        r.Codigo.Should().BeNull();
        r.Numero.Should().BeNull();
    }

    [Fact(DisplayName = "Falha de transporte: HttpStatus 0 e TransportError, sem resposta HTTP")]
    public void FalhaDeTransporte()
    {
        var r = FocusResponseParser.FromTransportError("Timeout: A task was canceled.");

        r.HttpStatus.Should().Be(0);
        r.TransportError.Should().Contain("Timeout");
        r.HouveRespostaHttp.Should().BeFalse();
    }

    [Theory(DisplayName = "Falha de transporte sem mensagem recebe texto padrao")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void FalhaDeTransporte_SemMensagem(string? mensagem)
    {
        var r = FocusResponseParser.FromTransportError(mensagem);

        r.TransportError.Should().NotBeNullOrWhiteSpace();
        r.HouveRespostaHttp.Should().BeFalse();
    }

    [Theory(DisplayName = "NormalizarChave: conservadora, nunca tenta consertar")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(FocusFixtures.ChaveBruta, FocusFixtures.ChaveNormalizada)]
    [InlineData(FocusFixtures.ChaveNormalizada, FocusFixtures.ChaveNormalizada)]
    [InlineData("  " + FocusFixtures.ChaveBruta + "  ", FocusFixtures.ChaveNormalizada)]
    [InlineData("nfe41260912820608000141650010000033221640357603", null)]   // prefixo em minusculas: nao inventa
    [InlineData("NFe4126091282060800014165001000003322164035760", null)]    // 43 digitos
    [InlineData("NFe412609128206080001416500100000332216403576033", null)] // 45 digitos
    [InlineData("4126091282060800014165001000003322164035760", null)]       // 43 digitos sem prefixo
    [InlineData("ABC41260912820608000141650010000033221640357603", null)]   // prefixo desconhecido
    [InlineData("NFe4126091282060800014165001000003322164035760X", null)]   // caractere nao numerico
    public void NormalizarChave(string? bruta, string? esperada)
    {
        FocusResponseParser.NormalizarChave(bruta).Should().Be(esperada);
    }

    [Fact(DisplayName = "Chave invalida na resposta: ChaveNfe nula, original preservada em ChaveNfeBruta")]
    public void ChaveInvalida_NaoApagaOriginal()
    {
        var r = FocusResponseParser.FromHttp(200, "{ \"status\": \"autorizado\", \"chave_nfe\": \"123\" }");

        r.ChaveNfe.Should().BeNull();
        r.ChaveNfeBruta.Should().Be("123");
    }
}
