using System.Globalization;
using ERP.Application.Fiscal.Focus;
using ERP.Application.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Entities;
using Serilog;

namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Orquestrador da recuperacao fiscal da fila (Etapa 4A-5d), NFC-e. Cada pendencia segue:
/// GET por ref -> classificador -> RecoveryPolicy -> execucao. Nunca faz POST sem um GET antes,
/// no maximo UM POST por pendencia por ciclo, e o payload novo esta no banco ANTES do POST.
///
/// TAXONOMIA DE FALHAS (condicao de revisao do G1):
///  - Cancelamento do CancellationToken: propaga. Nunca e contado.
///  - Falha de PERSISTENCIA (store, IFiscalService, ISaleService): nada e dado como concluido.
///    Nunca vira "resposta desconhecida da Focus" e nao conta para o limite K. Nao envia o POST
///    se o payload nao foi gravado; nao remove a pendencia se a autorizacao nao foi persistida.
///    A unica excecao e a falha ao persistir uma autorizacao JA confirmada pela Focus: a pendencia
///    fica e e reagendada com backoff (veredito Transitorio), porque repetir sem espera martelaria.
///  - Excecao de PROCESSAMENTO daquela nota (payload invalido, excecao inesperada do cliente):
///    vira veredito Desconhecido (a politica conta; K=3 leva a IntervencaoManual) e o ciclo segue.
///
/// DATA ORIGINAL: antes de mexer no payload, a data original e a nova vao para UltimaDecisao e
/// para o log estruturado (sem coluna nova). UltimaDecisao e sobrescrita pela decisao seguinte;
/// por isso o log e a segunda trilha.
///
/// LIMITES: sem exclusao mutua entre instancias (Etapa 5; hoje ha uma so) e sem alerta por tenant
/// (4A-6). O relogio e injetado como funcao: a data da NFC-e e gerada NA HORA da regeneracao, porque
/// um "agora" fixo no comeco de um ciclo longo ficaria velho (a tolerancia da SEFAZ e de 5 minutos).
/// </summary>
public sealed class NfePendenteRecoveryOrchestrator
{
    private const string TipoNfce = "NFCE";
    private const string FormatoDataEmissao = "yyyy-MM-dd'T'HH:mm:sszzz";
    private const int TamanhoMaximoResumoExcecao = 120;

    private readonly IFiscalRecoveryStore _store;
    private readonly IFocusReferenceClient _focus;
    private readonly IFiscalService _fiscal;
    private readonly ISaleService _vendas;
    private readonly RecoveryPolicyOptions _opcoes;
    private readonly Func<DateTimeOffset> _relogio;

    public NfePendenteRecoveryOrchestrator(
        IFiscalRecoveryStore store,
        IFocusReferenceClient focus,
        IFiscalService fiscal,
        ISaleService vendas,
        RecoveryPolicyOptions opcoes,
        Func<DateTimeOffset>? relogio = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _focus = focus ?? throw new ArgumentNullException(nameof(focus));
        _fiscal = fiscal ?? throw new ArgumentNullException(nameof(fiscal));
        _vendas = vendas ?? throw new ArgumentNullException(nameof(vendas));
        _opcoes = opcoes ?? throw new ArgumentNullException(nameof(opcoes));
        _relogio = relogio ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Processa as pendencias NFC-e elegiveis do tenant. Se a SELECAO da fila falhar, a excecao sobe
    /// (e infraestrutura: o worker registra e tenta no proximo ciclo). Falhas por pendencia ficam
    /// isoladas e o ciclo segue para a proxima.
    /// </summary>
    public async Task<RecoveryCycleResult> ProcessarTenantAsync(RecoveryTenantContext contexto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var resultado = new RecoveryCycleResult();

        var pendencias = await _store.ObterElegiveisAsync(TipoNfce, _relogio().UtcDateTime);
        resultado.Elegiveis = pendencias.Count;

        foreach (var pendencia in pendencias)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessarIsoladoAsync(pendencia, contexto, resultado, ct);
        }

        return resultado;
    }

    // ── Isolamento e taxonomia de falhas ─────────────────────────────────

