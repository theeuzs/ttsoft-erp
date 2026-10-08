using ERP.Application.Fiscal.Focus;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>4A-5b, condicao 7: compatibilidade e codificacao da URL do POST de emissao de NFC-e.</summary>
public class FocusEndpointsEmissaoTests
{
    private const string Guid1 = "5c8078ba-ed08-4c08-87ff-0c08b4a9f758";

    [Fact(DisplayName = "GUID atual: a URL e identica ao texto que o NfceEmissionService montava antes")]
    public void Guid_IdenticoAoFormatoAntigo()
    {
        // Formato antigo (NfceEmissionService, linha 26): $"https://api.focusnfe.com.br/v2/nfce?ref={referencia}"
        FocusEndpoints.EmissaoNfce(Guid1, isProducao: true)
            .Should().Be($"https://api.focusnfe.com.br/v2/nfce?ref={Guid1}");

        FocusEndpoints.EmissaoNfce(Guid1, isProducao: false)
            .Should().Be($"https://homologacao.focusnfe.com.br/v2/nfce?ref={Guid1}");
    }

    [Fact(DisplayName = "Referencia de devolucao (devolucao-{guid}) passa intacta")]
    public void Devolucao_Intacta()
    {
        FocusEndpoints.EmissaoNfce("devolucao-9dea8006-9b5c-4c06-a30a-fead11e7040f", true)
            .Should().EndWith("/v2/nfce?ref=devolucao-9dea8006-9b5c-4c06-a30a-fead11e7040f");
    }

    [Theory(DisplayName = "Caracteres reservados sao escapados como dado (nenhum cria outro parametro, caminho ou fragmento)")]
    [InlineData("a b", "a%20b")]
    [InlineData("a/b", "a%2Fb")]
    [InlineData("a?b", "a%3Fb")]
    [InlineData("a&b=c", "a%26b%3Dc")]
    [InlineData("a#b", "a%23b")]
    [InlineData("a+b", "a%2Bb")]
    [InlineData("a%b", "a%25b")]
    [InlineData("ação", "a%C3%A7%C3%A3o")]
    [InlineData("x&ref=y", "x%26ref%3Dy")]
    public void Reservados_Escapados(string referencia, string esperadoNaUrl)
    {
        FocusEndpoints.EmissaoNfce(referencia, true)
            .Should().Be($"https://api.focusnfe.com.br/v2/nfce?ref={esperadoNaUrl}");
    }

    [Theory(DisplayName = "A URL tem sempre um unico '?' e um unico parametro ref, qualquer que seja a referencia")]
    [InlineData("a&b=c")]
    [InlineData("a?b")]
    [InlineData("a#b")]
    [InlineData("x&ref=y")]
    public void UmUnicoParametro(string referencia)
    {
        var url = FocusEndpoints.EmissaoNfce(referencia, true);

        url.Count(c => c == '?').Should().Be(1);
        url.Count(c => c == '&').Should().Be(0);
        url.Count(c => c == '#').Should().Be(0);
        url.Should().StartWith("https://api.focusnfe.com.br/v2/nfce?ref=");
    }

    [Fact(DisplayName = "Espacos nas pontas da referencia sao removidos, como na consulta")]
    public void Trim()
    {
        FocusEndpoints.EmissaoNfce("  " + Guid1 + "  ", true)
            .Should().Be($"https://api.focusnfe.com.br/v2/nfce?ref={Guid1}");
    }

    [Theory(DisplayName = "Referencia vazia, nula ou em branco lanca ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReferenciaVazia_Lanca(string? referencia)
    {
        var act = () => FocusEndpoints.EmissaoNfce(referencia!, true);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "A URL de emissao e a de consulta sao enderecos distintos (POST em /v2/nfce?ref=, GET em /v2/nfce/{ref})")]
    public void EmissaoEConsulta_SaoDistintas()
    {
        FocusEndpoints.EmissaoNfce(Guid1, true).Should().NotBe(FocusEndpoints.Consulta(FocusDocumentType.Nfce, Guid1, true));
        FocusEndpoints.Consulta(FocusDocumentType.Nfce, Guid1, true).Should().Be($"https://api.focusnfe.com.br/v2/nfce/{Guid1}");
    }
}
