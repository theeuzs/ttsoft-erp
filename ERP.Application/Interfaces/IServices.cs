// ── ERP.Application/Interfaces/IServices.cs ──────────────────────────────────
// Sprint 2A: IAuditLogService expandido com GetPagedAsync.
// Todos os outros contratos mantidos intactos.
using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IProductService
{
    Task<IEnumerable<ProductDto>> GetAllAsync();
    Task<ProductDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<ProductDto>> SearchAsync(string term);
    Task<ProductDto?> GetByBarcodeAsync(string barcode);
    Task<ProductDto?> GetBySkuAsync(string sku);
    Task<ProductDto> CreateAsync(CreateProductDto dto);
    Task<ProductDto> UpdateAsync(UpdateProductDto dto);
    Task DeleteAsync(Guid id);
    Task<PagedResult<ProductDto>> GetPagedAsync(int page = 1, int pageSize = 50, string? search = null);
    Task<int> GetLowStockCountAsync();
    Task<IEnumerable<ProductDto>> GetLowStockListAsync();
}

public interface ICustomerService
{
    Task<IEnumerable<CustomerDto>> GetAllAsync();
    Task<CustomerDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<CustomerDto>> SearchAsync(string term);
    Task<CustomerDto> CreateAsync(CreateCustomerDto dto);
    Task<CustomerDto> UpdateAsync(Guid id, CreateCustomerDto dto);
    Task DeleteAsync(Guid id);
    Task<PagedResult<CustomerDto>> GetPagedAsync(int page = 1, int pageSize = 50, string? search = null);
}

public interface ISaleService
{
    Task<IEnumerable<SaleDto>> GetAllAsync(DateTime? from = null, DateTime? to = null, string? sellerId = null);
    Task<SaleDetailDto?> GetDetailAsync(Guid id);
    Task<SaleDto> CreateAsync(CreateSaleDto dto);
    Task CancelAsync(Guid id, string reason);
    Task AtualizarDadosNfceAsync(Guid vendaId, string urlDanfe, string status, string ambiente, string referencia);
    Task<IEnumerable<SalesReportItemDto>> GetSalesReportAsync(DateTime startDate, DateTime endDate, string? sellerName = null);
}

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync();
    Task<DreDto> GetDreSimplificadoAsync(DateTime inicio, DateTime fim);
}

public interface ISupplierService
{
    Task<IEnumerable<SupplierDto>> GetAllAsync();
    Task<SupplierDto> CreateAsync(string name);
}

public interface ICategoryService
{
    Task<IEnumerable<CategoryDto>> GetAllAsync();
    Task<CategoryDto> CreateAsync(string name);
}

public interface IBrandService
{
    Task<IEnumerable<BrandDto>> GetAllAsync();
    Task<BrandDto> CreateAsync(string name);
}

public interface IUserQueryService
{
    Task<IEnumerable<string>> GetAllNamesAsync();
}

public interface IAuditLogService
{
    /// <summary>
    /// Busca por período + texto livre. Mantido para compatibilidade com WPF / AuditLogViewModel.
    /// </summary>
    Task<IEnumerable<AuditLogDto>> SearchAsync(
        DateTime from, DateTime to,
        string? busca = null,
        int     take  = 100);

    /// <summary>
    /// Versão paginada usada pelo AuditoriaController (Sprint 2A).
    /// Todos os filtros e a ordenação são resolvidos no banco — sem ToList() antecipado.
    /// </summary>
    Task<PagedResult<AuditLogDto>> GetPagedAsync(
        string?   usuario  = null,
        string?   acao     = null,
        DateTime? de       = null,
        DateTime? ate      = null,
        int       pagina   = 1,
        int       tam      = 50,
        CancellationToken ct = default);
}