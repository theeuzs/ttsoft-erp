namespace ERP.Application.Fiscal.Recovery;

/// <summary>O que o orquestrador deve fazer com a pendencia (4A-5 executa; a politica so decide).</summary>
public enum RecoveryAction
{
    /// <summary>Persistir a autorizacao (PersistirEmissaoAutorizadaAsync) e remover a pendencia.</summary>
    PersistirAutorizadaERemover = 1,
    /// <summary>A Focus ja processou (already_processed): fazer GET e reconciliar. Nao POSTar.</summary>
    ConsultarEReconciliar,
    /// <summary>Manter a pendencia e consultar de novo em ProximaTentativaEm. Nao POSTar.</summary>
    Aguardar,
    /// <summary>Alterar SOMENTE DataEmissao no payload e reenviar na MESMA ref.</summary>
    ReenviarComDataRegenerada,
    /// <summary>Reenviar o payload original, sem alterar nada (NF-e: a regra de data e so NFC-e).</summary>
    ReenviarPayloadOriginal,
    /// <summary>Venda Rejeitada (como hoje) e remover a pendencia.</summary>
    MarcarVendaRejeitadaERemover,
    /// <summary>Falha transitoria: manter na fila com backoff.</summary>
    ManterComBackoff,
    /// <summary>401/403: estado AguardandoCorrecao, consulta espacada.</summary>
    AguardarCorrecao,
    /// <summary>Estado terminal IntervencaoManual.</summary>
    IntervencaoManual
}

/// <summary>
/// Resultado da politica. Datas em DateTimeOffset (UTC) por decisao: a fila
/// tecnica nao usa horario local (pode haver mais de uma instancia da API).
/// </summary>
/// <param name="Acao">O que fazer.</param>
/// <param name="NovoEstado">Estado persistido depois da acao; nulo quando a pendencia e removida.</param>
/// <param name="ProximaTentativaEm">Quando a pendencia volta a ser elegivel; nulo = sem agendamento (removida ou terminal).</param>
/// <param name="FalhasTransitoriasSeguidas">Incrementa SO em Transitorio; zera em qualquer resultado nao transitorio.</param>
/// <param name="FalhasDesconhecidasSeguidas">Incrementa SO em Desconhecido; zera em qualquer resultado conhecido.</param>
/// <param name="Motivo">Texto curto (veredito + razao) para gravar em UltimaDecisao. Sem corpo bruto.</param>
/// <param name="Regeneracao">Resultado da D8, quando ela foi avaliada.</param>
public sealed record RecoveryDecision(
    RecoveryAction Acao,
    string? NovoEstado,
    DateTimeOffset? ProximaTentativaEm,
    int FalhasTransitoriasSeguidas,
    int FalhasDesconhecidasSeguidas,
    string Motivo,
    MotivoRegeneracao? Regeneracao = null);
