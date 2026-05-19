using Microsoft.Extensions.DependencyInjection;
using ERP.Domain.Common;
using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace ERP.Persistence.Context;

public class AppDbContext : DbContext
{
    // ══════════════════════════════════════════════════════════════════
    //  TenantId ESTÁTICO GLOBAL
    //  Setado UMA VEZ no App.xaml.cs antes de abrir o Login.
    //  Funciona perfeitamente em WPF onde não existe escopo HTTP.
    // ══════════════════════════════════════════════════════════════════
    private static Guid   _globalTenantId  = Guid.Empty;
    private static Guid   _currentUserId   = Guid.Empty;
    private static string _currentUserName = string.Empty;

    /// <summary>Chamado no login para identificar o usuário nos logs de auditoria.</summary>
    public static void SetCurrentUser(Guid userId, string userName)
    {
        _currentUserId   = userId;
        _currentUserName = userName;
    }

    /// <summary>
    /// Chamado no App.xaml.cs durante o startup, antes de qualquer query.
    /// </summary>
    public static void SetGlobalTenantId(Guid id) => _globalTenantId = id;
    public static Guid GetGlobalTenantId() => _globalTenantId;

    /// <summary>
    /// Método de instância — EF Core avalia por query (não cacheia o valor no modelo).
    /// Use sempre GetTenantId() nos HasQueryFilter, nunca var tid = _globalTenantId.
    /// </summary>
    public Guid GetTenantId() => _globalTenantId;

    // ── IRequestTenant (API) — isolado por requisição HTTP ───────────────
    private readonly Func<Guid>?   _tenantResolver;
    private readonly Func<string>? _userNameResolver;

    /// <summary>
    /// Construtor padrão para WPF — usa static global (1 tenant por processo).
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    /// <summary>
    /// Construtor para API — recebe IServiceProvider para resolver IRequestTenant
    /// Scoped por requisição, eliminando race condition entre tenants simultâneos.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, IServiceProvider sp)
        : base(options)
    {
        _tenantResolver    = null;
        _userNameResolver  = null;
    }



