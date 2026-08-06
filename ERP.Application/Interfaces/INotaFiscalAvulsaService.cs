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
}