using ERP.Application.Fiscal.Focus;

namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Politica de recuperacao da fila fiscal (Etapa 4A-4). Funcao pura: nao
/// conhece banco, cliente HTTP, Focus nem relogio (recebe "agora").
///
/// O classificador (FocusResponseClassifier) diz O QUE A FOCUS DISSE; esta
/// politica diz O QUE O ERP PODE FAZER. Separados de proposito.
///
/// Regras de contadores:
///  - FalhasTransitoriasSeguidas: +1 SO em Transitorio; zera em qualquer
///    resultado nao transitorio.
///  - FalhasDesconhecidasSeguidas: +1 SO em Desconhecido; zera em qualquer
///    resultado conhecido.
///  - IntervencaoManual e terminal: a politica nunca sai dele sozinha.
///  - Qualquer veredito que mantenha a pendencia na fila (exceto
///    ErroDeConfiguracao) a devolve a Ativa: e assim que o token corrigido
///    "retoma sozinho".
/// </summary>
public static class RecoveryPolicy
{
    public static RecoveryDecision Decidir(
        FocusVerdict veredito,
        FocusOperation operacao,
        PendenciaSituacao pendencia,
        DateTimeOffset agora,
        RecoveryPolicyOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(veredito);
        ArgumentNullException.ThrowIfNull(pendencia);
        ArgumentNullException.ThrowIfNull(opcoes);

        if (!NfePendenteEstados.EhValido(pendencia.Estado))
        {
            throw new ArgumentException(
                $"Estado de pendencia invalido: '{pendencia.Estado}'.", nameof(pendencia));
        }

        if (pendencia.Estado == NfePendenteEstados.IntervencaoManual)
        {
            return new RecoveryDecision(
                RecoveryAction.IntervencaoManual,
                NfePendenteEstados.IntervencaoManual,
                null,
                pendencia.FalhasTransitoriasSeguidas,
                pendencia.FalhasDesconhecidasSeguidas,
                "Ja esta em intervencao manual; a politica nao sai desse estado sozinha.");
        }

        var nome = veredito.Kind.ToString();

        switch (veredito.Kind)
        {
            case FocusVerdictKind.Autorizado:
                return Remover(RecoveryAction.PersistirAutorizadaERemover,
                    $"{nome}: autorizada pela Focus.");

            case FocusVerdictKind.JaProcessado:
                return Seguir(RecoveryAction.ConsultarEReconciliar, agora, 0, 0,
                    $"{nome}: a Focus ja processou a ref; consultar e reconciliar, sem novo POST.");

            case FocusVerdictKind.Processando:
            case FocusVerdictKind.OperacaoPendente:
                return DecidirProcessando(pendencia, agora, opcoes, nome);

            case FocusVerdictKind.Denegado:
                return Manual($"{nome}: denegada pela SEFAZ; a ref esta consumida, sem reenvio automatico.", 0, 0);

            case FocusVerdictKind.NaoEncontrado:
                return DecidirReenvio(pendencia, agora, opcoes, nome);

            case FocusVerdictKind.RejeicaoFiscal:
                return DecidirRejeicaoFiscal(veredito, operacao, pendencia, agora, opcoes, nome);

            case FocusVerdictKind.RejeicaoDefinitiva:
                return Remover(RecoveryAction.MarcarVendaRejeitadaERemover,
                    $"{nome}: {veredito.Detalhe}");

            case FocusVerdictKind.ErroDeConfiguracao:
                return new RecoveryDecision(
                    RecoveryAction.AguardarCorrecao,
                    NfePendenteEstados.AguardandoCorrecao,
                    agora + opcoes.EsperaAguardandoCorrecao,
                    0, 0,
                    $"{nome}: credencial/configuracao; consulta espacada ate corrigir.");

            case FocusVerdictKind.ErroDeRequisicao:
                return Manual($"{nome}: requisicao invalida; nao se corrige com o passar do tempo.", 0, 0);

            case FocusVerdictKind.Transitorio:
                return DecidirTransitorio(pendencia, agora, opcoes, nome);

            case FocusVerdictKind.Desconhecido:
                return DecidirDesconhecido(veredito, pendencia, agora, opcoes, nome);

            default:
                return Manual($"{nome}: veredito nao tratado pela politica.", 0, 0);
        }
    }

