// ERP.Tests/Application/EmissionServicesChaveNumeroTests.cs
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Application.Services;
using FluentAssertions;
using FluentResults;
using Moq;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Application;

/// <summary>
/// Módulo 5 (Fiscal) — achados testando a Etapa 1 em produção, os dois no
/// mesmo lugar: NfceEmissionService/NfeEmissionService só liam status/
/// caminho_danfe/caminho_xml_nota_fiscal da resposta da Focus, descartando
/// chave_nfe/numero (Sale.NfceChave/NfceNumero e NotaFiscal.Chave/Numero
/// ficavam NULL mesmo com a nota autorizada) e mensagem_sefaz (rejeição
/// chegava no WPF só como "Status: erro_autorizacao", sem o motivo real —
/// confirmado com rejeições de verdade: NCM inexistente, CFOP incompatível
/// com o CSOSN). Essas duas classes nunca tinham teste direto (só mockadas
/// em testes de FiscalService/NotaFiscalAvulsaService, que não exercitam o
/// parsing de verdade).
/// </summary>
public class EmissionServicesChaveNumeroTests
{
    [Fact(DisplayName = "NfceEmissionService — achado de produção: Focus manda chave_nfe com prefixo 'NFe' colado, precisa limpar")]
    public async Task NfceEmissionService_ChaveComPrefixoNFe_LimpaPraSoDigitos()
    {
        var httpMock = new Mock<IFocusNfeHttpClient>();
        httpMock.Setup(h => h.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(Result.Ok(
                "{\"status\":\"autorizado\",\"caminho_danfe\":\"/x.html\",\"chave_nfe\":\"NFe41260912820608000141650010000033031335484324\",\"numero\":\"3303\"}"));

        var svc = new NfceEmissionService(httpMock.Object);
        var (_, _, _, _, chave, _) = await svc.EmitirNfceAsync("ref-1", new FocusNfceRequest(), "token-fake", isProducao: true);

        chave.Should().Be("41260912820608000141650010000033031335484324");
        chave.Should().HaveLength(44);
        chave.Should().NotContain("NFe");
    }

    [Fact(DisplayName = "NfceEmissionService — extrai chave_nfe/numero da resposta autorizada")]
    public async Task NfceEmissionService_Autorizada_ExtraiChaveENumero()
    {
        var httpMock = new Mock<IFocusNfeHttpClient>();
        httpMock.Setup(h => h.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(Result.Ok(
                "{\"status\":\"autorizado\",\"caminho_danfe\":\"/nfce.html\",\"caminho_xml_nota_fiscal\":\"/nfce.xml\"," +
                "\"chave_nfe\":\"41260912820608000141650010000033021148784290\",\"numero\":\"3302\"}"));

        var svc = new NfceEmissionService(httpMock.Object);
        var (sucesso, _, urlDanfe, _, chave, numero) = await svc.EmitirNfceAsync("ref-1", new FocusNfceRequest(), "token-fake", isProducao: true);

        sucesso.Should().BeTrue();
        chave.Should().Be("41260912820608000141650010000033021148784290");
        numero.Should().Be("3302");
        urlDanfe.Should().Be("https://api.focusnfe.com.br/nfce.html");
    }

    [Fact(DisplayName = "NfceEmissionService — rejeição não quebra, chave/numero vêm vazios")]
    public async Task NfceEmissionService_Rejeitada_ChaveENumeroVazios()
    {
        var httpMock = new Mock<IFocusNfeHttpClient>();
        httpMock.Setup(h => h.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(Result.Ok("{\"status\":\"erro_autorizacao\",\"mensagem_sefaz\":\"NCM inválido\"}"));

        var svc = new NfceEmissionService(httpMock.Object);
        var (sucesso, mensagem, _, _, chave, numero) = await svc.EmitirNfceAsync("ref-1", new FocusNfceRequest(), "token-fake", isProducao: true);

        sucesso.Should().BeFalse();
        chave.Should().BeEmpty();
        numero.Should().BeEmpty();
        // Achado testando em produção (depois desta correção existir): o WPF só
        // mostrava "Nota Rejeitada. Status: erro_autorizacao" — sem dizer que o
        // motivo real era NCM inválido, sem apontar o que corrigir na venda.
        mensagem.Should().Contain("NCM inválido");
    }

    [Fact(DisplayName = "NfceEmissionService — rejeição sem mensagem_sefaz cai pro texto antigo (Status: X), não quebra")]
    public async Task NfceEmissionService_RejeitadaSemMensagemSefaz_CaiNoFallback()
    {
        var httpMock = new Mock<IFocusNfeHttpClient>();
        httpMock.Setup(h => h.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(Result.Ok("{\"status\":\"erro_autorizacao\"}"));

        var svc = new NfceEmissionService(httpMock.Object);
        var (sucesso, mensagem, _, _, _, _) = await svc.EmitirNfceAsync("ref-1", new FocusNfceRequest(), "token-fake", isProducao: true);

        sucesso.Should().BeFalse();
        mensagem.Should().Contain("Status: erro_autorizacao");
    }

    [Fact(DisplayName = "NfeEmissionService (A4) — rejeição traz a mensagem_sefaz de verdade (achado testando em produção: CFOP/CSOSN, NCM)")]
    public async Task NfeEmissionService_Rejeitada_TrazMensagemSefaz()
    {
        var httpMock = new Mock<IFocusNfeHttpClient>();
        httpMock.Setup(h => h.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(Result.Ok(
                "{\"status\":\"erro_autorizacao\",\"mensagem_sefaz\":\"CFOP nao permitido para o CSOSN informado. [nItem:1]\"}"));

        var svc = new NfeEmissionService(httpMock.Object);
        var (sucesso, mensagem, _, _, _, _) = await svc.EmitirNfeA4Async("ref-1", new FocusNfceRequest(), "token-fake", isProducao: true);

        sucesso.Should().BeFalse();
        mensagem.Should().Contain("CFOP nao permitido para o CSOSN informado");
        mensagem.Should().Contain("[nItem:1]");
    }

    [Fact(DisplayName = "NfeEmissionService (A4) — extrai chave_nfe/numero da resposta autorizada de primeira")]
    public async Task NfeEmissionService_Autorizada_ExtraiChaveENumero()
    {
        var httpMock = new Mock<IFocusNfeHttpClient>();
        httpMock.Setup(h => h.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(Result.Ok(
                "{\"status\":\"autorizado\",\"caminho_danfe\":\"/nfe.pdf\",\"caminho_xml_nota_fiscal\":\"/nfe.xml\"," +
                "\"chave_nfe\":\"41260912820608000141550010000003121389908320\",\"numero\":\"312\"}"));

        var svc = new NfeEmissionService(httpMock.Object);
        var (sucesso, _, _, _, chave, numero) = await svc.EmitirNfeA4Async("ref-2", new FocusNfceRequest(), "token-fake", isProducao: true);

        sucesso.Should().BeTrue();
        chave.Should().Be("41260912820608000141550010000003121389908320");
        numero.Should().Be("312");
    }

    [Fact(DisplayName = "NfeEmissionService (A4) — extrai chave_nfe/numero no caminho de reconsulta (processando_autorizacao)")]
    public async Task NfeEmissionService_ProcessandoDepoisAutorizada_ExtraiChaveENumeroDaConsulta()
    {
        var httpMock = new Mock<IFocusNfeHttpClient>();
        httpMock.Setup(h => h.PostAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(Result.Ok("{\"status\":\"processando_autorizacao\"}"));
        httpMock.Setup(h => h.GetAsync(It.IsAny<string>()))
            .ReturnsAsync(Result.Ok(
                "{\"status\":\"autorizado\",\"caminho_danfe\":\"/nfe.pdf\",\"caminho_xml_nota_fiscal\":\"/nfe.xml\"," +
                "\"chave_nfe\":\"41260912820608000141550010000003121389908321\",\"numero\":\"313\"}"));

        var svc = new NfeEmissionService(httpMock.Object);
        var (sucesso, _, _, _, chave, numero) = await svc.EmitirNfeA4Async("ref-3", new FocusNfceRequest(), "token-fake", isProducao: true);

        sucesso.Should().BeTrue();
        chave.Should().Be("41260912820608000141550010000003121389908321");
        numero.Should().Be("313");
    }
}