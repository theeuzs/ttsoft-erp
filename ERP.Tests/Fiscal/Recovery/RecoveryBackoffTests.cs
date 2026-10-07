using ERP.Application.Fiscal.Recovery;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

public class RecoveryBackoffTests
{
    private static readonly IReadOnlyList<TimeSpan> Padrao = new RecoveryPolicyOptions().Backoff;

    [Theory(DisplayName = "Sequencia aprovada: 2, 2, 4, 8, 15, 30 min, depois teto de 30")]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 15)]
    [InlineData(6, 30)]
    [InlineData(7, 30)]
    [InlineData(10, 30)]
    [InlineData(1000, 30)]
    public void Sequencia_ComTeto(int falhas, int minutosEsperados)
    {
        RecoveryBackoff.Espera(falhas, Padrao).Should().Be(TimeSpan.FromMinutes(minutosEsperados));
    }

    [Fact(DisplayName = "Sequencia personalizada repete o ultimo valor")]
    public void SequenciaPersonalizada()
    {
        var seq = new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20) };

        RecoveryBackoff.Espera(1, seq).Should().Be(TimeSpan.FromSeconds(10));
        RecoveryBackoff.Espera(2, seq).Should().Be(TimeSpan.FromSeconds(20));
        RecoveryBackoff.Espera(3, seq).Should().Be(TimeSpan.FromSeconds(20));
    }

    [Theory(DisplayName = "Falhas menores que 1 lancam (o chamador incrementa antes)")]
    [InlineData(0)]
    [InlineData(-1)]
    public void FalhasInvalidas_Lancam(int falhas)
    {
        var act = () => RecoveryBackoff.Espera(falhas, Padrao);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Sequencia vazia lanca erro claro")]
    public void SequenciaVazia_Lanca()
    {
        var act = () => RecoveryBackoff.Espera(1, Array.Empty<TimeSpan>());

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Sequencia nula lanca ArgumentNullException")]
    public void SequenciaNula_Lanca()
    {
        var act = () => RecoveryBackoff.Espera(1, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
