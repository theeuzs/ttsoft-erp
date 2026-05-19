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

    public UnitOfWork(
        AppDbContext ctx,
        IProductRepository  products,
        ICustomerRepository customers,
        ISaleRepository     sales,
        ICategoryRepository categories,
        IUserRepository     users)
    {
        _ctx       = ctx;
        Products   = products;
        Customers  = customers;
        Sales      = sales;
        Categories = categories;
        Users      = users;

        Caixas        = new CaixaRepository(_ctx);
        Orcamentos    = new OrcamentoRepository(_ctx);
        ContasReceber = new ContaReceberRepository(_ctx);
        ContasPagar   = new ContaPagarRepository(_ctx);
        Suppliers     = new SupplierRepository(_ctx);
        NfePendentes  = new NfePendenteRepository(_ctx);
        PedidosCompra = new PedidoCompraRepository(_ctx);
        Brands        = new BrandRepository(_ctx);
        AuditLogs     = new AuditLogRepository(_ctx);
        Devolucoes    = new DevolucaoRepository(_ctx);
        Roles         = new RoleRepository(_ctx);
    }

    public async Task<int> CommitAsync() => await _ctx.SaveChangesAsync();

    public void Dispose()
    {
        if (!_disposed) { _ctx.Dispose(); _disposed = true; }
    }
}
