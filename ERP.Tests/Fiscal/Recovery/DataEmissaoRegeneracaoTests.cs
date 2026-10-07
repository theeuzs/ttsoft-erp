using ERP.Application.Fiscal.Recovery;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

public class DataEmissaoRegeneracaoTests
{
    private static readonly TimeSpan Brasil = TimeSpan.FromHours(-3);

    // 08/10/2026 09:00 -03:00 (= 12:00Z)
    private static readonly DateTimeOffset Original = new(2026, 10, 8, 9, 0, 0, Brasil);

    private static RecoveryPolicyOptions Ligada(double horas = 2) =>
        new() { PermitirRegeneracaoDataEmissao = true, JanelaMaximaRegeneracaoHoras = horas };

    // ── ParseDataEmissao ─────────────────────────────────────────────────

    [Fact(DisplayName = "Parse: formato do FusoBrasilHelper, com offset, e lido por inteiro")]
    public void Parse_Valido()
    {
        var r = DataEmissaoRegeneracao.ParseDataEmissao("2026-10-07T21:38:20-03:00");

        r.Should().Be(new DateTimeOffset(2026, 10, 7, 21, 38, 20, Brasil));
        r!.Value.Offset.Should().Be(Brasil);
    }

    [Theory(DisplayName = "Parse: estrito. Sem offset, com Z, vazio ou lixo devolvem nulo")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026-10-07")]
    [InlineData("2026-10-07T21:38:20")]        // sem offset: o .NET assumiria o fuso do servidor
    [InlineData("2026-10-07T21:38:20Z")]
    [InlineData("07/10/2026 21:38:20 -03:00")]
    [InlineData("lixo")]
    public void Parse_Invalido(string? texto)
    {
        DataEmissaoRegeneracao.ParseDataEmissao(texto).Should().BeNull();
    }

    // ── Avaliar: cada condicao isolada ───────────────────────────────────

    [Fact(DisplayName = "Padrao (desligada): negado com motivo Desligada")]
    public void Desligada_Padrao()
    {
        DataEmissaoRegeneracao.Avaliar(Original, Original.AddMinutes(10), new RecoveryPolicyOptions())
            .Should().Be(MotivoRegeneracao.Desligada);
    }

    [Fact(DisplayName = "Desligada mesmo com janela configurada")]
    public void Desligada_ComJanela()
    {
        var o = new RecoveryPolicyOptions { PermitirRegeneracaoDataEmissao = false, JanelaMaximaRegeneracaoHoras = 2 };

        DataEmissaoRegeneracao.Avaliar(Original, Original.AddMinutes(10), o)
            .Should().Be(MotivoRegeneracao.Desligada);
    }

    [Theory(DisplayName = "Ligada sem janela valida (nula, zero ou negativa): JanelaNaoConfigurada")]
    [InlineData(null)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void JanelaNaoConfigurada(double? horas)
    {
        var o = new RecoveryPolicyOptions { PermitirRegeneracaoDataEmissao = true, JanelaMaximaRegeneracaoHoras = horas };

        DataEmissaoRegeneracao.Avaliar(Original, Original.AddMinutes(10), o)
            .Should().Be(MotivoRegeneracao.JanelaNaoConfigurada);
    }

    [Fact(DisplayName = "Payload sem data valida: PayloadSemDataValida")]
    public void PayloadSemData()
    {
        DataEmissaoRegeneracao.Avaliar(null, Original.AddMinutes(10), Ligada())
            .Should().Be(MotivoRegeneracao.PayloadSemDataValida);
    }

    [Theory(DisplayName = "Agora igual ou anterior ao original: DataNaoPosterior")]
    [InlineData(0)]
    [InlineData(-1)]
    public void DataNaoPosterior(int minutos)
    {
        DataEmissaoRegeneracao.Avaliar(Original, Original.AddMinutes(minutos), Ligada())
            .Should().Be(MotivoRegeneracao.DataNaoPosterior);
    }

    [Fact(DisplayName = "Idade acima da janela: ForaDaJanela")]
    public void ForaDaJanela()
    {
        DataEmissaoRegeneracao.Avaliar(Original, Original.AddHours(2).AddSeconds(1), Ligada(2))
            .Should().Be(MotivoRegeneracao.ForaDaJanela);
    }

    [Fact(DisplayName = "Idade exatamente igual a janela ainda e permitida")]
    public void NaJanelaExata_Permitida()
    {
        DataEmissaoRegeneracao.Avaliar(Original, Original.AddHours(2), Ligada(2))
            .Should().Be(MotivoRegeneracao.Permitida);
    }

    [Fact(DisplayName = "Dentro da janela, mesmo mes: Permitida")]
    public void DentroDaJanela_Permitida()
    {
        DataEmissaoRegeneracao.Avaliar(Original, Original.AddMinutes(30), Ligada(2))
            .Should().Be(MotivoRegeneracao.Permitida);
    }

    // ── Mes/ano ──────────────────────────────────────────────────────────

    [Fact(DisplayName = "Virada de mes (31/01 23:58 -> 01/02 00:03): CruzaMesOuAno")]
    public void CruzaMes()
    {
        var original = new DateTimeOffset(2026, 1, 31, 23, 58, 0, Brasil);

        DataEmissaoRegeneracao.Avaliar(original, original.AddMinutes(5), Ligada(1))
            .Should().Be(MotivoRegeneracao.CruzaMesOuAno);
    }

    [Fact(DisplayName = "Virada de ano (31/12 23:58 -> 01/01 00:03): CruzaMesOuAno")]
    public void CruzaAno()
    {
        var original = new DateTimeOffset(2025, 12, 31, 23, 58, 0, Brasil);

        DataEmissaoRegeneracao.Avaliar(original, original.AddMinutes(5), Ligada(1))
            .Should().Be(MotivoRegeneracao.CruzaMesOuAno);
    }

    [Fact(DisplayName = "O mes e medido no fuso do Brasil, nao em UTC: UTC diz que cruzou, Brasil diz que nao")]
    public void Mes_NoFusoDoBrasil_NaoCruza()
    {
        // 30/09 20:00 -03:00 (= 23:00Z de 30/09) -> agora = 01/10 01:30Z (= 22:30 -03:00 de 30/09)
        var original = new DateTimeOffset(2026, 9, 30, 20, 0, 0, Brasil);
        var agora = new DateTimeOffset(2026, 10, 1, 1, 30, 0, TimeSpan.Zero);

        DataEmissaoRegeneracao.Avaliar(original, agora, Ligada(3))
            .Should().Be(MotivoRegeneracao.Permitida);
    }

    [Fact(DisplayName = "O mes e medido no fuso do Brasil, nao em UTC: UTC diz que nao cruzou, Brasil diz que sim")]
    public void Mes_NoFusoDoBrasil_Cruza()
    {
        // 30/09 23:30 -03:00 (= 02:30Z de 01/10) -> agora = 01/10 03:30Z (= 00:30 -03:00 de 01/10)
        var original = new DateTimeOffset(2026, 9, 30, 23, 30, 0, Brasil);
        var agora = new DateTimeOffset(2026, 10, 1, 3, 30, 0, TimeSpan.Zero);

        DataEmissaoRegeneracao.Avaliar(original, agora, Ligada(2))
            .Should().Be(MotivoRegeneracao.CruzaMesOuAno);
    }

    // ── Ordem das checagens ──────────────────────────────────────────────

    [Fact(DisplayName = "Fora da janela E cruzando o mes: o motivo devolvido e ForaDaJanela (ordem documentada)")]
    public void Ordem_ForaDaJanelaAntesDeCruzaMes()
    {
        var original = new DateTimeOffset(2026, 1, 31, 23, 58, 0, Brasil);

        DataEmissaoRegeneracao.Avaliar(original, original.AddHours(5), Ligada(1))
            .Should().Be(MotivoRegeneracao.ForaDaJanela);
    }

    [Fact(DisplayName = "Opcoes nulas lancam ArgumentNullException")]
    public void OpcoesNulas()
    {
        var act = () => DataEmissaoRegeneracao.Avaliar(Original, Original.AddMinutes(1), null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