    // ── DbSets ────────────────────────────────────────────────────────────
    public DbSet<NfePendente>      NfePendentes       { get; set; }
    public DbSet<AuditLog>         AuditLogs          { get; set; }
    public DbSet<ChatMessage>      ChatMessages       => Set<ChatMessage>();
    public DbSet<Product>          Products           => Set<Product>();
    public DbSet<Brand>            Brands             { get; set; }
    public DbSet<Supplier>         Suppliers          { get; set; }
    public DbSet<Category>         Categories         => Set<Category>();
    public DbSet<Customer>         Customers          => Set<Customer>();
    public DbSet<Role>             Roles              { get; set; }
    public DbSet<Permission>       Permissions        { get; set; }
    public DbSet<Sale>             Sales              => Set<Sale>();
    public DbSet<SaleItem>         SaleItems          => Set<SaleItem>();
    public DbSet<User>             Users              { get; set; }
    public DbSet<Caixa>            Caixas             { get; set; }
    public DbSet<PedidoCompra>     PedidosCompra      { get; set; }
    public DbSet<PedidoCompraItem> PedidosCompraItens { get; set; }
    public DbSet<CaixaMovimento>   CaixaMovimentos    { get; set; }
    public DbSet<ContaReceber>     ContasReceber      { get; set; }
    public DbSet<ContaPagar>       ContasPagar        { get; set; }
    public DbSet<MovimentoHaver>   MovimentosHaver    { get; set; }
    public DbSet<Orcamento>        Orcamentos         { get; set; }
    public DbSet<OrcamentoItem>    OrcamentoItens     { get; set; }
    public DbSet<SaleItemDevolucao>   SaleItemDevolucoes   { get; set; }
    public DbSet<Branch>              Branches             { get; set; }
    public DbSet<ProductBranchStock>  ProductBranchStocks  { get; set; }
    public DbSet<PontosFidelidade>    PontosFidelidade     { get; set; }
    public DbSet<TransferenciaEstoque> Transferencias       { get; set; }
    public DbSet<TransferenciaItem>   TransferenciaItens   { get; set; }
    public DbSet<NfseEmitida>         NfseEmitidas         { get; set; }
    public DbSet<MetaVendas>          MetasVendas          { get; set; }
    // Sprint 1 — Produtos Agregados
    public DbSet<ProdutoAgregado>     ProdutosAgregados    { get; set; }
    // Sprint 2 — Logística
    public DbSet<Entrega>             Entregas             { get; set; }
    public DbSet<Veiculo>             Veiculos             { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        // ══════════════════════════════════════════════════════════════
        //  FILTROS GLOBAIS
        //  GetTenantId() é método de instância — EF Core avalia o valor
        //  a cada query, não cacheia no modelo. Isso garante isolamento
        //  correto entre tenants mesmo quando dois ERPs compartilham a
        //  mesma DLL e o mesmo banco de dados.
        // ══════════════════════════════════════════════════════════════

        if (_globalTenantId != Guid.Empty)
        {
            // ── Entidades com IsDeleted + TenantId ────────────────────
            modelBuilder.Entity<Product>().HasQueryFilter(
                p => !p.IsDeleted && p.TenantId == GetTenantId());
            modelBuilder.Entity<Customer>().HasQueryFilter(
                c => !c.IsDeleted && c.TenantId == GetTenantId());
            modelBuilder.Entity<Sale>().HasQueryFilter(
                s => !s.IsDeleted && s.TenantId == GetTenantId());
            modelBuilder.Entity<Caixa>().HasQueryFilter(
                c => !c.IsDeleted && c.TenantId == GetTenantId());
            modelBuilder.Entity<PedidoCompra>().HasQueryFilter(
                p => !p.IsDeleted && p.TenantId == GetTenantId());
            modelBuilder.Entity<Orcamento>().HasQueryFilter(
                o => !o.IsDeleted && o.TenantId == GetTenantId());
            modelBuilder.Entity<Category>().HasQueryFilter(
                c => !c.IsDeleted && c.TenantId == GetTenantId());
            modelBuilder.Entity<Brand>().HasQueryFilter(
                b => !b.IsDeleted && b.TenantId == GetTenantId());
            modelBuilder.Entity<Supplier>().HasQueryFilter(
                s => !s.IsDeleted && s.TenantId == GetTenantId());

            // ── Entidades sem IsDeleted, só TenantId ──────────────────
            modelBuilder.Entity<User>().HasQueryFilter(
                u => u.TenantId == GetTenantId());
            modelBuilder.Entity<Role>().HasQueryFilter(
                r => r.TenantId == GetTenantId());
            modelBuilder.Entity<Branch>().HasQueryFilter(
                b => b.TenantId == GetTenantId());
            modelBuilder.Entity<ProductBranchStock>().HasQueryFilter(
                s => s.TenantId == GetTenantId());
            modelBuilder.Entity<TransferenciaEstoque>().HasQueryFilter(
                t => t.TenantId == GetTenantId());
            modelBuilder.Entity<ContaReceber>().HasQueryFilter(
                c => c.TenantId == GetTenantId());
            modelBuilder.Entity<ContaPagar>().HasQueryFilter(
                c => c.TenantId == GetTenantId());

            // ── Sprint 1 — ProdutoAgregado ────────────────────────────
            modelBuilder.Entity<ProdutoAgregado>().HasQueryFilter(
                pa => !pa.IsDeleted && pa.TenantId == GetTenantId());

            // ── Sprint 2 — Logística ──────────────────────────────────
            modelBuilder.Entity<Entrega>().HasQueryFilter(
                e => !e.IsDeleted && e.TenantId == GetTenantId());
            modelBuilder.Entity<Veiculo>().HasQueryFilter(
                v => !v.IsDeleted && v.TenantId == GetTenantId());
        }
        else
        {
            // Design-time (migrations): só soft-delete, sem filtro de tenant
            modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Sale>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<Caixa>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<PedidoCompra>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<Orcamento>().HasQueryFilter(o => !o.IsDeleted);
            modelBuilder.Entity<Category>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<Brand>().HasQueryFilter(b => !b.IsDeleted);
            modelBuilder.Entity<Supplier>().HasQueryFilter(s => !s.IsDeleted);
            // Sprint 1
            modelBuilder.Entity<ProdutoAgregado>().HasQueryFilter(pa => !pa.IsDeleted);
            // Sprint 2
            modelBuilder.Entity<Entrega>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<Veiculo>().HasQueryFilter(v => !v.IsDeleted);
        }

        // ── ProdutoAgregado: many-to-many auto-referenciante (Sprint 1) ───────
        modelBuilder.Entity<ProdutoAgregado>(e =>
        {
            e.ToTable("ProdutosAgregados");
            e.HasKey(pa => pa.Id);

            e.HasOne(pa => pa.ProdutoPrincipal)
                .WithMany(p => p.AgregadosPrincipais)
                .HasForeignKey(pa => pa.ProdutoPrincipalId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(pa => pa.ProdutoRelacionado)
                .WithMany(p => p.AgregadosRelacionados)
                .HasForeignKey(pa => pa.ProdutoRelacionadoId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(pa => new { pa.ProdutoPrincipalId, pa.ProdutoRelacionadoId })
                .IsUnique();
        });
    }

    // ── Auditoria automática ─────────────────────────────────────────────
    private List<AuditLog> GerarLogsAuditoria()
    {
        var logs = new List<AuditLog>();
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog
                     && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            string acao = entry.State switch
            {
                EntityState.Added    => "INSERT",
                EntityState.Modified => "UPDATE",
                EntityState.Deleted  => "DELETE",
                _                    => "UNKNOWN"
            };

            string? oldValues = null;
            string? newValues = null;

            if (entry.State == EntityState.Modified)
            {
                var propsModificadas = entry.Properties.Where(p => p.IsModified).ToList();
                if (!propsModificadas.Any()) continue;

                var orig = new Dictionary<string, object?>();
                var curr = new Dictionary<string, object?>();

                foreach (var prop in propsModificadas)
                {
                    orig[prop.Metadata.Name] = FormatarParaAuditoria(prop.OriginalValue);
                    curr[prop.Metadata.Name] = FormatarParaAuditoria(prop.CurrentValue);
                }

                oldValues = JsonSerializer.Serialize(orig);
                newValues = JsonSerializer.Serialize(curr);
            }
            else if (entry.State == EntityState.Deleted)
            {
                var orig = new Dictionary<string, object?>();
                foreach (var prop in entry.OriginalValues.Properties)
                    orig[prop.Name] = FormatarParaAuditoria(entry.OriginalValues[prop]);
                oldValues = JsonSerializer.Serialize(orig);
            }
            else if (entry.State == EntityState.Added)
            {
                var camposTecnicos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "TenantId", "CreatedAt", "UpdatedAt", "IsDeleted" };

                var curr = new Dictionary<string, object?>();
                foreach (var prop in entry.CurrentValues.Properties)
                {
                    if (camposTecnicos.Contains(prop.Name)) continue;
                    var val = entry.CurrentValues[prop];
                    if (val != null) curr[prop.Name] = FormatarParaAuditoria(val);
                }
                newValues = JsonSerializer.Serialize(curr);
            }

            var entityId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();

            logs.Add(new AuditLog
            {
                Id          = Guid.NewGuid(),
                TenantId    = _globalTenantId,
                UserId      = _currentUserId == Guid.Empty ? null : _currentUserId,
                UserName    = _currentUserName,
                Action      = acao,
                EntityType  = entry.Entity.GetType().Name,
                EntityId    = entityId,
                Timestamp   = DateTime.Now,
                MachineName = Environment.MachineName,
                OldValues   = oldValues,
                NewValues   = newValues,
                CreatedAt   = DateTime.UtcNow,
            });
        }

        return logs;
    }

