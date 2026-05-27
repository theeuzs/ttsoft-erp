using ERP.Application.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace ERP.Persistence.Context;

public class AppDbContext : DbContext
{
    // ══════════════════════════════════════════════════════════════════
    //  TenantId ESTÁTICO — mantido SOMENTE para o WPF Desktop e para
    //  PreencherTenantIdEUpdatedAt em cenários sem IRequestTenant.
    //  Na API esses campos NÃO são usados para filtragem.
    // ══════════════════════════════════════════════════════════════════
    private static Guid   _globalTenantId  = Guid.Empty;
    private static Guid   _currentUserId   = Guid.Empty;
    private static string _currentUserName = string.Empty;

    /// <summary>Chamado no WPF/login para identificar o usuário nos logs de auditoria.</summary>
    public static void SetCurrentUser(Guid userId, string userName)
    {
        _currentUserId   = userId;
        _currentUserName = userName;
    }

    /// <summary>
    /// Chamado no App.xaml.cs do WPF durante o startup.
    /// Na API este método NÃO é chamado — o tenant vem do IRequestTenant (Scoped).
    /// </summary>
    public static void SetGlobalTenantId(Guid id) => _globalTenantId = id;
    public static Guid   GetGlobalTenantId()   => _globalTenantId;
    public static Guid   GetCurrentUserId()    => _currentUserId;
    public static string GetCurrentUserName()  => _currentUserName;

    // ── IRequestTenant — isolado por requisição HTTP (API) ou por processo (WPF) ───
    private readonly IRequestTenant? _requestTenant;

    /// <summary>
    /// Retorna o TenantId correto para a instância atual:
    ///   • API:        _requestTenant.TenantId  — scoped por requisição HTTP
    ///   • WPF:        _globalTenantId          — setado uma vez no startup
    ///   • Design-time: Guid.Empty              — migrations sem filtro de tenant
    /// </summary>
    public Guid GetTenantId() => _requestTenant?.TenantId ?? _globalTenantId;

    /// <summary>
    /// Construtor para o WPF Desktop — não injeta IRequestTenant.
    /// O tenant vem de _globalTenantId (setado via SetGlobalTenantId).
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    /// <summary>
    /// Construtor para a API — recebe IRequestTenant Scoped.
    /// Cada requisição HTTP tem seu próprio scope, logo seu próprio TenantId.
    /// Elimina completamente a race condition entre tenants simultâneos.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, IRequestTenant requestTenant)
        : base(options)
    {
        _requestTenant = requestTenant;
    }

