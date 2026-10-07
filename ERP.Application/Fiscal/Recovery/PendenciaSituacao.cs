using ERP.Application.Fiscal.Focus;

namespace ERP.Application.Fiscal.Recovery;

/// <summary>
/// Foto da pendencia que a politica precisa. Valor puro, desacoplado da
/// entidade NfePendente (que ainda nao tem as colunas novas). Quem monta
/// isto (o orquestrador, 4A-5) converte DataFalha (horario de Brasilia,
/// convencao historica) para DateTimeOffset.
/// </summary>
/// <param name="Tipo">Tipo do documento (vem de NfePendente.TipoNota).</param>
/// <param name="Estado">Um de NfePendenteEstados.</param>
/// <param name="EntrouNaFilaEm">Quando a pendencia entrou na fila (base da espera maxima em Processando).</param>
/// <param name="FalhasTransitoriasSeguidas">Contador atual.</param>
/// <param name="FalhasDesconhecidasSeguidas">Contador atual.</param>
/// <param name="DataEmissaoPayload">DataEmissao lido do PayloadJson, ou nulo se invalido (ver DataEmissaoRegeneracao.ParseDataEmissao).</param>
public sealed record PendenciaSituacao(
    FocusDocumentType Tipo,
    string Estado,
    DateTimeOffset EntrouNaFilaEm,
    int FalhasTransitoriasSeguidas,
    int FalhasDesconhecidasSeguidas,
    DateTimeOffset? DataEmissaoPayload);
