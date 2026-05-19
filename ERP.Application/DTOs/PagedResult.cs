// ── ERP.Application/DTOs/PagedResult.cs ──────────────────────────────────────
namespace ERP.Application.DTOs;

/// <summary>
/// Resultado paginado genérico. Usado por qualquer listagem do sistema.
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items       { get; init; } = Enumerable.Empty<T>();
    public int            TotalItems  { get; init; }
    public int            Page        { get; init; }
    public int            PageSize    { get; init; }
    public int            TotalPages  => (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool           HasPrevious => Page > 1;
    public bool           HasNext     => Page < TotalPages;
}
