// ── ERP.Application/DTOs/CatalogoPublicoDtos.cs ──────────────────────────────
namespace ERP.Application.DTOs;

public record CatalogoItemDto(
    Guid     Id,
    string   Name,
    string   CategoryName,
    string   Barcode,
    string   Unit,
    decimal? SalePrice,
    decimal? Stock,
    string?  ImageUrl);

public record CatalogoResultadoDto(
    IReadOnlyList<CatalogoItemDto> Items,
    int TotalItems,
    int Page,
    int PageSize);
