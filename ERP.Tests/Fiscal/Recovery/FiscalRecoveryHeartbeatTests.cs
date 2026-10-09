using ERP.Application.Fiscal.Recovery;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>4A-6b (K4): o batimento do worker da recuperacao e a carencia depois de uma subida.</summary>
public class FiscalRecoveryHeartbeatTests
{
    private static readonly TimeSpan Janela = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Carencia = TimeSpan.FromMinutes(5);

    private sealed class Relogio
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        public Func<DateTimeOffset> Ler => () => Agora;
    }

    [Fact(DisplayName = "Recem-criado e sem nenhum batimento: esta na carencia (nao alerta logo depois de uma subida)")]
    public void RecemCriado_EstaNaCarencia()
    {
        var relogio = new Relogio();
        var batimento = new FiscalRecoveryHeartbeat(relogio.Ler);

        relogio.Agora = relogio.Agora.AddMinutes(4);

        batimento.HaBatimentoRecente(Janela, Carencia).Should().BeTrue();
    }

    [Fact(DisplayName = "Passada a carencia e SEM nenhum batimento: nao ha batimento recente (e o caso do worker ausente)")]
    public void AposCarencia_SemBatimento_NaoHa()
    {
        var relogio = new Relogio();
        var batimento = new FiscalRecoveryHeartbeat(relogio.Ler);

        relogio.Agora = relogio.Agora.AddMinutes(6);

        batimento.HaBatimentoRecente(Janela, Carencia).Should().BeFalse();
        batimento.UltimoCiclo.Should().BeNull();
        batimento.TotalDeCiclos.Should().Be(0);
    }

    [Fact(DisplayName = "Batimento dentro da janela: ha batimento recente")]
    public void BatimentoDentroDaJanela()
    {
        var relogio = new Relogio();
        var batimento = new FiscalRecoveryHeartbeat(relogio.Ler);
        relogio.Agora = relogio.Agora.AddMinutes(20);

        batimento.RegistrarCiclo();
        relogio.Agora = relogio.Agora.AddMinutes(4);

        batimento.HaBatimentoRecente(Janela, Carencia).Should().BeTrue();
    }

    [Fact(DisplayName = "Batimento mais antigo que a janela (e fora da carencia): nao ha batimento recente (o worker parou)")]
    public void BatimentoAntigo_NaoHa()
    {
        var relogio = new Relogio();
        var batimento = new FiscalRecoveryHeartbeat(relogio.Ler);
        relogio.Agora = relogio.Agora.AddMinutes(20);
        batimento.RegistrarCiclo();

        relogio.Agora = relogio.Agora.AddMinutes(6);

        batimento.HaBatimentoRecente(Janela, Carencia).Should().BeFalse();
    }

    [Fact(DisplayName = "Um novo batimento volta a contar como recente")]
    public void NovoBatimento_VoltaAContar()
    {
        var relogio = new Relogio();
        var batimento = new FiscalRecoveryHeartbeat(relogio.Ler);
        relogio.Agora = relogio.Agora.AddMinutes(30);
        batimento.HaBatimentoRecente(Janela, Carencia).Should().BeFalse();

        batimento.RegistrarCiclo();

        batimento.HaBatimentoRecente(Janela, Carencia).Should().BeTrue();
    }

    [Fact(DisplayName = "RegistrarCiclo guarda o instante do ultimo ciclo e conta os ciclos")]
    public void RegistrarCiclo_GuardaInstanteEContagem()
    {
        var relogio = new Relogio();
        var batimento = new FiscalRecoveryHeartbeat(relogio.Ler);

        batimento.RegistrarCiclo();
        relogio.Agora = relogio.Agora.AddSeconds(60);
        batimento.RegistrarCiclo();

        batimento.TotalDeCiclos.Should().Be(2);
        batimento.UltimoCiclo.Should().Be(relogio.Agora);
    }
}
