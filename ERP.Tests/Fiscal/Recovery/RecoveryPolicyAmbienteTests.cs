using ERP.Application.Fiscal.Focus;
using ERP.Application.Fiscal.Recovery;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// Trava de ambiente, Estagio 1: a decisao da politica quando a guarda nao autoriza. O QUE PROVA: que a decisao NUNCA implica HTTP, que a
/// pendencia vai para AguardandoCorrecao com o motivo certo e os contadores preservados, que IntervencaoManual continua terminal e que
/// o texto cabe no limite do store. O QUE NAO PROVA: que o orquestrador a chama (Estagio 2) nem a gravacao no banco.
/// </summary>
public class RecoveryPolicyAmbienteTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly RecoveryPolicyOptions Padrao = new();

    private static PendenciaSituacao Pend(string estado = NfePendenteEstados.Ativa, int transitorias = 0, int desconhecidas = 0) =>
        new(FocusDocumentType.Nfce, estado, Agora.AddMinutes(-10), transitorias, desconhecidas, null);

    private static AvaliacaoAmbiente HomologacaoParaProducao => PendenciaAmbienteGuard.Avaliar(false, true);

    private static AvaliacaoAmbiente ProducaoParaHomologacao => PendenciaAmbienteGuard.Avaliar(true, false);

    private static AvaliacaoAmbiente Legado(bool atualProducao) => PendenciaAmbienteGuard.Avaliar(null, atualProducao);

    // ── A decisao ────────────────────────────────────────────────────────

    [Theory(DisplayName = "Divergencia: AguardandoCorrecao agendado +30 min, contadores PRESERVADOS, sem regeneracao")]
    [InlineData(NfePendenteEstados.Ativa)]
    [InlineData(NfePendenteEstados.AguardandoCorrecao)]
    public void Divergencia_AguardaCorrecao_PreservandoContadores(string estadoAtual)
    {
        var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(
            Pend(estadoAtual, transitorias: 2, desconhecidas: 1), HomologacaoParaProducao, Agora, Padrao);

        d.Acao.Should().Be(RecoveryAction.AguardarCorrecao);
        d.NovoEstado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        d.ProximaTentativaEm.Should().Be(Agora.AddMinutes(30));
        d.FalhasTransitoriasSeguidas.Should().Be(2, "nao houve resultado da Focus: nada a somar nem a zerar");
        d.FalhasDesconhecidasSeguidas.Should().Be(1);
        d.Regeneracao.Should().BeNull();
    }

    [Fact(DisplayName = "Divergencia: o motivo nomeia o problema, os dois ambientes e a ausencia de chamada, e comeca pelo que importa")]
    public void Divergencia_MotivoClaro()
    {
        var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(), HomologacaoParaProducao, Agora, Padrao);

        d.Motivo.Should().StartWith("AmbienteDivergente:");
        d.Motivo.Should().Contain("pendencia criada em Homologacao");
        d.Motivo.Should().Contain("configurado agora: Producao");
        d.Motivo.Should().Contain("Nenhuma chamada a Focus");
        d.Motivo.Should().Contain("so retoma quando a guarda confirmar o mesmo ambiente");
    }

    [Fact(DisplayName = "Producao -> homologacao: mesma decisao, com o motivo trocado")]
    public void ProducaoParaHomologacao_MesmaDecisao()
    {
        var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(), ProducaoParaHomologacao, Agora, Padrao);

        d.Acao.Should().Be(RecoveryAction.AguardarCorrecao);
        d.Motivo.Should().Contain("pendencia criada em Producao").And.Contain("configurado agora: Homologacao");
    }

    [Theory(DisplayName = "Ambiente DESCONHECIDO (legado): mesma decisao segura, pedindo classificacao manual")]
    [InlineData(true)]
    [InlineData(false)]
    public void Desconhecido_AguardaCorrecao_ComPedidoDeClassificacao(bool atualProducao)
    {
        var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(transitorias: 1), Legado(atualProducao), Agora, Padrao);

        d.Acao.Should().Be(RecoveryAction.AguardarCorrecao);
        d.NovoEstado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        d.ProximaTentativaEm.Should().Be(Agora.AddMinutes(30));
        d.FalhasTransitoriasSeguidas.Should().Be(1);
        d.Motivo.Should().StartWith("AmbienteDesconhecido:");
        d.Motivo.Should().Contain("sem ambiente de origem registrado");
        d.Motivo.Should().Contain("classificar manualmente");
    }

    [Fact(DisplayName = "A espera respeita RecoveryPolicyOptions.EsperaAguardandoCorrecao")]
    public void RespeitaAEsperaConfigurada()
    {
        var opcoes = new RecoveryPolicyOptions { EsperaAguardandoCorrecao = TimeSpan.FromMinutes(10) };

        var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(), HomologacaoParaProducao, Agora, opcoes);

        d.ProximaTentativaEm.Should().Be(Agora.AddMinutes(10));
    }

    // ── Nunca implica HTTP ───────────────────────────────────────────────

    [Fact(DisplayName = "NUNCA implica enviar nem consultar: para qualquer estado e qualquer nao-autorizacao, a acao e AguardarCorrecao ou IntervencaoManual")]
    public void NuncaImplicaHttp()
    {
        var estados = new[] { NfePendenteEstados.Ativa, NfePendenteEstados.AguardandoCorrecao, NfePendenteEstados.IntervencaoManual };
        var avaliacoes = new[] { HomologacaoParaProducao, ProducaoParaHomologacao, Legado(true), Legado(false),
            PendenciaAmbienteGuard.Avaliar(false, false, true), PendenciaAmbienteGuard.Avaliar(false, true, false) };

        foreach (var estado in estados)
        foreach (var avaliacao in avaliacoes)
        {
            var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(estado), avaliacao, Agora, Padrao);

            d.Acao.Should().BeOneOf(RecoveryAction.AguardarCorrecao, RecoveryAction.IntervencaoManual);
            d.Acao.Should().NotBe(RecoveryAction.ReenviarComDataRegenerada);
            d.Acao.Should().NotBe(RecoveryAction.ReenviarPayloadOriginal);
            d.Acao.Should().NotBe(RecoveryAction.ConsultarEReconciliar);
            d.Acao.Should().NotBe(RecoveryAction.PersistirAutorizadaERemover);
            d.Acao.Should().NotBe(RecoveryAction.MarcarVendaRejeitadaERemover);
            d.Regeneracao.Should().BeNull();
        }
    }

    [Fact(DisplayName = "IntervencaoManual continua TERMINAL: a trava nunca tira a pendencia desse estado")]
    public void IntervencaoManual_ContinuaTerminal()
    {
        var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(
            Pend(NfePendenteEstados.IntervencaoManual, transitorias: 3, desconhecidas: 3), HomologacaoParaProducao, Agora, Padrao);

        d.Acao.Should().Be(RecoveryAction.IntervencaoManual);
        d.NovoEstado.Should().Be(NfePendenteEstados.IntervencaoManual);
        d.ProximaTentativaEm.Should().BeNull();
        d.FalhasTransitoriasSeguidas.Should().Be(3);
        d.FalhasDesconhecidasSeguidas.Should().Be(3);
    }

    // ── Limite do store ──────────────────────────────────────────────────

    [Fact(DisplayName = "O motivo cabe no limite de UltimaDecisao do store, com a informacao importante no inicio")]
    public void Motivo_CabeNoLimiteDoStore()
    {
        var avaliacoes = new[] { HomologacaoParaProducao, ProducaoParaHomologacao, Legado(true), Legado(false) };

        foreach (var avaliacao in avaliacoes)
        {
            var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(), avaliacao, Agora, Padrao);

            d.Motivo.Length.Should().BeLessThanOrEqualTo(FiscalRecoveryStore.TamanhoMaximoUltimaDecisao);
            d.Motivo.Substring(0, Math.Min(60, d.Motivo.Length)).Should().MatchRegex("^Ambiente(Divergente|Desconhecido):");
        }
    }

    // ── Argumentos invalidos ─────────────────────────────────────────────

    [Fact(DisplayName = "Avaliacao COMPATIVEL e erro de uso: esta decisao so existe para nao-autorizacao")]
    public void AvaliacaoCompativel_Lanca()
    {
        Action act = () => RecoveryPolicy.DecidirDivergenciaDeAmbiente(
            Pend(), PendenciaAmbienteGuard.Avaliar(true, true), Agora, Padrao);

        act.Should().Throw<ArgumentException>().WithMessage("*compativel*");
    }

    [Fact(DisplayName = "Registro rotulado 'Compativel' mas inconsistente NAO e tratado como autorizado: vira divergencia")]
    public void RegistroInconsistente_TratadoComoDivergencia()
    {
        var forjada = new AvaliacaoAmbiente(ResultadoAmbiente.Compativel, true, false, false);

        var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(), forjada, Agora, Padrao);

        d.Acao.Should().Be(RecoveryAction.AguardarCorrecao);
        d.Motivo.Should().StartWith("AmbienteDivergente:");
    }

    [Fact(DisplayName = "Estado invalido e argumentos nulos sao rejeitados")]
    public void ArgumentosInvalidos()
    {
        Action estadoInvalido = () => RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend("Qualquer"), HomologacaoParaProducao, Agora, Padrao);
        estadoInvalido.Should().Throw<ArgumentException>().WithMessage("*Estado de pendencia invalido*");

        Action pendenciaNula = () => RecoveryPolicy.DecidirDivergenciaDeAmbiente(null!, HomologacaoParaProducao, Agora, Padrao);
        pendenciaNula.Should().Throw<ArgumentNullException>();

        Action avaliacaoNula = () => RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(), null!, Agora, Padrao);
        avaliacaoNula.Should().Throw<ArgumentNullException>();

        Action opcoesNulas = () => RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(), HomologacaoParaProducao, Agora, null!);
        opcoesNulas.Should().Throw<ArgumentNullException>();
    }

    // ── Configuracao ilegivel (Estagio 2) ────────────────────────────────

    private static AvaliacaoAmbiente Ilegivel => PendenciaAmbienteGuard.ConfiguracaoIlegivel(false, true);

    [Fact(DisplayName = "Configuracao ILEGIVEL: AguardandoCorrecao +30 min, contadores preservados, motivo AmbienteIndeterminado")]
    public void Ilegivel_AguardaCorrecao()
    {
        var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(transitorias: 2, desconhecidas: 1), Ilegivel, Agora, Padrao);

        d.Acao.Should().Be(RecoveryAction.AguardarCorrecao);
        d.NovoEstado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        d.ProximaTentativaEm.Should().Be(Agora.AddMinutes(30));
        d.FalhasTransitoriasSeguidas.Should().Be(2);
        d.FalhasDesconhecidasSeguidas.Should().Be(1);
        d.Motivo.Should().StartWith("AmbienteIndeterminado:").And.Contain("nao foi possivel ler a configuracao fiscal atual");
        d.Motivo.Length.Should().BeLessThanOrEqualTo(FiscalRecoveryStore.TamanhoMaximoUltimaDecisao);
    }

    [Fact(DisplayName = "Configuracao ILEGIVEL tambem NUNCA implica enviar nem consultar, em qualquer estado")]
    public void Ilegivel_NuncaImplicaHttp()
    {
        foreach (var estado in new[] { NfePendenteEstados.Ativa, NfePendenteEstados.AguardandoCorrecao, NfePendenteEstados.IntervencaoManual })
        {
            var d = RecoveryPolicy.DecidirDivergenciaDeAmbiente(Pend(estado), Ilegivel, Agora, Padrao);

            d.Acao.Should().BeOneOf(RecoveryAction.AguardarCorrecao, RecoveryAction.IntervencaoManual);
            d.Regeneracao.Should().BeNull();
        }
    }
}
