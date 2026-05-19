// ── ERP.Application/Interfaces/IContaPagarService.cs ─────────────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Serviço completo de contas a pagar.
/// Sprint 2A: expandido com todas as operações que estavam no controller.
/// Os dois métodos originais (CountVencendoHojeAsync / GetVencendoHojeAsync)
/// foram mantidos para não quebrar DashboardService e outros consumers.
/// </summary>
public interface IContaPagarService
{
    // ── Métodos originais (não remover — usados pelo DashboardService) ─────────
    Task<int>                                          CountVencendoHojeAsync();
    Task<IEnumerable<(string Descricao, decimal Valor)>> GetVencendoHojeAsync();

    // ── Novos métodos (Sprint 2A) ─────────────────────────────────────────────

    /// <summary>Lista contas pendentes (não Pago e não Cancelado), ordenadas por vencimento.</summary>
    Task<IReadOnlyList<ContaPagarDto>> GetPendentesAsync(CancellationToken ct = default);

    /// <summary>Lista contas vencidas (Pendente + DataVencimento anterior a hoje).</summary>
    Task<IReadOnlyList<ContaPagarDto>> GetVencidasAsync(CancellationToken ct = default);

    /// <summary>Resumo financeiro agregado — calculado inteiramente no banco.</summary>
    Task<ContaPagarResumoDto> GetResumoAsync(CancellationToken ct = default);

    /// <summary>Cria nova conta a pagar e retorna a entidade persistida.</summary>
    Task<ContaPagarDto> CreateAsync(CreateContaPagarDto dto, CancellationToken ct = default);

    /// <summary>Registra pagamento: Status → "Pago" + DataPagamento.</summary>
    Task PagarAsync(Guid id, CancellationToken ct = default);

    /// <summary>Cancela uma conta: Status → "Cancelado".</summary>
    Task CancelarAsync(Guid id, CancellationToken ct = default);
}