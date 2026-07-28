using ERP.Domain.Entities;

namespace ERP.Domain.Interfaces;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetByBarcodeAsync(string barcode);
    Task<Product?> GetBySkuAsync(string sku);
    Task<IEnumerable<Product>> SearchAsync(string term);
    Task<IEnumerable<Product>> GetLowStockAsync();

    /// <summary>
    /// Baixa o estoque atomicamente via SQL direto.
    /// Retorna false se o estoque for insuficiente (race condition seguro).
    /// </summary>
    Task<bool> BaixarEstoqueAtomicoAsync(Guid productId, decimal quantidade, bool allowNegative);
}

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> GetByDocumentAsync(string document);
    Task<IEnumerable<Customer>> SearchAsync(string term);

    /// <summary>
    /// Debita o saldo Haver do cliente atomicamente (UPDATE relativo com guarda
    /// de saldo suficiente) — mesmo padrão do BaixarEstoqueAtomicoAsync. Antes:
    /// `customer.HaverBalance -= x` lido/escrito em C# a partir de uma leitura
    /// AsNoTracking tinha o mesmo lost-update que já corrigimos no crediário.
    /// Retorna false se o saldo não é mais suficiente no momento exato da escrita.
    /// </summary>
    Task<bool> DebitarHaverAtomicoAsync(Guid customerId, decimal valor);
}

public interface ISaleRepository : IRepository<Sale>
{
    Task<Sale?> GetWithItemsAsync(Guid id);
    Task<IEnumerable<Sale>> GetByDateRangeAsync(DateTime from, DateTime to);
    Task<IEnumerable<Sale>> GetBySellerAsync(string sellerId);
    Task<decimal> GetTodayTotalAsync();
    Task<decimal> GetMonthTotalAsync();
    Task<decimal> GetAverageTicketAsync(DateTime from, DateTime to);
    Task<IEnumerable<(Guid ProductId, string Name, decimal Quantity)>> GetTopProductsAsync(int count, DateTime from, DateTime to);
    // Uma busca otimizada só para relatórios
    Task<IEnumerable<Sale>> GetSalesByPeriodAsync(DateTime startDate, DateTime endDate);

    /// <summary>
    /// Todo item de venda já lançado para um produto específico, com a venda
    /// (cliente, data, número) incluída — histórico de vendas por produto.
    /// </summary>
    Task<IEnumerable<SaleItem>> GetHistoricoVendasPorProdutoAsync(Guid productId);
}

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetByNameAsync(string name);
}

public interface IBrandRepository    : IRepository<Brand>    { }
public interface IUserRepository2    : IRepository<User>     { }
public interface IAuditLogRepository : IRepository<AuditLog> { }