    private static RecoveryDecision DecidirProcessando(
        PendenciaSituacao p, DateTimeOffset agora, RecoveryPolicyOptions o, string nome)
    {
        var idade = agora - p.EntrouNaFilaEm;

        if (idade > o.EsperaMaximaProcessando)
        {
            return Manual(
                $"{nome}: ainda em processamento depois de {(int)idade.TotalHours} h " +
                $"(limite {(int)o.EsperaMaximaProcessando.TotalHours} h); intervencao manual.",
                0, 0);
        }

        return Seguir(RecoveryAction.Aguardar, agora + o.EsperaProcessando, 0, 0,
            $"{nome}: em processamento na Focus; consulta de novo, sem POST.");
    }

    /// <summary>NaoEncontrado e 704 de NFC-e (vindo de GET) caem aqui.</summary>
    private static RecoveryDecision DecidirReenvio(
        PendenciaSituacao p, DateTimeOffset agora, RecoveryPolicyOptions o, string nome)
    {
        // A regra de data (704/5 min) e so de NFC-e. NF-e reenvia o payload
        // original, sem alterar nada.
        if (p.Tipo == FocusDocumentType.Nfe)
        {
            return Seguir(RecoveryAction.ReenviarPayloadOriginal, agora, 0, 0,
                $"{nome}: NF-e; reenvia o payload original sem alterar a data (a regra de data e so NFC-e).");
        }

        var motivo = DataEmissaoRegeneracao.Avaliar(p.DataEmissaoPayload, agora, o);

        if (motivo == MotivoRegeneracao.Permitida)
        {
            return Seguir(RecoveryAction.ReenviarComDataRegenerada, agora, 0, 0,
                $"{nome}: regenera somente DataEmissao e reenvia na mesma ref.", motivo);
        }

        return Manual($"{nome}: regeneracao de DataEmissao negada ({motivo}).", 0, 0, motivo);
    }

    private static RecoveryDecision DecidirRejeicaoFiscal(
        FocusVerdict v, FocusOperation operacao, PendenciaSituacao p,
        DateTimeOffset agora, RecoveryPolicyOptions o, string nome)
    {
        var ehTemporal704DeNfce = v.EhRejeicao704 && p.Tipo == FocusDocumentType.Nfce;

        if (!ehTemporal704DeNfce)
        {
            return Remover(RecoveryAction.MarcarVendaRejeitadaERemover,
                $"{nome}: rejeicao fiscal {v.StatusSefaz}: {v.Detalhe}");
        }

        // Guarda contra laco: se o 704 veio de um POST, o payload ja foi
        // enviado com a data atual (regenerada). Outro 704 nao se resolve
        // regenerando de novo (relogio errado? payload?): revisar.
        if (operacao == FocusOperation.Post)
        {
            return Manual(
                $"{nome}: 704 voltou depois de um POST com a data ja atualizada; " +
                "possivel erro de relogio ou de payload, revisar.",
                0, 0);
        }

        return DecidirReenvio(p, agora, o, nome + " 704");
    }

    private static RecoveryDecision DecidirTransitorio(
        PendenciaSituacao p, DateTimeOffset agora, RecoveryPolicyOptions o, string nome)
    {
        var n = p.FalhasTransitoriasSeguidas + 1;
        var espera = RecoveryBackoff.Espera(n, o.Backoff);

        return Seguir(RecoveryAction.ManterComBackoff, agora + espera, n, 0,
            $"{nome}: falha transitoria #{n}; proxima tentativa em {(int)espera.TotalMinutes} min.");
    }

    private static RecoveryDecision DecidirDesconhecido(
        FocusVerdict v, PendenciaSituacao p, DateTimeOffset agora, RecoveryPolicyOptions o, string nome)
    {
        var n = p.FalhasDesconhecidasSeguidas + 1;

        if (n >= o.LimiteDesconhecidos)
        {
            return Manual(
                $"{nome}: {n} resultados desconhecidos seguidos (limite {o.LimiteDesconhecidos}); " +
                $"intervencao manual. {v.Detalhe}",
                0, n);
        }

        return Seguir(RecoveryAction.Aguardar, agora + o.EsperaDesconhecido, 0, n,
            $"{nome}: resultado desconhecido #{n} de {o.LimiteDesconhecidos}; tenta de novo. {v.Detalhe}");
    }

