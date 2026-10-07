using ERP.Application.Fiscal.Recovery;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>Os padroes SAO decisoes aprovadas (07/10/2026); mudar um deles e mudar uma decisao.</summary>
public class RecoveryPolicyOptionsTests
{
    [Fact(DisplayName = "Padroes aprovados")]
    public void Padroes()
    {
        var o = new RecoveryPolicyOptions();

        o.Backoff.Select(b => (int)b.TotalMinutes).Should().Equal(2, 2, 4, 8, 15, 30);
        o.EsperaProcessando.Should().Be(TimeSpan.FromMinutes(2));
        o.EsperaMaximaProcessando.Should().Be(TimeSpan.FromHours(6));
        o.EsperaDesconhecido.Should().Be(TimeSpan.FromMinutes(2));
        o.LimiteDesconhecidos.Should().Be(3);
        o.EsperaAguardandoCorrecao.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact(DisplayName = "D8 nasce DESLIGADA e sem valor de N")]
    public void D8_DesligadaESemN()
    {
        var o = new RecoveryPolicyOptions();

        o.PermitirRegeneracaoDataEmissao.Should().BeFalse();
        o.JanelaMaximaRegeneracaoHoras.Should().BeNull();
    }

    [Theory(DisplayName = "EhValido: so os tres estados persistidos")]
    [InlineData("Ativa", true)]
    [InlineData("AguardandoCorrecao", true)]
    [InlineData("IntervencaoManual", true)]
    [InlineData("ativa", false)]
    [InlineData("Processando", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EstadosValidos(string? estado, bool esperado)
    {
        NfePendenteEstados.EhValido(estado).Should().Be(esperado);
    }
}
