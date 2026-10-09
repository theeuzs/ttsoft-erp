using ERP.Domain.Entities;

namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Acesso da recuperacao fiscal (Etapa 4A-5c) a fila NfePendente, por estado e agenda.
/// Aditiva: o INfeContingencyService (que o WPF tambem injeta) nao muda.
///
/// Regras que valem para TODOS os metodos:
///  - Isolamento por tenant: so enxerga e altera pendencias do tenant do contexto
///    (filtro global do AppDbContext). Pendencia de outro tenant se comporta como inexistente.
///  - IntervencaoManual e TERMINAL, verificado a partir do que esta GRAVADO no banco (nao do
///    que o chamador acha): uma pendencia nesse estado nunca e selecionada, alterada nem
///    removida por esta store. So uma acao humana, fora daqui, a tira de la.
///  - Horarios sao UTC. Quem chama passa "agora" com Kind=Utc; datas de agenda gravadas sao UTC.
///  - As colunas legadas (Tentativas, UltimaMensagemErro) nunca sao alteradas por esta store.
///  - Sem exclusao mutua entre instancias: quem leu e gravou depois pode sobrescrever uma
///    gravacao concorrente. Isso e da Etapa 5 (hoje ha uma unica instancia).
/// </summary>
public interface IFiscalRecoveryStore
{
    /// <summary>
    /// Pendencias do tipo informado, em Ativa ou AguardandoCorrecao, com ProximaTentativaEm nula
    /// ou ja vencida (&lt;= agoraUtc), da mais antiga (DataFalha) para a mais nova. Nunca devolve
    /// IntervencaoManual, estado desconhecido, outro tipo de nota nem outro tenant.
    /// </summary>
    Task<IReadOnlyList<NfePendente>> ObterElegiveisAsync(string tipoNota, DateTime agoraUtc);

    /// <summary>
    /// Troca SOMENTE o PayloadJson e grava na hora (commit antes de devolver), para que o payload
    /// novo esteja no banco antes de qualquer POST. Devolve false, sem alterar nada, se a pendencia
    /// nao existe (para este tenant) ou se o estado gravado nao e Ativa/AguardandoCorrecao.
    /// </summary>
    Task<bool> RegravarPayloadAsync(Guid id, string payloadJson);

    /// <summary>
    /// Grava o resultado de uma decisao da politica: Estado, ProximaTentativaEm (em UTC), os dois
    /// contadores de falhas seguidas e UltimaDecisao (truncada em 500 caracteres). Se
    /// <paramref name="consultou"/>, grava UltimaConsultaEm e soma 1 em TentativasConsulta; se
    /// <paramref name="postou"/>, grava UltimoPostEm e soma 1 em TentativasPost.
    /// Decisao que remove a pendencia (NovoEstado nulo) NAO passa por aqui: use RemoverAsync.
    /// Devolve false, sem alterar nada, nas mesmas condicoes de RegravarPayloadAsync.
    /// </summary>
    Task<bool> AplicarDecisaoAsync(Guid id, RecoveryDecision decisao, DateTime agoraUtc, bool consultou, bool postou);

    /// <summary>
    /// Apaga a pendencia. Devolve false, sem alterar nada, nas mesmas condicoes de
    /// RegravarPayloadAsync (inclusive: nunca remove uma pendencia em IntervencaoManual).
    /// </summary>
    Task<bool> RemoverAsync(Guid id);
}
