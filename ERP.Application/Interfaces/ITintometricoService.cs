// ── ERP.Application/Interfaces/ITintometricoService.cs ───────────────────────
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

/// <summary>
/// Serviço de fórmulas tintométricas (CRUD + cálculo de quantidade de tinta).
/// Encapsula toda a lógica de domínio e acesso a dados — controllers não
/// tocam em AppDbContext diretamente.
/// </summary>
public interface ITintometricoService
{
    Task<IReadOnlyList<FormulaDto>> GetAllAsync(string? busca, CancellationToken ct = default);
    Task<FormulaDto?> GetByProductAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Cria ou atualiza fórmula de um produto (upsert).
    /// Lança KeyNotFoundException se o ProductId não existir no tenant atual
    /// (controller mapeia para 404) — S16 FIX: sem essa checagem, um ProductId
    /// de outro tenant gerava fórmula órfã que quebrava consultas futuras.
    /// </summary>
    Task UpsertAsync(SalvarFormulaDto dto, CancellationToken ct = default);

    /// <summary>Retorna false se não existir fórmula para o produto (controller mapeia para 404).</summary>
    Task<bool> DeleteAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Calcula quantidade de tinta para uma área, baseado na fórmula cadastrada.
    /// Retorna null se o produto não tem fórmula (controller mapeia para 404).
    /// Lança InvalidOperationException se areaM2 &lt;= 0 (controller mapeia para 400).
    /// </summary>
    Task<CalculoTintaResultadoDto?> CalcularAsync(
        Guid productId, decimal areaM2, int demaos, CancellationToken ct = default);
}