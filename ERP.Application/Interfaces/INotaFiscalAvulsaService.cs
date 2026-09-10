using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Item 9 do roadmap fiscal — nota avulsa: NF-e desacoplada de uma Venda,
/// com estado de rascunho ("salvar sem emitir") e conferência de impostos
/// antes de transmitir. Serviço separado do IFiscalService (que é
/// especificamente pra nota de venda/marketplace) — a fonte dos dados é
/// bem diferente (NotaFiscalItem em vez de SaleItem, sem Payments).
/// </summary>
public interface INotaFiscalAvulsaService
{
    /// <summary>Cria (Id nulo) ou atualiza (Id preenchido) um rascunho.
    /// Só funciona em notas ainda em Rascunho — depois de emitida, não editável.</summary>
    Task<Guid> SalvarRascunhoAsync(SalvarNotaFiscalAvulsaDto dto);

    /// <summary>Backlog premium — "copiar nota": cria um rascunho novo a
    /// partir de qualquer nota existente (rascunho ou já emitida), com
    /// destinatário e itens copiados. Útil pra repetir uma operação
    /// recorrente sem redigitar tudo.</summary>
    Task<Guid> CopiarComoRascunhoAsync(Guid idOrigem);

    Task<NotaFiscalAvulsaDto?> ObterAsync(Guid id);

    Task<IReadOnlyList<NotaFiscalAvulsaResumoDto>> ListarAsync();

    /// <summary>Só remove rascunhos — nota já emitida se cancela, não se exclui.</summary>
    Task ExcluirRascunhoAsync(Guid id);

    /// <summary>"Pré-visualização" honesta — não existe DANFE antes de
    /// autorizar, mas mostra os impostos calculados pelo MotorFiscal item a
    /// item, pra conferir antes de transmitir.</summary>
    Task<ConferenciaFiscalDto> ConferirAsync(Guid id);

    Task<FiscalEmissionResult> EmitirAsync(Guid id);

    /// <summary>Achado da revisão de arquitetura (18/08) — nota avulsa
    /// autorizada não tinha NENHUM jeito de cancelar em lugar nenhum do
    /// sistema (nem essa tela, nem a tela geral de Notas Fiscais, que só
    /// enxerga notas ligadas a uma Sale). Usa o mesmo endpoint de
    /// cancelamento já existente, só que com a referência "avulsa-{id}".</summary>
    Task<FiscalEmissionResult> CancelarAsync(Guid id, string justificativa);

    /// <summary>Consulta o status real de uma nota "Processando" (Focus
    /// respondeu sucesso mas a SEFAZ ainda não tinha confirmado autorização
    /// na hora da emissão).</summary>
    Task<FiscalEmissionResult> ConsultarStatusAsync(Guid id);

    /// <summary>Histórico amplo pra tela "NF-e (Emissão)" — qualquer NF-e
    /// A4, avulsa ou ligada a venda, com filtro opcional por período/status.</summary>
    Task<IReadOnlyList<NfeA4HistoricoDto>> ListarHistoricoAsync(
        DateTime? dataInicio = null, DateTime? dataFim = null, string? status = null);
}