    private async Task ProcessarIsoladoAsync(
        NfePendente pendencia, RecoveryTenantContext contexto, RecoveryCycleResult resultado, CancellationToken ct)
    {
        var e = new Execucao(pendencia, contexto, resultado, ct);

        try
        {
            await ProcessarPendenciaAsync(e);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (FalhaDePersistenciaException fx)
        {
            resultado.FalhasDePersistencia++;
            Log.Error(fx.InnerException ?? fx,
                "Recuperacao fiscal: falha de PERSISTENCIA ({Etapa}) na pendencia {PendenciaId} (ref {Referencia}). " +
                "Nada foi dado como concluido; a proxima rodada retoma.",
                fx.Etapa, pendencia.Id, pendencia.Referencia);
        }
        catch (Exception ex)
        {
            resultado.ErrosDeProcessamento++;
            Log.Error(ex,
                "Recuperacao fiscal: excecao de processamento na pendencia {PendenciaId} (ref {Referencia}); " +
                "tratada como resultado desconhecido.",
                pendencia.Id, pendencia.Referencia);

            await RegistrarExcecaoComoDesconhecidaAsync(e, ex);
        }
    }

    private async Task RegistrarExcecaoComoDesconhecidaAsync(Execucao e, Exception ex)
    {
        // Nada aqui pode escapar: isto roda dentro de um catch, e uma excecao nova derrubaria o ciclo.
        try
        {
            var veredito = new FocusVerdict(
                FocusVerdictKind.Desconhecido,
                Detalhe: $"Excecao no processamento ({ex.GetType().Name}): {Resumo(ex)}");

            var decisao = RecoveryPolicy.Decidir(veredito, FocusOperation.Get, e.Situacao, _relogio(), _opcoes);
            await GravarDecisaoAsync(e, ComNota(e, decisao));
        }
        catch (FalhaDePersistenciaException fx)
        {
            e.Resultado.FalhasDePersistencia++;
            Log.Error(fx.InnerException ?? fx,
                "Recuperacao fiscal: nao foi possivel gravar a decisao da excecao na pendencia {PendenciaId}.", e.Pendencia.Id);
        }
        catch (Exception falha)
        {
            Log.Error(falha,
                "Recuperacao fiscal: nao foi possivel decidir sobre a excecao na pendencia {PendenciaId}.", e.Pendencia.Id);
        }
    }

    // ── Fluxo de uma pendencia ───────────────────────────────────────────

    private async Task ProcessarPendenciaAsync(Execucao e)
    {
        // Token em branco: veredito sintetico, ZERO chamadas HTTP.
        if (string.IsNullOrWhiteSpace(e.Contexto.Token))
        {
            var sintetico = new FocusVerdict(
                FocusVerdictKind.ErroDeConfiguracao, Detalhe: "Token da Focus nao configurado para este tenant.");
            await DecidirEExecutarAsync(e, sintetico, FocusOperation.Get, resposta: null);
            return;
        }

        var resposta = await ConsultarAsync(e);
        var veredito = FocusResponseClassifier.Classify(resposta, FocusOperation.Get);
        await DecidirEExecutarAsync(e, veredito, FocusOperation.Get, resposta);
    }

    private async Task DecidirEExecutarAsync(Execucao e, FocusVerdict veredito, FocusOperation operacao, FocusResponse? resposta)
    {
        var decisao = RecoveryPolicy.Decidir(veredito, operacao, e.Situacao, _relogio(), _opcoes);
        await ExecutarAsync(e, veredito, resposta, decisao);
    }

    private async Task ExecutarAsync(Execucao e, FocusVerdict veredito, FocusResponse? resposta, RecoveryDecision decisao)
    {
        switch (decisao.Acao)
        {
            case RecoveryAction.PersistirAutorizadaERemover:
                await ConcluirAutorizacaoAsync(e, resposta!);
                return;

            case RecoveryAction.MarcarVendaRejeitadaERemover:
                await ConcluirRejeicaoAsync(e, veredito, resposta);
                return;

            case RecoveryAction.ReenviarComDataRegenerada:
                await RegenerarEReenviarAsync(e, decisao);
                return;

            case RecoveryAction.ConsultarEReconciliar:
                await ReconciliarAsync(e);
                return;

            case RecoveryAction.Aguardar:
            case RecoveryAction.ManterComBackoff:
            case RecoveryAction.AguardarCorrecao:
            case RecoveryAction.IntervencaoManual:
                await GravarDecisaoAsync(e, ComNota(e, decisao));
                return;

            default:
                // ReenviarPayloadOriginal e so de NF-e: nunca deveria chegar aqui para NFC-e.
                throw new InvalidOperationException($"Acao inesperada da politica para NFC-e: {decisao.Acao}.");
        }
    }

    // ── Autorizacao ──────────────────────────────────────────────────────

    private async Task ConcluirAutorizacaoAsync(Execucao e, FocusResponse resposta)
    {
        if (string.IsNullOrWhiteSpace(resposta.ChaveNfe) || string.IsNullOrWhiteSpace(resposta.CaminhoDanfe))
        {
            Log.Error("Recuperacao fiscal: resposta 'autorizado' sem chave normalizada ou sem caminho do DANFE na pendencia {PendenciaId} " +
                      "(ref {Referencia}); nada foi persistido.", e.Pendencia.Id, e.Pendencia.Referencia);

            var anomalia = new RecoveryDecision(
                RecoveryAction.IntervencaoManual, NfePendenteEstados.IntervencaoManual, null, 0, 0,
                $"Autorizado: a resposta da Focus veio sem chave de acesso normalizada ou sem caminho do DANFE; " +
                $"nada foi persistido (HTTP {resposta.HttpStatus}, chave bruta '{resposta.ChaveNfeBruta}').");

            await GravarDecisaoAsync(e, ComNota(e, anomalia));
            return;
        }

        var host = FocusEndpoints.Host(e.Contexto.IsProducao);
        var urlDanfe = host + resposta.CaminhoDanfe;
        var urlXml = string.IsNullOrWhiteSpace(resposta.CaminhoXmlNotaFiscal) ? string.Empty : host + resposta.CaminhoXmlNotaFiscal;

        try
        {
            await _fiscal.PersistirEmissaoAutorizadaAsync(
                e.Pendencia.VendaId, TipoNfce, urlDanfe, e.Contexto.AmbienteNome,
                e.Pendencia.Referencia, urlXml, resposta.ChaveNfe, resposta.Numero ?? string.Empty);
        }
        catch (Exception ex)
        {
            // A Focus JA autorizou, mas a persistencia local falhou: a pendencia NAO e removida.
            // Nao e resposta desconhecida da Focus. Reagenda com backoff; a proxima rodada faz
            // GET de novo e tenta persistir outra vez (a persistencia e idempotente por venda/tipo).
            e.Resultado.FalhasDePersistencia++;
            Log.Error(ex,
                "Recuperacao fiscal: autorizada na Focus, mas a persistencia local FALHOU na pendencia {PendenciaId} " +
                "(ref {Referencia}). A pendencia continua na fila.", e.Pendencia.Id, e.Pendencia.Referencia);

            await RegistrarFalhaLocalDeAutorizacaoAsync(e, ex);
            return;
        }

        await RemoverPendenciaAsync(e);
        e.Resultado.Autorizadas++;
    }

    private async Task RegistrarFalhaLocalDeAutorizacaoAsync(Execucao e, Exception ex)
    {
        try
        {
            var detalhe = $"Autorizada na Focus, mas a persistencia local falhou ({ex.GetType().Name}): {Resumo(ex)}";
            var veredito = new FocusVerdict(FocusVerdictKind.Transitorio, Detalhe: detalhe);

            var decisao = RecoveryPolicy.Decidir(veredito, FocusOperation.Get, e.Situacao, _relogio(), _opcoes);
            decisao = decisao with { Motivo = decisao.Motivo + " | " + detalhe };

            await GravarDecisaoAsync(e, ComNota(e, decisao));
        }
        catch (FalhaDePersistenciaException fx)
        {
            Log.Error(fx.InnerException ?? fx,
                "Recuperacao fiscal: nao foi possivel registrar o reagendamento da pendencia {PendenciaId}.", e.Pendencia.Id);
        }
        catch (Exception falha)
        {
            Log.Error(falha,
                "Recuperacao fiscal: nao foi possivel decidir o reagendamento da pendencia {PendenciaId}.", e.Pendencia.Id);
        }
    }

    // ── Rejeicao definitiva ──────────────────────────────────────────────

    private async Task ConcluirRejeicaoAsync(Execucao e, FocusVerdict veredito, FocusResponse? resposta)
    {
        var detalhe = !string.IsNullOrWhiteSpace(resposta?.MensagemSefaz)
            ? resposta!.MensagemSefaz!
            : (!string.IsNullOrWhiteSpace(veredito.Detalhe) ? veredito.Detalhe! : "rejeicao fiscal sem detalhe");

        try
        {
            // Mesmos argumentos que o worker atual usa na rejeicao.
            await _vendas.AtualizarDadosNfceAsync(
                e.Pendencia.VendaId, string.Empty, "Rejeitada: " + detalhe,
                e.Contexto.AmbienteNome, e.Pendencia.Referencia);
        }
        catch (Exception ex)
        {
            throw new FalhaDePersistenciaException("MarcarVendaRejeitada", ex);
        }

        // So remove depois de a venda estar marcada. (O worker antigo marcava e deixava a pendencia
        // na fila, re-marcando a venda a cada 2 minutos.)
        await RemoverPendenciaAsync(e);
        e.Resultado.Rejeitadas++;
    }

    // ── Regeneracao da DataEmissao e reenvio ─────────────────────────────

    private async Task RegenerarEReenviarAsync(Execucao e, RecoveryDecision decisaoGet)
    {
        ExigirConsultaConcluida(e);   // antes de QUALQUER gravacao

        if (e.JaPostou)
            throw new InvalidOperationException("Um segundo POST na mesma rodada foi bloqueado.");

        var payloadAntigo = e.Pendencia.PayloadJson;
        var dataOriginal = PayloadDataEmissao.LerTexto(payloadAntigo)
            ?? throw new InvalidOperationException("O payload nao tem uma DataEmissao textual de primeiro nivel.");
        var dataNova = NovaDataEmissao();

        // Tudo que pode falhar por causa do PAYLOAD acontece antes de gravar qualquer coisa.
        var payloadNovo = PayloadDataEmissao.Substituir(payloadAntigo, dataNova);
        var corpo = NfceCorpoDeEnvio.Montar(payloadNovo);

        e.NotaRegeneracao = $"DataEmissao original={dataOriginal}; nova={dataNova}";
        Log.Information(
            "Recuperacao fiscal: regenerando DataEmissao da pendencia {PendenciaId} (ref {Referencia}): original {DataOriginal}, nova {DataNova}.",
            e.Pendencia.Id, e.Pendencia.Referencia, dataOriginal, dataNova);

        // (1) Registra a data original e a nova em UltimaDecisao ANTES de mexer no payload.
        var intencao = decisaoGet with { Motivo = $"Regenerando {e.NotaRegeneracao} | {decisaoGet.Motivo}" };
        if (!await AplicarAsync(e, intencao))
        {
            e.Resultado.Ignoradas++;
            return;
        }

        // (2) O payload novo precisa estar no banco ANTES de qualquer POST.
        bool regravou;
        try
        {
            regravou = await _store.RegravarPayloadAsync(e.Pendencia.Id, payloadNovo);
        }
        catch (Exception ex)
        {
            throw new FalhaDePersistenciaException("RegravarPayload", ex);
        }

        if (!regravou)
        {
            Log.Warning("Recuperacao fiscal: a pendencia {PendenciaId} nao aceita mais gravacao (terminal ou removida); nenhum POST foi feito.", e.Pendencia.Id);
            e.Resultado.Ignoradas++;
            return;
        }

        // (3) POST, na MESMA ref.
        var respostaPost = await EnviarAsync(e, corpo);
        var vereditoPost = FocusResponseClassifier.Classify(respostaPost, FocusOperation.Post);

        // (4) A decisao do POST parte dos contadores que a gravacao (1) deixou.
        e.Situacao = e.Situacao with
        {
            FalhasTransitoriasSeguidas = decisaoGet.FalhasTransitoriasSeguidas,
            FalhasDesconhecidasSeguidas = decisaoGet.FalhasDesconhecidasSeguidas
        };

        await DecidirEExecutarAsync(e, vereditoPost, FocusOperation.Post, respostaPost);
    }

    /// <summary>
    /// A Focus disse (POST) que a ref ja foi processada: um GET no mesmo ciclo. Se ele contradisser
    /// (nao encontrada, ou rejeicao), NAO se reenvia: vai para IntervencaoManual.
    /// </summary>
    private async Task ReconciliarAsync(Execucao e)
    {
        if (e.JaReconciliou)
            throw new InvalidOperationException("Uma segunda reconciliacao na mesma rodada foi bloqueada.");

        e.JaReconciliou = true;

        var resposta = await ConsultarAsync(e);
        var veredito = FocusResponseClassifier.Classify(resposta, FocusOperation.Get);

        if (veredito.Kind is FocusVerdictKind.NaoEncontrado
                          or FocusVerdictKind.RejeicaoFiscal
                          or FocusVerdictKind.RejeicaoDefinitiva)
        {
            Log.Warning(
                "Recuperacao fiscal: contradicao da Focus na pendencia {PendenciaId} (ref {Referencia}): o POST indicou ref ja processada, " +
                "mas a consulta devolveu {Veredito}. Sem novo POST.", e.Pendencia.Id, e.Pendencia.Referencia, veredito.Kind);

            var contradicao = new RecoveryDecision(
                RecoveryAction.IntervencaoManual, NfePendenteEstados.IntervencaoManual, null, 0, 0,
                $"Contradicao da Focus: o POST disse que a ref ja foi processada, mas a consulta seguinte devolveu " +
                $"{veredito.Kind}. Sem novo POST; revisar manualmente.");

            await GravarDecisaoAsync(e, ComNota(e, contradicao));
            return;
        }

        await DecidirEExecutarAsync(e, veredito, FocusOperation.Get, resposta);
    }

    // ── Chamadas a Focus ─────────────────────────────────────────────────

    private async Task<FocusResponse> ConsultarAsync(Execucao e)
    {
        e.ConsultaPendente = true;   // a tentativa conta, tenha dado certo ou nao

        var resposta = await _focus.ConsultarAsync(
            FocusDocumentType.Nfce, e.Pendencia.Referencia, e.Contexto.Token, e.Contexto.IsProducao, e.Ct);

        // "GET concluido" = a Focus respondeu por HTTP, com QUALQUER status (inclusive o 404 que motiva a
        // regeneracao). Falha de transporte / timeout nao conta.
        if (resposta.HouveRespostaHttp)
            e.ConsultaConcluidaNoCiclo = true;

        return resposta;
    }

    private async Task<FocusResponse> EnviarAsync(Execucao e, string corpo)
    {
        ExigirConsultaConcluida(e);   // ultima linha de defesa: este e o unico ponto de POST

        e.PostPendente = true;
        e.JaPostou = true;

        return await _focus.EnviarNfceAsync(
            e.Pendencia.Referencia, corpo, e.Contexto.Token, e.Contexto.IsProducao, e.Ct);
    }

    // ── Gravacoes (toda falha vira FalhaDePersistenciaException) ─────────

    /// <summary>Grava a decisao e conta o resultado do ciclo (a store pode recusar: conta como Ignorada).</summary>
    private async Task GravarDecisaoAsync(Execucao e, RecoveryDecision decisao)
    {
        if (!await AplicarAsync(e, decisao))
        {
            e.Resultado.Ignoradas++;
            return;
        }

        switch (decisao.Acao)
        {
            case RecoveryAction.Aguardar:
            case RecoveryAction.ManterComBackoff:
                e.Resultado.Agendadas++;
                break;
            case RecoveryAction.AguardarCorrecao:
                e.Resultado.AguardandoCorrecao++;
                break;
            case RecoveryAction.IntervencaoManual:
                e.Resultado.IntervencaoManual++;
                break;
        }
    }

    private async Task<bool> AplicarAsync(Execucao e, RecoveryDecision decisao)
    {
        bool gravou;
        try
        {
            gravou = await _store.AplicarDecisaoAsync(
                e.Pendencia.Id, decisao, _relogio().UtcDateTime, e.ConsultaPendente, e.PostPendente);
        }
        catch (Exception ex)
        {
            throw new FalhaDePersistenciaException("AplicarDecisao", ex);
        }

        if (gravou)
        {
            // Consulta e POST ja foram contados nesta gravacao.
            e.ConsultaPendente = false;
            e.PostPendente = false;
        }

        return gravou;
    }

    private async Task RemoverPendenciaAsync(Execucao e)
    {
        bool removeu;
        try
        {
            removeu = await _store.RemoverAsync(e.Pendencia.Id);
        }
        catch (Exception ex)
        {
            throw new FalhaDePersistenciaException("Remover", ex);
        }

        if (!removeu)
        {
            Log.Warning("Recuperacao fiscal: a pendencia {PendenciaId} nao foi removida (nao processavel).", e.Pendencia.Id);
        }
    }

    // ── Apoio ────────────────────────────────────────────────────────────

    // A nota vai no INICIO: a store corta UltimaDecisao em 500 caracteres pelo FIM, e o texto da politica
    // pode crescer (traz detalhes vindos da Focus). Assim o rastro das datas e o que nunca se perde.
    private static RecoveryDecision ComNota(Execucao e, RecoveryDecision decisao) =>
        e.NotaRegeneracao is null || decisao.Motivo.Contains(e.NotaRegeneracao, StringComparison.Ordinal)
            ? decisao
            : decisao with { Motivo = $"{e.NotaRegeneracao} | {decisao.Motivo}" };

    private string NovaDataEmissao() =>
        TimeZoneInfo.ConvertTime(_relogio(), FusoBrasilHelper.FusoBrasil)
            .ToString(FormatoDataEmissao, CultureInfo.InvariantCulture);

    private static PendenciaSituacao MontarSituacao(NfePendente pendencia)
    {
        // DataFalha e horario de Brasilia (convencao historica da coluna).
        var dataFalha = DateTime.SpecifyKind(pendencia.DataFalha, DateTimeKind.Unspecified);
        var entrouNaFila = new DateTimeOffset(dataFalha, FusoBrasilHelper.FusoBrasil.GetUtcOffset(dataFalha));

        return new PendenciaSituacao(
            FocusDocumentType.Nfce,
            pendencia.Estado,
            entrouNaFila,
            pendencia.FalhasTransitoriasSeguidas,
            pendencia.FalhasDesconhecidasSeguidas,
            PayloadDataEmissao.Ler(pendencia.PayloadJson));
    }

    /// <summary>
    /// Garantia ESTRUTURAL de que nenhum POST sai sem uma consulta (GET) concluida, neste ciclo e para esta
    /// pendencia, em vez de depender so de a politica nunca pedir a regeneracao sem um veredito de GET.
    /// Uma violacao e um defeito de programacao: vira excecao de processamento (conta para o limite K).
    /// </summary>
    private static void ExigirConsultaConcluida(Execucao e)
    {
        if (!e.ConsultaConcluidaNoCiclo)
            throw new InvalidOperationException(
                "POST bloqueado: nao houve consulta (GET) concluida neste ciclo para a pendencia.");
    }

    private static string Resumo(Exception ex)
    {
        var texto = (ex.Message ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return texto.Length <= TamanhoMaximoResumoExcecao ? texto : texto.Substring(0, TamanhoMaximoResumoExcecao);
    }

    // ── Estado de uma execucao ───────────────────────────────────────────

    private sealed class Execucao
    {
        public Execucao(NfePendente pendencia, RecoveryTenantContext contexto, RecoveryCycleResult resultado, CancellationToken ct)
        {
            Pendencia = pendencia;
            Contexto = contexto;
            Resultado = resultado;
            Ct = ct;
            Situacao = MontarSituacao(pendencia);
        }

        public NfePendente Pendencia { get; }
        public RecoveryTenantContext Contexto { get; }
        public RecoveryCycleResult Resultado { get; }
        public CancellationToken Ct { get; }

        /// <summary>Situacao corrente que a politica enxerga (contadores mudam durante a rodada).</summary>
        public PendenciaSituacao Situacao { get; set; }

        /// <summary>GET feito e ainda nao contabilizado por uma gravacao.</summary>
        public bool ConsultaPendente { get; set; }

        /// <summary>POST tentado e ainda nao contabilizado por uma gravacao.</summary>
        public bool PostPendente { get; set; }

        /// <summary>A Focus RESPONDEU (HTTP, qualquer status) a um GET desta pendencia neste ciclo.</summary>
        public bool ConsultaConcluidaNoCiclo { get; set; }

        /// <summary>No maximo UM POST por pendencia por ciclo.</summary>
        public bool JaPostou { get; set; }

        /// <summary>No maximo UMA reconciliacao (GET extra) depois de JaProcessado.</summary>
        public bool JaReconciliou { get; set; }

        /// <summary>"DataEmissao original=...; nova=..." depois de uma regeneracao; acompanha as decisoes seguintes.</summary>
        public string? NotaRegeneracao { get; set; }
    }

    private sealed class FalhaDePersistenciaException : Exception
    {
        public FalhaDePersistenciaException(string etapa, Exception inner)
            : base($"Falha de persistencia em {etapa}: {inner.Message}", inner)
        {
            Etapa = etapa;
        }

        public string Etapa { get; }
    }
}
