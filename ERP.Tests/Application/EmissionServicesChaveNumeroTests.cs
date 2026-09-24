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
/// Módulo 5 (Fiscal) — achado testando a Etapa 1 em produção: Sale.NfceChave/
/// NfceNumero (e NotaFiscal.Chave/Numero) ficavam sempre NULL, mesmo com a
/// nota autorizada, porque NfceEmissionService/NfeEmissionService nunca
/// extraíam chave_nfe/numero da resposta da Focus — só liam status/
/// caminho_danfe/caminho_xml_nota_fiscal. Essas duas classes nunca tinham
/// teste direto (só mockadas em testes de FiscalService/NotaFiscalAvulsaService,
/// que não exercitam o parsing de verdade). Achado confirmado com dado real
/// de produção: consulta no banco mostrou NfceChave/NfceNumero NULL numa
/// venda com NfceStatusFocus='Autorizada' e URL de DANFE preenchida.
/// </summary>
public class EmissionServicesChaveNumeroTests
{
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
        var (sucesso, _, _, _, chave, numero) = await svc.EmitirNfceAsync("ref-1", new FocusNfceRequest(), "token-fake", isProducao: true);

        sucesso.Should().BeFalse();
        chave.Should().BeEmpty();
        numero.Should().BeEmpty();
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
