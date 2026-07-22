// ── ERP.Domain/Interfaces/IRecebivelOperadoraRepository.cs ───────────────────
using ERP.Domain.Entities;

namespace ERP.Domain.Interfaces;

public interface IRecebivelOperadoraRepository
{
    Task AddAsync(RecebivelOperadora recebivel);

    /// <summary>Todos os recebíveis ainda Pendentes, mais recentes primeiro.</summary>
    Task<IEnumerable<RecebivelOperadora>> GetPendentesAsync();

    Task<IEnumerable<RecebivelOperadora>> GetByIdsAsync(IEnumerable<Guid> ids);

    /// <summary>Marca um conjunto de recebíveis como Liquidado, vinculando ao movimento bancário criado.</summary>
    Task MarcarLiquidadosAsync(IEnumerable<Guid> ids, Guid movimentoContaBancariaId, DateTime dataLiquidacao);

    /// <summary>Recebíveis CRIADOS num período (qualquer status) — pro Extrato Financeiro.</summary>
    Task<IEnumerable<RecebivelOperadora>> GetPorPeriodoAsync(DateTime inicio, DateTime fim);

    /// <summary>Todos os recebíveis de uma venda específica, qualquer status — pro cancelamento.</summary>
    Task<IEnumerable<RecebivelOperadora>> GetByVendaIdAsync(Guid vendaId);

    /// <summary>Cancela os recebíveis AINDA PENDENTES de uma venda (chamado no cancelamento da venda).</summary>
    Task CancelarPendentesPorVendaAsync(Guid vendaId);
}