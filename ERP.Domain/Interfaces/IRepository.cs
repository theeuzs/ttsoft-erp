// ── ERP.Domain/Interfaces/IRepository.cs ─────────────────────────────────────
using System.Linq.Expressions;

namespace ERP.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    /// <summary>Busca rastreada explícita para Update+Auditoria via ChangeTracker.</summary>
    Task<T?> GetByIdTrackedAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Retorna uma página de registros, ordenados pela expressão informada.
    /// </summary>
    Task<(IEnumerable<T> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        Expression<Func<T, bool>>?  filter  = null,
        Expression<Func<T, object>>? orderBy = null);

    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
    /// <summary>Atualiza CurrentValues mantendo OriginalValues para auditoria correta. Aceita entidade ou DTO.</summary>
    void SetValues(T existente, object novosValores);
}

public interface IDevolucaoRepository
{
    Task AddAsync(ERP.Domain.Entities.SaleItemDevolucao devolucao);
    /// <summary>Retorna a quantidade total já devolvida de um produto em uma venda.</summary>
    Task<decimal> GetQuantidadeJaDevolvida(Guid saleId, Guid productId);
}

public interface IUnitOfWork : IDisposable
{
    IProductRepository  Products     { get; }
    ICustomerRepository Customers    { get; }
    ISaleRepository     Sales        { get; }
    ICategoryRepository Categories   { get; }
    ICaixaRepository    Caixas       { get; }
    IContaBancariaRepository ContasBancarias { get; }
    IOperadoraRecebimentoRepository OperadorasRecebimento { get; }
    IRecebivelOperadoraRepository RecebiveisOperadora { get; }
    IVendaSuspensaRepository VendasSuspensas { get; }
    IOrcamentoRepository Orcamentos  { get; }
    IContaReceberRepository ContasReceber { get; }
    IContaPagarRepository   ContasPagar  { get; }
    INfePendenteRepository  NfePendentes { get; }
    ISupplierRepository     Suppliers    { get; }
    IPedidoCompraRepository PedidosCompra { get; }
    IBrandRepository        Brands        { get; }
    IUserRepository         Users         { get; }
    IAuditLogRepository     AuditLogs     { get; }
    IDevolucaoRepository    Devolucoes    { get; }
    IRoleRepository         Roles         { get; }
    IOrderSyncRepository    OrderSync     { get; }
    Task<int> CommitAsync();
    /// <summary>
    /// Inicia uma transação explícita no banco de dados.
    /// Use para operações que precisam ser atômicas (ex: baixa de estoque + criação de venda).
    /// O caller é responsável por chamar CommitAsync e DisposeAsync na transação retornada.
    /// </summary>
    Task<ITransaction> BeginTransactionAsync();
}