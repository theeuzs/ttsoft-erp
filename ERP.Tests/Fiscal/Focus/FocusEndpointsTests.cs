using ERP.Application.Fiscal.Focus;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

public class FocusEndpointsTests
{
    private const string Ref = "5c8078ba-ed08-4c08-87ff-0c08b4a9f758";

    [Theory(DisplayName = "Consulta: o tipo escolhe o endpoint; o ambiente escolhe o host")]
    [InlineData(FocusDocumentType.Nfce, true, "https://api.focusnfe.com.br/v2/nfce/" + Ref)]
    [InlineData(FocusDocumentType.Nfe, true, "https://api.focusnfe.com.br/v2/nfe/" + Ref)]
    [InlineData(FocusDocumentType.Nfce, false, "https://homologacao.focusnfe.com.br/v2/nfce/" + Ref)]
    [InlineData(FocusDocumentType.Nfe, false, "https://homologacao.focusnfe.com.br/v2/nfe/" + Ref)]
    public void Consulta_MontaUrl(FocusDocumentType tipo, bool producao, string esperada)
    {
        FocusEndpoints.Consulta(tipo, Ref, producao).Should().Be(esperada);
    }

    [Fact(DisplayName = "Referencia de devolucao (devolucao-{guid}) passa intacta")]
    public void Consulta_ReferenciaDeDevolucao()
    {
        var url = FocusEndpoints.Consulta(FocusDocumentType.Nfe, "devolucao-9dea8006-9b5c-4c06-a30a-fead11e7040f", true);

        url.Should().EndWith("/v2/nfe/devolucao-9dea8006-9b5c-4c06-a30a-fead11e7040f");
    }

    [Fact(DisplayName = "Referencia com caracteres especiais e escapada")]
    public void Consulta_EscapaReferencia()
    {
        FocusEndpoints.Consulta(FocusDocumentType.Nfce, "a b/c?d", true)
            .Should().EndWith("/v2/nfce/a%20b%2Fc%3Fd");
    }

    [Theory(DisplayName = "Referencia vazia ou nula lanca ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Consulta_ReferenciaVazia_Lanca(string? referencia)
    {
        var act = () => FocusEndpoints.Consulta(FocusDocumentType.Nfce, referencia!, true);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Tipo de documento invalido lanca, nao cai num endpoint padrao")]
    public void Consulta_TipoInvalido_Lanca()
    {
        var act = () => FocusEndpoints.Consulta((FocusDocumentType)0, Ref, true);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory(DisplayName = "FromTipoNota aceita NFCE/NFE sem diferenciar maiusculas e espacos")]
    [InlineData("NFCE", FocusDocumentType.Nfce)]
    [InlineData("nfce", FocusDocumentType.Nfce)]
    [InlineData(" NFCe ", FocusDocumentType.Nfce)]
    [InlineData("NFE", FocusDocumentType.Nfe)]
    [InlineData("nfe", FocusDocumentType.Nfe)]
    public void FromTipoNota_Valido(string texto, FocusDocumentType esperado)
    {
        FocusDocumentTypes.FromTipoNota(texto).Should().Be(esperado);
    }

    [Theory(DisplayName = "FromTipoNota com valor desconhecido lanca (nunca assume um tipo)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NFSE")]
    [InlineData("NFC-e")]
    [InlineData("X")]
    public void FromTipoNota_Invalido_Lanca(string? texto)
    {
        var act = () => FocusDocumentTypes.FromTipoNota(texto);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
