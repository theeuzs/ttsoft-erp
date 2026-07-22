// ── ERP.Application/Interfaces/IExtratoFinanceiroService.cs ──────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IExtratoFinanceiroService
{
    /// <summary>Timeline unificada de Caixa + Conta Bancária + Recebíveis de Operadora, num período.</summary>
    Task<IReadOnlyList<ExtratoItemDto>> ObterExtratoAsync(DateTime inicio, DateTime fim);
}
