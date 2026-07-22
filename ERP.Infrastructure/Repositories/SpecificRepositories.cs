// S3.7: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync nos métodos atômicos.
// EXCEÇÃO DOCUMENTADA: DevolucaoRepository.AddAsync mantém ExecuteSqlRawAsync
// por ter 11 parâmetros — risco de reordenamento supera risco de injection
// (todos os valores são Guid, decimal, string — nunca input direto do usuário).
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ERP.Infrastructure.Repositories;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext ctx) : base(ctx) { }

    // TENANT FIX: AND TenantId= adicionado nas duas queries, fechando a única
    // exceção no projeto ao padrão de defesa em profundidade em SQL raw (todo o
    // resto do projeto já inclui TenantId no WHERE — ver ContaPagarService,
    // ContaReceberService, FidelidadeService, MarketplaceService, RoleRepository).
    // Mitigado na prática mesmo sem isso, porque o único chamador (SaleService,
    // DevolucaoService) sempre resolve o productId via GetByIdAsync (que já
    // filtra por tenant) antes de chegar aqui — mas depender só disso é frágil:
    // um chamador futuro que passe um productId direto, sem essa checagem prévia,
    // reintroduziria a mesma classe de bug que já corrigimos em outros 3 lugares.
    // Usa GetQueryTenantId() (AsyncLocal + fallback _globalTenantId), não
    // GetGlobalTenantId() sozinho — esse é seguro tanto na API quanto no WPF,
    // ao contrário do que usamos por engano em RoleRepository/TransferenciaService/
    // NfseEmissionService antes do fix.
    public async Task<bool> BaixarEstoqueAtomicoAsync(Guid productId, decimal quantidade, bool allowNegative)
    {
        int rows;
        var tenantId = AppDbContext.GetQueryTenantId();

        if (allowNegative)
        {
            // S3.7: ExecuteSqlInterpolatedAsync
            rows = await _ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET Stock = Stock - {quantidade} WHERE Id = {productId} AND TenantId = {tenantId}");
        }
        else
        {
            // {quantidade} referenciado duas vezes — variável local evita avaliar duas vezes
            var qtd = quantidade;
            rows = await _ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Products SET Stock = Stock - {qtd} WHERE Id = {productId} AND TenantId = {tenantId} AND Stock >= {qtd}");
        }

        return rows > 0;
    }

    public async Task<Product?> GetByBarcodeAsync(string barcode)
        => await _ctx.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Barcode == barcode);

    public async Task<Product?> GetBySkuAsync(string sku)
        => await _ctx.Products.AsNoTracking().FirstOrDefaultAsync(p => p.SKU == sku);

    public async Task<IEnumerable<Product>> SearchAsync(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return new List<Product>();

        var query = _ctx.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .AsQueryable();

        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var word in words)
        {
            query = query.Where(p =>
                p.Name.Contains(word) ||
                (p.Barcode != null && p.Barcode.Contains(word)) ||
                (p.SKU     != null && p.SKU.Contains(word)));
        }

        return await query
            .OrderByDescending(p => p.Barcode == term || p.SKU == term)
            .ThenByDescending(p => p.Name.StartsWith(words[0]))
            .ThenBy(p => p.Name)
            .Take(50)
            .ToListAsync();
    }

    public async Task<IEnumerable<Product>> GetLowStockAsync()
        => await _ctx.Products.AsNoTracking()
            .Where(p => p.IsActive && p.Stock <= p.MinStock)
            .ToListAsync();
}

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(AppDbContext ctx) : base(ctx) { }

    public async Task<Customer?> GetByDocumentAsync(string document)
    {
        // S8 FIX: removido IgnoreQueryFilters() — HasQueryFilter de tenant aplica.
        // Dedup de cliente por CPF/CNPJ é scoped ao tenant do usuário autenticado.
        // Pessoa física com mesmo CPF em duas lojas distintas são clientes separados.
        return await _ctx.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Document == document);
    }

    public async Task<IEnumerable<Customer>> SearchAsync(string term)
    {
        return await _ctx.Customers
            .AsNoTracking()
            .Where(c => c.Name.Contains(term) ||
                        c.Document.Contains(term) ||
                        (c.Phone != null && c.Phone.Contains(term)))
            .Take(50)
            .ToListAsync();
    }
}

public class SaleRepository : Repository<Sale>, ISaleRepository
{
    public SaleRepository(AppDbContext ctx) : base(ctx) { }

