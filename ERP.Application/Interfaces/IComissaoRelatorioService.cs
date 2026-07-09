// ── ERP.Application/Interfaces/IComissaoRelatorioService.cs ──────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Serviço de relatório de comissões por vendedor, usando o percentual
/// registrado no cargo (Role) de cada um — usado pelo endpoint GET /api/comissao.
/// Não confundir com IComissaoService (IRelatorioServices.cs), que calcula
/// comissão com um percentual único informado manualmente (usado pelo WPF
/// para simulação/relatório em PDF) — são dois cálculos diferentes que só
/// coincidem no nome genérico "comissão".
/// Encapsula toda a lógica de domínio e acesso a dados — controllers não
/// tocam em AppDbContext diretamente.
/// </summary>
public interface IComissaoRelatorioService
{
    /// <summary>
    /// Calcula comissões por vendedor no período informado.
    /// A taxa de comissão vem do campo PercentualComissao do cargo (Role) do usuário.
    /// </summary>
    Task<ComissaoRelatorioDto> CalcularComissoesAsync(
        DateTime? inicio, DateTime? fim, CancellationToken ct = default);
}