    private static object? FormatarParaAuditoria(object? valor)
    {
        if (valor == null) return null;
        return valor switch
        {
            decimal d  => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            double  d  => d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            float   f  => f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            bool    b  => b,
            Guid    g  => g.ToString(),
            _          => valor
        };
    }

    // ── Auto-fill TenantId e UpdatedAt em todo SaveChanges ───────────────
    private void PreencherTenantIdEUpdatedAt()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && _globalTenantId != Guid.Empty)
            {
                if (entry.Entity is BaseEntity baseEntity && baseEntity.TenantId == Guid.Empty)
                    baseEntity.TenantId = _globalTenantId;

                if (entry.Entity is ContaReceber cr && cr.TenantId == Guid.Empty)
                    cr.TenantId = _globalTenantId;

                if (entry.Entity is ContaPagar cp && cp.TenantId == Guid.Empty)
                    cp.TenantId = _globalTenantId;
            }

            if (entry.State == EntityState.Modified && entry.Entity is BaseEntity be)
                be.UpdatedAt = DateTime.UtcNow;
        }
    }

    public override int SaveChanges()
    {
        PreencherTenantIdEUpdatedAt();
        var auditLogs = GerarLogsAuditoria();
        if (auditLogs.Any()) AuditLogs.AddRange(auditLogs);
        var result = base.SaveChanges();
        ChangeTracker.Clear();
        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        PreencherTenantIdEUpdatedAt();
        var auditLogs = GerarLogsAuditoria();
        if (auditLogs.Any()) AuditLogs.AddRange(auditLogs);
        var result = await base.SaveChangesAsync(ct);
        ChangeTracker.Clear();
        return result;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) { }
}