    public async Task<Sale?> GetWithItemsAsync(Guid id)
    {
        return await _ctx.Sales
            .Include(s => s.Customer)
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Sale>> GetSalesByPeriodAsync(DateTime startDate, DateTime endDate)
    {
        return await _ctx.Sales
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Payments)
            .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<SaleItem>> GetHistoricoVendasPorProdutoAsync(Guid productId)
    {
        return await _ctx.SaleItems
            .AsNoTracking()
            .Include(i => i.Sale).ThenInclude(s => s.Customer)
            .Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.Sale.SaleDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Sale>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        return await _ctx.Sales
            .AsNoTracking()
            .Include(s => s.Customer)
            .Include(s => s.Payments)
            .Include(s => s.Items)
            .Where(s => s.SaleDate >= from && s.SaleDate <= to)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Sale>> GetBySellerAsync(string sellerId)
    {
        return await _ctx.Sales
            .AsNoTracking()
            .Where(s => s.SellerId == sellerId)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTodayTotalAsync()
    {
        var today = DateTime.Today;
        return await _ctx.Sales
            .AsNoTracking()
            .Where(s => s.SaleDate >= today && s.Status != Domain.Enums.SaleStatus.Cancelada)
            .SumAsync(s => (decimal?)s.Total) ?? 0;
    }

    public async Task<decimal> GetMonthTotalAsync()
    {
        var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        return await _ctx.Sales
            .AsNoTracking()
            .Where(s => s.SaleDate >= first && s.Status != Domain.Enums.SaleStatus.Cancelada)
            .SumAsync(s => (decimal?)s.Total) ?? 0;
    }

    public async Task<decimal> GetAverageTicketAsync(DateTime from, DateTime to)
    {
        var q = _ctx.Sales
            .AsNoTracking()
            .Where(s => s.SaleDate >= from && s.SaleDate <= to &&
                        s.Status != Domain.Enums.SaleStatus.Cancelada);
        var count = await q.CountAsync();
        if (count == 0) return 0;
        return await q.AverageAsync(s => s.Total);
    }

    public async Task<IEnumerable<(Guid ProductId, string Name, decimal Quantity)>> GetTopProductsAsync(
        int count, DateTime from, DateTime to)
    {
        return await _ctx.SaleItems
            .AsNoTracking()
            .Include(i => i.Sale)
            .Where(i => i.Sale.SaleDate >= from && i.Sale.SaleDate <= to &&
                        i.Sale.Status != Domain.Enums.SaleStatus.Cancelada)
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new { g.Key.ProductId, g.Key.ProductName, Total = g.Sum(i => i.Quantity) })
            .OrderByDescending(x => x.Total)
            .Take(count)
            .Select(x => ValueTuple.Create(x.ProductId, x.ProductName, x.Total))
            .ToListAsync();
    }
}

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext ctx) : base(ctx) { }

    public async Task<Category?> GetByNameAsync(string name)
        => await _ctx.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Name == name);
}

public class BrandRepository    : Repository<Brand>,    IBrandRepository    { public BrandRepository(AppDbContext ctx)    : base(ctx) { } }
public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository { public AuditLogRepository(AppDbContext ctx) : base(ctx) { } }

public class DevolucaoRepository : IDevolucaoRepository
{
    private readonly AppDbContext _ctx;
    private readonly ERP.Application.Interfaces.IRequestTenant _tenant;

    // Fase 1.5 Fix: injetar IRequestTenant para usar o tenant da requisição HTTP.
    // Antes: AppDbContext.GetGlobalTenantId() retorna Guid.Empty na API,
    // fazendo devoluções via Portal/API ficarem com TenantId = Guid.Empty (órfãs).
    public DevolucaoRepository(AppDbContext ctx, ERP.Application.Interfaces.IRequestTenant tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    /// <summary>
    /// EXCEÇÃO S3.7: mantém ExecuteSqlRawAsync por ter 11 parâmetros posicionais.
    /// Todos os valores são Guid/decimal/string de domínio — nunca input direto do usuário.
    /// </summary>
    public async Task AddAsync(ERP.Domain.Entities.SaleItemDevolucao d)
    {
        // IRequestTenant.TenantId é correto em API (JWT) e WPF (estático via WpfRequestTenant).
        var tenantId = _tenant.TenantId;
        await _ctx.Database.ExecuteSqlRawAsync(@"
            INSERT INTO SaleItemDevolucoes
                (Id, TenantId, SaleId, ProductId, ProductName,
                 QuantidadeDevolvida, ValorDevolvido, Motivo, OperadorNome,
                 DataDevolucao, CreatedAt, UpdatedAt, IsDeleted)
            VALUES
                ({0}, {1}, {2}, {3}, {4},
                 {5}, {6}, {7}, {8},
                 {9}, {10}, NULL, 0)",
            Guid.NewGuid(), tenantId, d.SaleId, d.ProductId, d.ProductName,
            d.QuantidadeDevolvida, d.ValorDevolvido, d.Motivo ?? (object)DBNull.Value,
            d.OperadorNome ?? (object)DBNull.Value,
            d.DataDevolucao, DateTime.UtcNow);
    }

    public async Task<decimal> GetQuantidadeJaDevolvida(Guid saleId, Guid productId)
    {
        var result = await _ctx.Database.SqlQueryRaw<decimal>(@"
            SELECT ISNULL(SUM(QuantidadeDevolvida), 0) AS Value
            FROM SaleItemDevolucoes
            WHERE SaleId = {0} AND ProductId = {1} AND IsDeleted = 0",
            saleId, productId).FirstOrDefaultAsync();
        return result;
    }
}