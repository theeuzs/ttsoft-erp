using ERP.Application.DTOs;
using ERP.Domain.Entities;

namespace ERP.Application.Interfaces;

public interface IOrcamentoService
{
    Task<Orcamento> SalvarOrcamentoAsync(CreateOrcamentoDto dto);
    Task<IEnumerable<Orcamento>> ObterTodosAsync();
    Task<IEnumerable<Orcamento>> GetByClienteAsync(Guid clienteId);
    Task MarcarComoVendidoAsync(Guid id);

    // ── CRM / Follow-Up ──────────────────────────────────────────────────────

    /// <summary>
    /// Lista orçamentos com follow-up agendado para hoje (ou vencidos e pendentes).
    /// Usada na agenda diária do vendedor.
    /// </summary>
    Task<IEnumerable<OrcamentoFollowUpDto>> GetAgendaHojeAsync();

    /// <summary>
    /// Todos os orçamentos com seus campos de follow-up.
    /// Usada pela listagem completa do Portal/WPF com filtros de CRM.
    /// </summary>
    Task<IEnumerable<OrcamentoFollowUpDto>> GetTodosComFollowUpAsync();

    /// <summary>Agenda (ou reagenda) um follow-up para um orçamento.</summary>
    Task AgendarFollowUpAsync(Guid id, AgendarFollowUpDto dto);

    /// <summary>
    /// Registra o resultado de um contato com o cliente:
    /// Contatado / Convertido / Perdido + observação + motivo da perda.
    /// Se ProximoFollowUpEmDias > 0, reagenda automaticamente.
    /// </summary>
    Task RegistrarContatoAsync(Guid id, RegistrarContatoDto dto);

    /// <summary>
    /// Taxa de conversão de orçamentos no período informado.
    /// Inclui top motivos de perda.
    /// </summary>
    Task<TaxaConversaoDto> GetTaxaConversaoAsync(DateTime inicio, DateTime fim);
}
