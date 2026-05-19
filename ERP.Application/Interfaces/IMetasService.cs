// ── ERP.Application/Interfaces/IMetasService.cs ──────────────────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Serviço de metas de vendas.
/// Encapsula toda a lógica de domínio e acesso a dados — controllers não
/// tocam em AppDbContext diretamente.
/// </summary>
public interface IMetasService
{
    /// <summary>
    /// Lista as metas do mês/ano informados com progresso real de vendas calculado no banco.
    /// </summary>
    Task<IReadOnlyList<MetaProgressoDto>> GetAllAsync(int mes, int ano, CancellationToken ct = default);

    /// <summary>Cria uma nova meta ou atualiza a existente (upsert por vendedor/mês/ano).</summary>
    /// <returns>(Id, atualizado: true se era update, false se era insert)</returns>
    Task<(Guid Id, bool Atualizado)> UpsertAsync(MetaVendasDto dto, CancellationToken ct = default);

    /// <summary>Remove uma meta pelo Id.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
