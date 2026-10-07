using ERP.Application.Fiscal.Focus;
using ERP.Application.Fiscal.Recovery;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Fiscal;

public class RecoveryPolicyTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private static readonly RecoveryPolicyOptions Padrao = new();

    private static RecoveryPolicyOptions D8Ligada(double horas = 2) =>
        new() { PermitirRegeneracaoDataEmissao = true, JanelaMaximaRegeneracaoHoras = horas };

    private static PendenciaSituacao Pend(
        FocusDocumentType tipo = FocusDocumentType.Nfce,
        string estado = NfePendenteEstados.Ativa,
        int transitorias = 0,
        int desconhecidas = 0,
        DateTimeOffset? entrouNaFilaEm = null,
        DateTimeOffset? dataEmissaoPayload = null) =>
        new(tipo, estado, entrouNaFilaEm ?? Agora.AddMinutes(-10), transitorias, desconhecidas, dataEmissaoPayload);

    private static FocusVerdict V(FocusVerdictKind kind, string? statusSefaz = null, string? detalhe = null) =>
        new(kind, statusSefaz, detalhe);

    private static RecoveryDecision Decidir(
        FocusVerdict v, PendenciaSituacao p, FocusOperation op = FocusOperation.Get, RecoveryPolicyOptions? o = null) =>
        RecoveryPolicy.Decidir(v, op, p, Agora, o ?? Padrao);

    // ── Autorizado ───────────────────────────────────────────────────────

    [Theory(DisplayName = "Autorizado: persiste e remove, em GET e POST; zera os contadores")]
    [InlineData(FocusOperation.Get)]
    [InlineData(FocusOperation.Post)]
    public void Autorizado(FocusOperation op)
    {
        var d = Decidir(V(FocusVerdictKind.Autorizado), Pend(transitorias: 4, desconhecidas: 2), op);

        d.Acao.Should().Be(RecoveryAction.PersistirAutorizadaERemover);
        d.NovoEstado.Should().BeNull();
        d.ProximaTentativaEm.Should().BeNull();
        d.FalhasTransitoriasSeguidas.Should().Be(0);
        d.FalhasDesconhecidasSeguidas.Should().Be(0);
    }

    // ── JaProcessado ─────────────────────────────────────────────────────

    [Fact(DisplayName = "JaProcessado (already_processed): consultar e reconciliar, NUNCA reenviar")]
    public void JaProcessado()
    {
        var d = Decidir(V(FocusVerdictKind.JaProcessado), Pend(transitorias: 3), FocusOperation.Post);

        d.Acao.Should().Be(RecoveryAction.ConsultarEReconciliar);
        d.NovoEstado.Should().Be(NfePendenteEstados.Ativa);
        d.ProximaTentativaEm.Should().Be(Agora);
        d.FalhasTransitoriasSeguidas.Should().Be(0);
    }

    // ── Processando / OperacaoPendente ───────────────────────────────────

    [Theory(DisplayName = "Processando e OperacaoPendente: aguardar 2 min, sem POST, contadores zerados")]
    [InlineData(FocusVerdictKind.Processando)]
    [InlineData(FocusVerdictKind.OperacaoPendente)]
    public void Processando_Aguarda(FocusVerdictKind kind)
    {
        var d = Decidir(V(kind), Pend(transitorias: 4, desconhecidas: 2, entrouNaFilaEm: Agora.AddHours(-1)));

        d.Acao.Should().Be(RecoveryAction.Aguardar);
        d.NovoEstado.Should().Be(NfePendenteEstados.Ativa);
        d.ProximaTentativaEm.Should().Be(Agora.AddMinutes(2));
        d.FalhasTransitoriasSeguidas.Should().Be(0, "processando nao e falha transitoria");
        d.FalhasDesconhecidasSeguidas.Should().Be(0);
    }

    [Fact(DisplayName = "Processando exatamente no limite de 6 h ainda aguarda")]
    public void Processando_NoLimite()
    {
        var d = Decidir(V(FocusVerdictKind.Processando), Pend(entrouNaFilaEm: Agora.AddHours(-6)));

        d.Acao.Should().Be(RecoveryAction.Aguardar);
    }

    [Fact(DisplayName = "Processando alem de 6 h vira intervencao manual")]
    public void Processando_AlemDoLimite()
    {
        var d = Decidir(V(FocusVerdictKind.Processando), Pend(entrouNaFilaEm: Agora.AddHours(-6).AddSeconds(-1)));

        d.Acao.Should().Be(RecoveryAction.IntervencaoManual);
        d.NovoEstado.Should().Be(NfePendenteEstados.IntervencaoManual);
        d.ProximaTentativaEm.Should().BeNull();
    }

    // ── Denegado ─────────────────────────────────────────────────────────

    [Fact(DisplayName = "Denegado: intervencao manual (sem reenvio, sem status novo em Sale)")]
    public void Denegado()
    {
        var d = Decidir(V(FocusVerdictKind.Denegado, "301"), Pend());

        d.Acao.Should().Be(RecoveryAction.IntervencaoManual);
        d.NovoEstado.Should().Be(NfePendenteEstados.IntervencaoManual);
    }

    // ── NaoEncontrado (404) ──────────────────────────────────────────────

    [Fact(DisplayName = "404 em NF-e: reenvia o payload ORIGINAL, sem mexer na data")]
    public void NaoEncontrado_Nfe_ReenviaOriginal()
    {
        var d = Decidir(V(FocusVerdictKind.NaoEncontrado), Pend(tipo: FocusDocumentType.Nfe), o: D8Ligada());

        d.Acao.Should().Be(RecoveryAction.ReenviarPayloadOriginal);
        d.NovoEstado.Should().Be(NfePendenteEstados.Ativa);
        d.Regeneracao.Should().BeNull("a regra de data e so de NFC-e");
    }

    [Fact(DisplayName = "404 em NFC-e com D8 desligada (padrao): intervencao manual, motivo Desligada")]
    public void NaoEncontrado_Nfce_D8Desligada()
    {
        var d = Decidir(V(FocusVerdictKind.NaoEncontrado), Pend(dataEmissaoPayload: Agora.AddMinutes(-30)));

        d.Acao.Should().Be(RecoveryAction.IntervencaoManual);
        d.Regeneracao.Should().Be(MotivoRegeneracao.Desligada);
    }

    [Fact(DisplayName = "404 em NFC-e com D8 ligada e dentro da janela: regenera so a data e reenvia")]
    public void NaoEncontrado_Nfce_D8Ligada_Regenera()
    {
        var d = Decidir(V(FocusVerdictKind.NaoEncontrado),
            Pend(dataEmissaoPayload: Agora.AddMinutes(-30)), o: D8Ligada(2));

        d.Acao.Should().Be(RecoveryAction.ReenviarComDataRegenerada);
        d.NovoEstado.Should().Be(NfePendenteEstados.Ativa);
        d.ProximaTentativaEm.Should().Be(Agora);
        d.Regeneracao.Should().Be(MotivoRegeneracao.Permitida);
    }

    [Fact(DisplayName = "404 em NFC-e com D8 ligada mas payload sem data valida: intervencao manual")]
    public void NaoEncontrado_Nfce_PayloadSemData()
    {
        var d = Decidir(V(FocusVerdictKind.NaoEncontrado), Pend(dataEmissaoPayload: null), o: D8Ligada());

        d.Acao.Should().Be(RecoveryAction.IntervencaoManual);
        d.Regeneracao.Should().Be(MotivoRegeneracao.PayloadSemDataValida);
    }

    [Fact(DisplayName = "404 em NFC-e fora da janela: intervencao manual")]
    public void NaoEncontrado_Nfce_ForaDaJanela()
    {
        var d = Decidir(V(FocusVerdictKind.NaoEncontrado),
            Pend(dataEmissaoPayload: Agora.AddHours(-3)), o: D8Ligada(2));

        d.Acao.Should().Be(RecoveryAction.IntervencaoManual);
        d.Regeneracao.Should().Be(MotivoRegeneracao.ForaDaJanela);
    }

    // ── RejeicaoFiscal ───────────────────────────────────────────────────

    [Fact(DisplayName = "704 de NFC-e vindo de GET: mesmo caminho do 404 (D8 decide)")]
    public void Rejeicao704_Nfce_Get_D8Decide()
    {
        var ligada = Decidir(V(FocusVerdictKind.RejeicaoFiscal, "704"),
            Pend(dataEmissaoPayload: Agora.AddMinutes(-30)), FocusOperation.Get, D8Ligada());
        var desligada = Decidir(V(FocusVerdictKind.RejeicaoFiscal, "704"),
            Pend(dataEmissaoPayload: Agora.AddMinutes(-30)), FocusOperation.Get);

        ligada.Acao.Should().Be(RecoveryAction.ReenviarComDataRegenerada);
        desligada.Acao.Should().Be(RecoveryAction.IntervencaoManual);
        desligada.Regeneracao.Should().Be(MotivoRegeneracao.Desligada);
    }

    [Fact(DisplayName = "GUARDA CONTRA LACO: 704 vindo de um POST (data ja atualizada) vai para intervencao, mesmo com D8 ligada")]
    public void Rejeicao704_Nfce_Post_NaoRegeneraDeNovo()
    {
        var d = Decidir(V(FocusVerdictKind.RejeicaoFiscal, "704"),
            Pend(dataEmissaoPayload: Agora.AddMinutes(-5)), FocusOperation.Post, D8Ligada());

        d.Acao.Should().Be(RecoveryAction.IntervencaoManual);
        d.Acao.Should().NotBe(RecoveryAction.ReenviarComDataRegenerada);
    }

    [Fact(DisplayName = "704 em NF-e nao tem regra de data: rejeicao definitiva (venda Rejeitada)")]
    public void Rejeicao704_Nfe_EhDefinitiva()
    {
        var d = Decidir(V(FocusVerdictKind.RejeicaoFiscal, "704"), Pend(tipo: FocusDocumentType.Nfe), o: D8Ligada());

        d.Acao.Should().Be(RecoveryAction.MarcarVendaRejeitadaERemover);
    }

    [Theory(DisplayName = "Rejeicao fiscal com outro codigo, e RejeicaoDefinitiva: venda Rejeitada e remove (como hoje)")]
    [InlineData(FocusVerdictKind.RejeicaoFiscal, "778")]
    [InlineData(FocusVerdictKind.RejeicaoFiscal, null)]
    [InlineData(FocusVerdictKind.RejeicaoDefinitiva, null)]
    public void RejeicaoDefinitiva(FocusVerdictKind kind, string? statusSefaz)
    {
        var d = Decidir(V(kind, statusSefaz), Pend(transitorias: 2), o: D8Ligada());

        d.Acao.Should().Be(RecoveryAction.MarcarVendaRejeitadaERemover);
        d.NovoEstado.Should().BeNull();
        d.FalhasTransitoriasSeguidas.Should().Be(0);
    }

    // ── Configuracao / requisicao ────────────────────────────────────────

    [Theory(DisplayName = "401/403: AguardandoCorrecao, consulta em 30 min, contadores zerados (de Ativa ou de AguardandoCorrecao)")]
    [InlineData(NfePendenteEstados.Ativa)]
    [InlineData(NfePendenteEstados.AguardandoCorrecao)]
    public void ErroDeConfiguracao(string estadoInicial)
    {
        var d = Decidir(V(FocusVerdictKind.ErroDeConfiguracao), Pend(estado: estadoInicial, transitorias: 3, desconhecidas: 2));

        d.Acao.Should().Be(RecoveryAction.AguardarCorrecao);
        d.NovoEstado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        d.ProximaTentativaEm.Should().Be(Agora.AddMinutes(30));
        d.FalhasTransitoriasSeguidas.Should().Be(0);
        d.FalhasDesconhecidasSeguidas.Should().Be(0);
    }

    [Fact(DisplayName = "400: intervencao manual")]
    public void ErroDeRequisicao()
    {
        var d = Decidir(V(FocusVerdictKind.ErroDeRequisicao), Pend());

        d.Acao.Should().Be(RecoveryAction.IntervencaoManual);
        d.NovoEstado.Should().Be(NfePendenteEstados.IntervencaoManual);
    }

    // ── Transitorio (backoff) ────────────────────────────────────────────

    [Theory(DisplayName = "Transitorio: contador +1 e espera do backoff (2, 2, 4, 8, 15, 30, 30...)")]
    [InlineData(0, 1, 2)]
    [InlineData(1, 2, 2)]
    [InlineData(2, 3, 4)]
    [InlineData(3, 4, 8)]
    [InlineData(4, 5, 15)]
    [InlineData(5, 6, 30)]
    [InlineData(6, 7, 30)]
    [InlineData(40, 41, 30)]
    public void Transitorio_Backoff(int antes, int depois, int minutos)
    {
        var d = Decidir(V(FocusVerdictKind.Transitorio), Pend(transitorias: antes));

        d.Acao.Should().Be(RecoveryAction.ManterComBackoff);
        d.NovoEstado.Should().Be(NfePendenteEstados.Ativa);
        d.FalhasTransitoriasSeguidas.Should().Be(depois);
        d.ProximaTentativaEm.Should().Be(Agora.AddMinutes(minutos));
    }

    [Fact(DisplayName = "Transitorio zera as falhas desconhecidas (resultado conhecido)")]
    public void Transitorio_ZeraDesconhecidas()
    {
        var d = Decidir(V(FocusVerdictKind.Transitorio), Pend(transitorias: 1, desconhecidas: 2));

        d.FalhasDesconhecidasSeguidas.Should().Be(0);
        d.FalhasTransitoriasSeguidas.Should().Be(2);
    }

    // ── Desconhecido (K = 3) ─────────────────────────────────────────────

    [Theory(DisplayName = "Desconhecido: #1 e #2 seguem Ativas (2 min); #3 vai para intervencao manual")]
    [InlineData(0, RecoveryAction.Aguardar, NfePendenteEstados.Ativa, 1)]
    [InlineData(1, RecoveryAction.Aguardar, NfePendenteEstados.Ativa, 2)]
    [InlineData(2, RecoveryAction.IntervencaoManual, NfePendenteEstados.IntervencaoManual, 3)]
    public void Desconhecido_K3(int antes, RecoveryAction acaoEsperada, string estadoEsperado, int contadorDepois)
    {
        var d = Decidir(V(FocusVerdictKind.Desconhecido, detalhe: "HTTP 418"), Pend(transitorias: 3, desconhecidas: antes));

        d.Acao.Should().Be(acaoEsperada);
        d.NovoEstado.Should().Be(estadoEsperado);
        d.FalhasDesconhecidasSeguidas.Should().Be(contadorDepois);
        d.FalhasTransitoriasSeguidas.Should().Be(0, "resultado desconhecido nao e transitorio");

        if (acaoEsperada == RecoveryAction.Aguardar)
            d.ProximaTentativaEm.Should().Be(Agora.AddMinutes(2));
        else
            d.ProximaTentativaEm.Should().BeNull();
    }

    // ── Contadores: a regra, explicita, para TODOS os vereditos ──────────

    [Fact(DisplayName = "Contadores: transitorias +1 so em Transitorio; desconhecidas +1 so em Desconhecido; o resto zera")]
    public void Contadores_RegraParaTodosOsVereditos()
    {
        foreach (var kind in Enum.GetValues<FocusVerdictKind>())
        {
            var d = Decidir(V(kind), Pend(transitorias: 4, desconhecidas: 1));

            var transitoriasEsperadas = kind == FocusVerdictKind.Transitorio ? 5 : 0;
            var desconhecidasEsperadas = kind == FocusVerdictKind.Desconhecido ? 2 : 0;

            d.FalhasTransitoriasSeguidas.Should().Be(transitoriasEsperadas, $"veredito {kind}");
            d.FalhasDesconhecidasSeguidas.Should().Be(desconhecidasEsperadas, $"veredito {kind}");
        }
    }

    // ── Estados ──────────────────────────────────────────────────────────

    [Theory(DisplayName = "Quem estava em AguardandoCorrecao volta para Ativa quando o resultado deixa de ser 401/403")]
    [InlineData(FocusVerdictKind.Processando)]
    [InlineData(FocusVerdictKind.Transitorio)]
    [InlineData(FocusVerdictKind.Desconhecido)]
    [InlineData(FocusVerdictKind.JaProcessado)]
    public void AguardandoCorrecao_VoltaParaAtiva(FocusVerdictKind kind)
    {
        var d = Decidir(V(kind), Pend(estado: NfePendenteEstados.AguardandoCorrecao));

        d.NovoEstado.Should().Be(NfePendenteEstados.Ativa);
    }

    [Fact(DisplayName = "AguardandoCorrecao + 404 de NF-e: reenvia e volta para Ativa")]
    public void AguardandoCorrecao_NaoEncontradoNfe_VoltaParaAtiva()
    {
        var d = Decidir(V(FocusVerdictKind.NaoEncontrado),
            Pend(tipo: FocusDocumentType.Nfe, estado: NfePendenteEstados.AguardandoCorrecao));

        d.Acao.Should().Be(RecoveryAction.ReenviarPayloadOriginal);
        d.NovoEstado.Should().Be(NfePendenteEstados.Ativa);
    }

    [Fact(DisplayName = "IntervencaoManual e TERMINAL: qualquer veredito mantem o estado e os contadores")]
    public void IntervencaoManual_Terminal()
    {
        foreach (var kind in Enum.GetValues<FocusVerdictKind>())
        {
            foreach (var op in new[] { FocusOperation.Get, FocusOperation.Post })
            {
                var d = Decidir(V(kind, "704"),
                    Pend(estado: NfePendenteEstados.IntervencaoManual, transitorias: 3, desconhecidas: 2), op, D8Ligada());

                d.Acao.Should().Be(RecoveryAction.IntervencaoManual, $"{kind}/{op}");
                d.NovoEstado.Should().Be(NfePendenteEstados.IntervencaoManual, $"{kind}/{op}");
                d.ProximaTentativaEm.Should().BeNull($"{kind}/{op}");
                d.FalhasTransitoriasSeguidas.Should().Be(3, $"{kind}/{op}");
                d.FalhasDesconhecidasSeguidas.Should().Be(2, $"{kind}/{op}");
            }
        }
    }

    // ── Cobertura e validacao de entrada ─────────────────────────────────

    [Fact(DisplayName = "Todo FocusVerdictKind e tratado: decisao valida, motivo preenchido e citando o veredito")]
    public void TodosOsVereditosSaoTratados()
    {
        foreach (var kind in Enum.GetValues<FocusVerdictKind>())
        {
            var d = Decidir(V(kind), Pend());

            Enum.IsDefined(d.Acao).Should().BeTrue($"veredito {kind}");
            d.Motivo.Should().NotBeNullOrWhiteSpace($"veredito {kind}");
            d.Motivo.Should().Contain(kind.ToString(), $"o motivo deve dizer de qual veredito veio ({kind})");
        }
    }

    [Theory(DisplayName = "Estado de pendencia invalido lanca ArgumentException (dado corrompido nao e adivinhado)")]
    [InlineData("")]
    [InlineData("ativa")]
    [InlineData("Processando")]
    public void EstadoInvalido_Lanca(string estado)
    {
        var act = () => Decidir(V(FocusVerdictKind.Autorizado), Pend(estado: estado));

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Argumentos nulos lancam ArgumentNullException")]
    public void ArgumentosNulos()
    {
        var p = Pend();
        var v = V(FocusVerdictKind.Autorizado);

        ((Action)(() => RecoveryPolicy.Decidir(null!, FocusOperation.Get, p, Agora, Padrao))).Should().Throw<ArgumentNullException>();
        ((Action)(() => RecoveryPolicy.Decidir(v, FocusOperation.Get, null!, Agora, Padrao))).Should().Throw<ArgumentNullException>();
        ((Action)(() => RecoveryPolicy.Decidir(v, FocusOperation.Get, p, Agora, null!))).Should().Throw<ArgumentNullException>();
    }
}