    // ── DbSets ────────────────────────────────────────────────────────────
    public DbSet<NfePendente>          NfePendentes        { get; set; }
    public DbSet<AuditLog>             AuditLogs           { get; set; }
    public DbSet<ChatMessage>          ChatMessages        => Set<ChatMessage>();
    public DbSet<Product>              Products            => Set<Product>();
    public DbSet<Brand>                Brands              { get; set; }
    public DbSet<Supplier>             Suppliers           { get; set; }
    public DbSet<Category>             Categories          => Set<Category>();
    public DbSet<Customer>             Customers           => Set<Customer>();
    public DbSet<Role>                 Roles               { get; set; }
    public DbSet<Permission>           Permissions         { get; set; }
    public DbSet<Sale>                 Sales               => Set<Sale>();
    public DbSet<SaleItem>             SaleItems           => Set<SaleItem>();
    public DbSet<User>                 Users               { get; set; }
    public DbSet<Caixa>                Caixas              { get; set; }
    public DbSet<PedidoCompra>         PedidosCompra       { get; set; }
    public DbSet<PedidoCompraItem>     PedidosCompraItens  { get; set; }
    public DbSet<CaixaMovimento>       CaixaMovimentos     { get; set; }
    public DbSet<ContaReceber>         ContasReceber       { get; set; }
    public DbSet<ContaPagar>           ContasPagar         { get; set; }
    public DbSet<MovimentoHaver>       MovimentosHaver     { get; set; }
    public DbSet<Orcamento>            Orcamentos          { get; set; }
    public DbSet<OrcamentoItem>        OrcamentoItens      { get; set; }
    public DbSet<SaleItemDevolucao>    SaleItemDevolucoes  { get; set; }
    public DbSet<Branch>               Branches            { get; set; }
    public DbSet<ProductBranchStock>   ProductBranchStocks { get; set; }
    public DbSet<PontosFidelidade>     PontosFidelidade    { get; set; }
    public DbSet<TransferenciaEstoque> Transferencias      { get; set; }
    public DbSet<TransferenciaItem>    TransferenciaItens  { get; set; }
    public DbSet<NfseEmitida>          NfseEmitidas        { get; set; }
    public DbSet<MetaVendas>           MetasVendas         { get; set; }
    public DbSet<ProdutoAgregado>      ProdutosAgregados   { get; set; }
    public DbSet<Entrega>              Entregas            { get; set; }
    public DbSet<Veiculo>              Veiculos            { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        // ══════════════════════════════════════════════════════════════
        //  FILTROS GLOBAIS
        //
        //  IMPORTANTE: os filtros são SEMPRE registrados, independente
        //  de qual construtor foi usado. GetTenantId() é avaliado
        //  POR QUERY no contexto correto:
        //    • API:  retorna o TenantId do JWT da requisição atual
        //    • WPF:  retorna o TenantId lido do licenca.json
        //    • Migrations: retorna Guid.Empty (não afeta o schema)
        //
        //  Remover o antigo `if (_globalTenantId != Guid.Empty)` era
        //  o bug principal: na API _globalTenantId é sempre Guid.Empty,
        //  então o bloco inteiro era ignorado e NENHUM filtro de tenant
        //  era aplicado, expondo dados de todos os tenants.
        // ══════════════════════════════════════════════════════════════

        // ── Entidades com IsDeleted + TenantId ────────────────────────
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
        modelBuilder.Entity<ProdutoAgregado>().HasQueryFilter(
            pa => !pa.IsDeleted && pa.TenantId == GetTenantId());
        modelBuilder.Entity<Entrega>().HasQueryFilter(
            e => !e.IsDeleted && e.TenantId == GetTenantId());
        modelBuilder.Entity<Veiculo>().HasQueryFilter(
            v => !v.IsDeleted && v.TenantId == GetTenantId());

        // ── Entidades sem IsDeleted — só TenantId ─────────────────────
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

        // ── Fidelidade e Haver — agora com isolamento de tenant ───────
        // PontosFidelidade herda BaseEntity (tem TenantId).
        // MovimentoHaver deve ter TenantId para filtrar corretamente.
        modelBuilder.Entity<PontosFidelidade>().HasQueryFilter(
            p => p.TenantId == GetTenantId());
        modelBuilder.Entity<MovimentoHaver>().HasQueryFilter(
            m => m.TenantId == GetTenantId());

        // ── ProdutoAgregado: many-to-many auto-referenciante ──────────
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

    // ── Auditoria automática ──────────────────────────────────────────
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

            // Usa GetTenantId() — funciona tanto no WPF quanto na API
            var tenantId  = GetTenantId();
            var userId    = _requestTenant?.UserId    ?? _currentUserId;
            var userName  = _requestTenant?.UserName  ?? _currentUserName;

            logs.Add(new AuditLog
            {
                Id          = Guid.NewGuid(),
                TenantId    = tenantId,
                UserId      = userId   == Guid.Empty ? null : userId,
                UserName    = userName,
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

    // ── Auto-fill TenantId e UpdatedAt em todo SaveChanges ────────────
    private void PreencherTenantIdEUpdatedAt()
    {
        var tenantId = GetTenantId(); // Correto para API e WPF
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && tenantId != Guid.Empty)
            {
                if (entry.Entity is BaseEntity baseEntity && baseEntity.TenantId == Guid.Empty)
                    baseEntity.TenantId = tenantId;

                if (entry.Entity is ContaReceber cr && cr.TenantId == Guid.Empty)
                    cr.TenantId = tenantId;

                if (entry.Entity is ContaPagar cp && cp.TenantId == Guid.Empty)
                    cp.TenantId = tenantId;
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
