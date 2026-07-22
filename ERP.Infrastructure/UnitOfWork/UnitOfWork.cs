using ERP.Domain.Interfaces;
using ERP.Infrastructure.Repositories;
using ERP.Persistence.Context;

namespace ERP.Infrastructure.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _ctx;
    private bool _disposed;

    public IProductRepository       Products      { get; }
    public ICustomerRepository      Customers     { get; }
    public ISaleRepository          Sales         { get; }
    public ICategoryRepository      Categories    { get; }
    public ICaixaRepository         Caixas        { get; }
    public IContaBancariaRepository ContasBancarias { get; }
    public IOperadoraRecebimentoRepository OperadorasRecebimento { get; }
    public IRecebivelOperadoraRepository RecebiveisOperadora { get; }
    public IVendaSuspensaRepository VendasSuspensas { get; }
    public IContaReceberRepository  ContasReceber { get; }
    public IContaPagarRepository    ContasPagar   { get; }
    public IOrcamentoRepository     Orcamentos    { get; }
    public ISupplierRepository      Suppliers     { get; }
    public INfePendenteRepository   NfePendentes  { get; }
    public IPedidoCompraRepository  PedidosCompra { get; }
    public IBrandRepository         Brands        { get; }    // ← NOVO
    public IUserRepository          Users         { get; }    // ← NOVO
    public IAuditLogRepository      AuditLogs     { get; }    // ← NOVO
    public IDevolucaoRepository     Devolucoes    { get; }
    public IRoleRepository          Roles         { get; }
    public IOrderSyncRepository     OrderSync     { get; }

    public UnitOfWork(
        AppDbContext ctx,
        IProductRepository  products,
        ICustomerRepository customers,
        ISaleRepository     sales,
        ICategoryRepository categories,
        IUserRepository     users,
        ERP.Application.Interfaces.IRequestTenant requestTenant)
    {
        _ctx       = ctx;
        Products   = products;
        Customers  = customers;
        Sales      = sales;
        Categories = categories;
        Users      = users;

        Caixas        = new CaixaRepository(_ctx);
        ContasBancarias = new ContaBancariaRepository(_ctx);
        OperadorasRecebimento = new OperadoraRecebimentoRepository(_ctx);
        RecebiveisOperadora = new RecebivelOperadoraRepository(_ctx);
        VendasSuspensas = new VendaSuspensaRepository(_ctx);
        Orcamentos    = new OrcamentoRepository(_ctx);
        ContasReceber = new ContaReceberRepository(_ctx);
        ContasPagar   = new ContaPagarRepository(_ctx);
        Suppliers     = new SupplierRepository(_ctx);
        NfePendentes  = new NfePendenteRepository(_ctx);
        PedidosCompra = new PedidoCompraRepository(_ctx);
        Brands        = new BrandRepository(_ctx);
        AuditLogs     = new AuditLogRepository(_ctx);
        Devolucoes    = new DevolucaoRepository(_ctx, requestTenant);
        // TENANT FIX: RoleRepository agora recebe IRequestTenant (ver RoleRepository.cs)
        Roles         = new RoleRepository(_ctx, requestTenant);
        OrderSync     = new OrderSyncRepository(_ctx);
    }

    public async Task<int> CommitAsync() => await _ctx.SaveChangesAsync();

    /// <summary>
    /// Abre uma transação no banco. A baixa de estoque (ExecuteSqlInterpolated)
    /// e o SaveChanges da Sale são executados na mesma transação — garante
    /// que se qualquer parte falhar, nada é persistido.
    /// Retorna ITransaction (abstração do Domain) para manter o Application
    /// desacoplado de EF Core.
    /// </summary>
    public async Task<ITransaction> BeginTransactionAsync()
    {
        var efTx = await _ctx.Database.BeginTransactionAsync();
        return new EfTransaction(efTx);
    }

    public void Dispose()
    {
        if (!_disposed) { _ctx.Dispose(); _disposed = true; }
    }
}

/// <summary>
/// Wrapper que adapta IDbContextTransaction (EF Core) para ITransaction (Domain).
/// Fica na camada Infrastructure — é a única que conhece EF Core.
/// </summary>
internal sealed class EfTransaction : ITransaction
{
    private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _tx;

    public EfTransaction(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx)
        => _tx = tx;

    public Task CommitAsync(CancellationToken ct = default)  => _tx.CommitAsync(ct);
    public Task RollbackAsync(CancellationToken ct = default) => _tx.RollbackAsync(ct);
    public ValueTask DisposeAsync() => _tx.DisposeAsync();
}