    // ── Construtores de decisao ─────────────────────────────────────────

    /// <summary>A pendencia sai da fila (autorizada ou rejeitada definitivamente).</summary>
    private static RecoveryDecision Remover(RecoveryAction acao, string motivo) =>
        new(acao, null, null, 0, 0, motivo);

    /// <summary>A pendencia continua Ativa, elegivel a partir de "proxima".</summary>
    private static RecoveryDecision Seguir(
        RecoveryAction acao, DateTimeOffset proxima, int transitorias, int desconhecidas,
        string motivo, MotivoRegeneracao? regeneracao = null) =>
        new(acao, NfePendenteEstados.Ativa, proxima, transitorias, desconhecidas, motivo, regeneracao);

    private static RecoveryDecision Manual(
        string motivo, int transitorias, int desconhecidas, MotivoRegeneracao? regeneracao = null) =>
        new(RecoveryAction.IntervencaoManual, NfePendenteEstados.IntervencaoManual, null,
            transitorias, desconhecidas, motivo, regeneracao);

    /// <summary>
    /// Trava de ambiente (Estagio 1): a decisao quando a guarda NAO autoriza uma chamada (ambiente divergente ou desconhecido).
    /// NUNCA implica HTTP: so agenda a pendencia em AguardandoCorrecao (retoma sozinha quando a guarda voltar a confirmar o mesmo ambiente) e
    /// PRESERVA os contadores (nao houve resultado da Focus, entao nao ha o que zerar nem o que somar). IntervencaoManual continua terminal.
    /// O motivo comeca pelo que importa, porque o store corta UltimaDecisao pelo fim.
    /// </summary>
    public static RecoveryDecision DecidirDivergenciaDeAmbiente(
        PendenciaSituacao pendencia,
        AvaliacaoAmbiente avaliacao,
        DateTimeOffset agora,
        RecoveryPolicyOptions opcoes)
    {
        ArgumentNullException.ThrowIfNull(pendencia);
        ArgumentNullException.ThrowIfNull(avaliacao);
        ArgumentNullException.ThrowIfNull(opcoes);

        if (avaliacao.PodeProsseguir)
        {
            throw new ArgumentException(
                "A avaliacao e compativel; esta decisao so existe para ambiente divergente ou desconhecido.", nameof(avaliacao));
        }

        if (!NfePendenteEstados.EhValido(pendencia.Estado))
        {
            throw new ArgumentException(
                $"Estado de pendencia invalido: '{pendencia.Estado}'.", nameof(pendencia));
        }

        if (pendencia.Estado == NfePendenteEstados.IntervencaoManual)
        {
            return new RecoveryDecision(
                RecoveryAction.IntervencaoManual,
                NfePendenteEstados.IntervencaoManual,
                null,
                pendencia.FalhasTransitoriasSeguidas,
                pendencia.FalhasDesconhecidasSeguidas,
                "Ja esta em intervencao manual; a politica nao sai desse estado sozinha.");
        }

        var motivo = avaliacao.Resultado switch
        {
            ResultadoAmbiente.Desconhecido =>
                $"AmbienteDesconhecido: {avaliacao.Descricao}. Nenhuma chamada a Focus; classificar manualmente o ambiente de origem (CriadaEmProducao).",
            ResultadoAmbiente.Indeterminado =>
                $"AmbienteIndeterminado: {avaliacao.Descricao}. Nenhuma chamada a Focus; so retoma quando a configuracao puder ser lida e confirmar o mesmo ambiente.",
            _ =>
                $"AmbienteDivergente: {avaliacao.Descricao}. Nenhuma chamada a Focus; so retoma quando a guarda confirmar o mesmo ambiente."
        };

        return new RecoveryDecision(
            RecoveryAction.AguardarCorrecao,
            NfePendenteEstados.AguardandoCorrecao,
            agora + opcoes.EsperaAguardandoCorrecao,
            pendencia.FalhasTransitoriasSeguidas,
            pendencia.FalhasDesconhecidasSeguidas,
            motivo);
    }
}
