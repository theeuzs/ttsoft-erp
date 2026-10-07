using ERP.Application.Fiscal.Focus;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

public class FocusChaveAcessoTests
{
    // Chaves reais (07/10/2026): modelo nas posicoes 20-21.
    private const string ChaveNfceReal = "41260912820608000141650010000033221640357603"; // 65
    private const string ChaveNfeReal  = "41260912820608000141550010000003141237502171"; // 55

    [Fact(DisplayName = "Modelo 65 na chave real de NFC-e e Nfce")]
    public void ChaveNfce_ModeloSessentaECinco()
    {
        FocusChaveAcesso.TipoDocumento(ChaveNfceReal).Should().Be(FocusDocumentType.Nfce);
    }

    [Fact(DisplayName = "Modelo 55 na chave real de NF-e (devolucao) e Nfe")]
    public void ChaveNfe_ModeloCinquentaECinco()
    {
        FocusChaveAcesso.TipoDocumento(ChaveNfeReal).Should().Be(FocusDocumentType.Nfe);
    }

    [Theory(DisplayName = "Chave invalida ou modelo desconhecido: nulo (nunca adivinha)")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("4126091282060800014165001000003322164035760")]                // 43 digitos
    [InlineData("412609128206080001416500100000332216403576033")]              // 45 digitos
    [InlineData("NFe41260912820608000141650010000033221640357603")]            // com prefixo: precisa vir normalizada
    [InlineData("4126091282060800014165001000003322164035760X")]               // nao numerica
    [InlineData("41260912820608000141570010000033221640357603")]               // modelo 57
    [InlineData("41260912820608000141000010000033221640357603")]               // modelo 00
    public void ChaveInvalida_Nulo(string? chave)
    {
        FocusChaveAcesso.TipoDocumento(chave).Should().BeNull();
    }
}
