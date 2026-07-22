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

    // ── AsyncLocal: TenantId por contexto async (resolve o problema de caching do EF InMemory) ─
    // EF Core InMemory NÃO rebind "this" no HasQueryFilter compilado — usa a primeira instância.
    // AsyncLocal<Guid> garante que cada async context (request/test) tem seu próprio TenantId.
    // O HasQueryFilter chama AppDbContext.GetQueryTenantId() como método ESTÁTICO,
    // que EF Core avalia fresh a cada query (não compila como closure sobre "this").
    private static readonly AsyncLocal<Guid> _asyncTenantId = new();

    /// <summary>Define o TenantId para o contexto async atual (por request/test).</summary>
    public static void SetQueryTenantId(Guid id) => _asyncTenantId.Value = id;
    /// <summary>
    /// Lê o TenantId para o HasQueryFilter.
    /// Cascata de 3 fontes — garante funcionamento em API (AsyncLocal via TenantMiddleware),
    /// WPF (globalTenantId, setado no startup) e testes (AsyncLocal via SetQueryTenantId).
    ///
    /// ATENÇÃO — thread safety no WPF:
    /// AsyncLocal NÃO é visível em threads do thread pool que não originam do
    /// contexto onde foi setado. O WPF usa EF Core async (thread pool), então
    /// AsyncLocal definido no constructor do AppDbContext pode ser perdido.
    /// Por isso a cascata inclui _globalTenantId como fallback estável para o WPF.
    /// </summary>
    public static Guid GetQueryTenantId()
    {
        // 1. AsyncLocal: setado pelo TenantMiddleware (API) ou SetQueryTenantId (testes)
        var asyncVal = _asyncTenantId.Value;
        if (asyncVal != Guid.Empty) return asyncVal;

        // 2. Global estático: setado no startup do WPF via SetGlobalTenantId()
        // Fallback necessário porque AsyncLocal pode não fluir para threads do pool no WPF.
        if (_globalTenantId != Guid.Empty) return _globalTenantId;

        // 3. IRequestTenant (API): lido via construtor do AppDbContext
        return Guid.Empty;
    }

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
    /// <remarks>
    /// ATENÇÃO — não otimize este método para retornar um campo diretamente:
    ///   ERRADO: p => p.TenantId == _requestTenant.TenantId  (captura o valor no build do modelo — congela o primeiro tenant)
    ///   CERTO:  p => p.TenantId == tenantFilter.Value             (EF Core avalia a chamada por query, no contexto correto)
    /// O EF Core 8 reconhece GetTenantId() como referência captiva avaliada por query.
    /// Trocar para acesso direto ao campo faz o modelo cachear o primeiro tenant e vazar dados.
    /// </remarks>
    /// Resolve o TenantId para gravação/validação de entidades.
    /// Ordem de prioridade (fix 1.7.6):
    ///   1. _requestTenant.TenantId  — scoped da requisição HTTP corrente
    ///   2. _asyncTenantId.Value     — AsyncLocal, flui para CreateScope filhos
    ///   3. _globalTenantId          — estático do WPF
    /// Antes do fix, CreateScope + GetGlobalTenantId em TransferenciaService/NfseEmissionService/RoleRepository
    /// retornava Guid.Empty porque o IRequestTenant do scope-filho não era populado
    /// e _globalTenantId é sempre Guid.Empty na API. Com o fallback para AsyncLocal,
    /// o tenant flui automaticamente para qualquer scope-filho criado durante a requisição.
    public Guid GetTenantId()
    {
        if (_requestTenant?.TenantId is Guid rt && rt != Guid.Empty) return rt;
        var asyncId = _asyncTenantId.Value;
        if (asyncId != Guid.Empty) return asyncId;
        return _globalTenantId;
    }

    /// <summary>
    /// Construtor para o WPF Desktop — não injeta IRequestTenant.
    /// O tenant vem de _globalTenantId (setado via SetGlobalTenantId no login).
    /// NÃO copia _globalTenantId para _asyncTenantId aqui: o _globalTenantId
    /// no startup (licenca.json) pode ser diferente do tenant do usuário logado.
    /// GetQueryTenantId() lê _globalTenantId diretamente como fallback.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        // S13 FIX: NÃO setar _asyncTenantId.Value = _globalTenantId aqui.
        // O construtor roda durante o startup com o TenantId do licenca.json,
        // mas o tenant correto só é conhecido após o login do usuário.
        // GetQueryTenantId() cai no fallback _globalTenantId (atualizado no login).
    }

    /// <summary>
    /// Construtor para a API — recebe IRequestTenant Scoped.
    /// Cada requisição HTTP tem seu próprio scope, logo seu próprio TenantId.
    /// Elimina completamente a race condition entre tenants simultâneos.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options, IRequestTenant requestTenant)
        : base(options)
    {
        _requestTenant = requestTenant;
        // Propaga o TenantId para o AsyncLocal APENAS quando temos um tenant real.
        // Quando requestTenant.TenantId == Guid.Empty (ex: seed em teste sem request HTTP),
        // NÃO sobrescreve — evita que o construtor apague um valor válido setado pelo
        // TenantMiddleware ou pelo TenantScope antes da criação do contexto.
        if (requestTenant.TenantId != Guid.Empty)
            _asyncTenantId.Value = requestTenant.TenantId;
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
    public DbSet<ContaBancaria>          ContasBancarias         { get; set; }
    public DbSet<MovimentoContaBancaria> MovimentosContaBancaria { get; set; }
    public DbSet<OperadoraRecebimento>   OperadorasRecebimento   { get; set; }
    public DbSet<RecebivelOperadora>     RecebiveisOperadora     { get; set; }
    public DbSet<VendaSuspensa>          VendasSuspensas         { get; set; }
    public DbSet<VendaSuspensaItem>      VendaSuspensaItens      { get; set; }
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
    public DbSet<FormulaTintometrica>  FormulasTintometricas { get; set; }

    // ── Módulo Integrações (Marketplace) ──────────────────────────────────
    public DbSet<SalesChannel>              SalesChannels              { get; set; }
    public DbSet<SalesChannelPricingPolicy> SalesChannelPricingPolicies { get; set; }
    public DbSet<ExternalOrder>             ExternalOrders             { get; set; }
    public DbSet<ExternalOrderItem>         ExternalOrderItems         { get; set; }
    public DbSet<SkuMapping>                SkuMappings                { get; set; }
    public DbSet<ShadowStockReservation>    ShadowStockReservations    { get; set; }
    public DbSet<OrderEvent>                OrderEvents                { get; set; }
    public DbSet<OrderAction>               OrderActions               { get; set; }
    public DbSet<OrderConflict>             OrderConflicts             { get; set; }
    public DbSet<ProcessingSession>         ProcessingSessions         { get; set; }

    // ── TenantFilterHelper: cascata AsyncLocal → _globalTenantId ──────────────
    // Usado pelo HasQueryFilter em vez de _asyncTenantId diretamente.
    // Propriedade de instância em objeto capturado → EF Core avalia por query (não cacheia).
    // S13 FIX: quando AsyncLocal está vazio (WPF sem SetQueryTenantId explícito),
    // cai no _globalTenantId atualizado no login — corrige "Produto não encontrado" no WPF.
    private sealed class TenantFilterHelper
    {
        private readonly AsyncLocal<Guid> _asyncLocal;
        public TenantFilterHelper(AsyncLocal<Guid> asyncLocal) => _asyncLocal = asyncLocal;
        public Guid Value => _asyncLocal.Value != Guid.Empty ? _asyncLocal.Value : _globalTenantId;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        // S13 FIX: usa TenantFilterHelper em vez de _asyncTenantId diretamente.
        // tenantFilter.Value cascateia: AsyncLocal (API/testes) → _globalTenantId (WPF).
        // EF Core avalia propriedades de instância em closures por query (não cacheia),
        // ao contrário de chamadas de método estático sem argumentos (cacheadas no InMemory).
        var tenantFilter = new TenantFilterHelper(_asyncTenantId);

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
            p => !p.IsDeleted && p.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Customer>().HasQueryFilter(
            c => !c.IsDeleted && c.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Sale>().HasQueryFilter(
            s => !s.IsDeleted && s.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Caixa>().HasQueryFilter(
            c => !c.IsDeleted && c.TenantId == tenantFilter.Value);
        modelBuilder.Entity<ContaBancaria>().HasQueryFilter(
            c => !c.IsDeleted && c.TenantId == tenantFilter.Value);
        // S17: MovimentoContaBancaria ganha filtro próprio (defesa em profundidade) —
        // diferente de CaixaMovimento, que hoje só é seguro porque sempre é consultado
        // a partir de um CaixaId já filtrado por tenant a montante.
        modelBuilder.Entity<MovimentoContaBancaria>().HasQueryFilter(
            m => !m.IsDeleted && m.TenantId == tenantFilter.Value);
        modelBuilder.Entity<OperadoraRecebimento>().HasQueryFilter(
            o => !o.IsDeleted && o.TenantId == tenantFilter.Value);
        modelBuilder.Entity<RecebivelOperadora>().HasQueryFilter(
            r => !r.IsDeleted && r.TenantId == tenantFilter.Value);
        modelBuilder.Entity<VendaSuspensa>().HasQueryFilter(
            v => !v.IsDeleted && v.TenantId == tenantFilter.Value);
        modelBuilder.Entity<VendaSuspensaItem>().HasQueryFilter(
            i => !i.IsDeleted && i.TenantId == tenantFilter.Value);

        // S17 FIX: precisão decimal explícita — sem isso, EF usa um default que pode
        // truncar valor monetário silenciosamente. Aviso apareceu na migration (o mesmo
        // gap já existe em várias entidades antigas do projeto, mas essas duas são novas
        // e financeiras — não faz sentido introduzir o mesmo problema de novo).
        modelBuilder.Entity<ContaBancaria>().Property(c => c.SaldoInicial).HasPrecision(18, 2);
        modelBuilder.Entity<MovimentoContaBancaria>().Property(m => m.Valor).HasPrecision(18, 2);
        modelBuilder.Entity<OperadoraRecebimento>().Property(o => o.TaxaDebitoPercentual).HasPrecision(5, 2);
        modelBuilder.Entity<OperadoraRecebimento>().Property(o => o.TaxaCreditoVistaPercentual).HasPrecision(5, 2);
        modelBuilder.Entity<OperadoraRecebimento>().Property(o => o.TaxaCreditoParceladoPercentual).HasPrecision(5, 2);
        modelBuilder.Entity<RecebivelOperadora>().Property(r => r.ValorBruto).HasPrecision(18, 2);
        modelBuilder.Entity<RecebivelOperadora>().Property(r => r.ValorTaxa).HasPrecision(18, 2);
        modelBuilder.Entity<RecebivelOperadora>().Property(r => r.ValorLiquido).HasPrecision(18, 2);
        modelBuilder.Entity<VendaSuspensa>().Property(v => v.TotalAproximado).HasPrecision(18, 2);
        modelBuilder.Entity<VendaSuspensaItem>().Property(i => i.NormalUnitPrice).HasPrecision(18, 4);
        modelBuilder.Entity<VendaSuspensaItem>().Property(i => i.UnitPrice).HasPrecision(18, 4);
        modelBuilder.Entity<VendaSuspensaItem>().Property(i => i.Quantity).HasPrecision(18, 4);
        modelBuilder.Entity<VendaSuspensaItem>().Property(i => i.FatorConversao).HasPrecision(18, 4);
        modelBuilder.Entity<VendaSuspensaItem>().Property(i => i.WholesalePrice).HasPrecision(18, 4);
        modelBuilder.Entity<VendaSuspensaItem>().Property(i => i.WholesaleMinQuantity).HasPrecision(18, 4);
        modelBuilder.Entity<PedidoCompra>().HasQueryFilter(
            p => !p.IsDeleted && p.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Orcamento>().HasQueryFilter(
            o => !o.IsDeleted && o.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Category>().HasQueryFilter(
            c => !c.IsDeleted && c.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Brand>().HasQueryFilter(
            b => !b.IsDeleted && b.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Supplier>().HasQueryFilter(
            s => !s.IsDeleted && s.TenantId == tenantFilter.Value);
        modelBuilder.Entity<ProdutoAgregado>().HasQueryFilter(
            pa => !pa.IsDeleted && pa.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Entrega>().HasQueryFilter(
            e => !e.IsDeleted && e.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Veiculo>().HasQueryFilter(
            v => !v.IsDeleted && v.TenantId == tenantFilter.Value);

        // ── Tintométrico ──────────────────────────────────────────────────────
        modelBuilder.Entity<FormulaTintometrica>().HasQueryFilter(
            f => !f.IsDeleted && f.TenantId == tenantFilter.Value);
        modelBuilder.Entity<FormulaTintometrica>(e =>
        {
            e.HasOne(f => f.Product)
                .WithMany()
                .HasForeignKey(f => f.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Entidades sem IsDeleted — só TenantId ─────────────────────
        modelBuilder.Entity<User>().HasQueryFilter(
            u => u.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Role>().HasQueryFilter(
            r => r.TenantId == tenantFilter.Value);
        modelBuilder.Entity<Branch>().HasQueryFilter(
            b => b.TenantId == tenantFilter.Value);
        modelBuilder.Entity<ProductBranchStock>().HasQueryFilter(
            s => s.TenantId == tenantFilter.Value);
        modelBuilder.Entity<TransferenciaEstoque>().HasQueryFilter(
            t => t.TenantId == tenantFilter.Value);
        modelBuilder.Entity<ContaReceber>().HasQueryFilter(
            c => c.TenantId == tenantFilter.Value);
        modelBuilder.Entity<ContaPagar>().HasQueryFilter(
            c => c.TenantId == tenantFilter.Value);

        // ── Fidelidade e Haver — agora com isolamento de tenant ───────
        // PontosFidelidade herda BaseEntity (tem TenantId).
        // MovimentoHaver deve ter TenantId para filtrar corretamente.
        modelBuilder.Entity<PontosFidelidade>().HasQueryFilter(
            p => p.TenantId == tenantFilter.Value);
        modelBuilder.Entity<MovimentoHaver>().HasQueryFilter(
            m => m.TenantId == tenantFilter.Value);

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

        // ══════════════════════════════════════════════════════════════
        //  Módulo Integrações (Marketplace) — SalesChannel/ExternalOrder
        //
        //  Todas as 10 entidades herdam BaseEntity (TenantId + IsDeleted),
        //  então todas recebem HasQueryFilter — defesa em profundidade,
        //  mesmo princípio já aplicado em MovimentoContaBancaria/VendaSuspensaItem:
        //  não confiar só no filtro do pai pra isolar tenant num filho.
        // ══════════════════════════════════════════════════════════════
        modelBuilder.Entity<SalesChannel>().HasQueryFilter(
            s => !s.IsDeleted && s.TenantId == tenantFilter.Value);
        modelBuilder.Entity<SalesChannelPricingPolicy>().HasQueryFilter(
            p => !p.IsDeleted && p.TenantId == tenantFilter.Value);
        modelBuilder.Entity<ExternalOrder>().HasQueryFilter(
            o => !o.IsDeleted && o.TenantId == tenantFilter.Value);
        modelBuilder.Entity<ExternalOrderItem>().HasQueryFilter(
            i => !i.IsDeleted && i.TenantId == tenantFilter.Value);
        modelBuilder.Entity<SkuMapping>().HasQueryFilter(
            m => !m.IsDeleted && m.TenantId == tenantFilter.Value);
        modelBuilder.Entity<ShadowStockReservation>().HasQueryFilter(
            r => !r.IsDeleted && r.TenantId == tenantFilter.Value);
        modelBuilder.Entity<OrderEvent>().HasQueryFilter(
            e => !e.IsDeleted && e.TenantId == tenantFilter.Value);
        modelBuilder.Entity<OrderAction>().HasQueryFilter(
            a => !a.IsDeleted && a.TenantId == tenantFilter.Value);
        modelBuilder.Entity<OrderConflict>().HasQueryFilter(
            c => !c.IsDeleted && c.TenantId == tenantFilter.Value);
        modelBuilder.Entity<ProcessingSession>().HasQueryFilter(
            p => !p.IsDeleted && p.TenantId == tenantFilter.Value);

        // ── Precisão decimal explícita (mesmo motivo do S17 FIX acima) ──
        modelBuilder.Entity<SalesChannelPricingPolicy>().Property(p => p.PercentualAjuste).HasPrecision(5, 2);
        modelBuilder.Entity<ExternalOrder>().Property(o => o.ValorTotal).HasPrecision(18, 2);
        modelBuilder.Entity<ExternalOrderItem>().Property(i => i.Quantidade).HasPrecision(18, 4);
        modelBuilder.Entity<ExternalOrderItem>().Property(i => i.ValorUnitario).HasPrecision(18, 4);
        modelBuilder.Entity<SkuMapping>().Property(m => m.BufferSeguranca).HasPrecision(18, 4);
        modelBuilder.Entity<ShadowStockReservation>().Property(r => r.Quantidade).HasPrecision(18, 4);

        // ── SalesChannel: filhos diretos ────────────────────────────────
        // Restrict, não Cascade — apagar um canal não deve arrastar o histórico de
        // pedidos/mapeamentos junto (o soft-delete via IsDeleted é o caminho normal;
        // isso só protege contra um hard-delete acidental em manutenção/teste).
        modelBuilder.Entity<SalesChannelPricingPolicy>()
            .HasOne(p => p.SalesChannel)
            .WithMany(s => s.PoliticasDePreco)
            .HasForeignKey(p => p.SalesChannelId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesChannel>()
            .HasOne(s => s.ClienteRepasse)
            .WithMany()
            .HasForeignKey(s => s.ClienteRepasseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SkuMapping>(e =>
        {
            e.HasOne(m => m.SalesChannel)
                .WithMany(s => s.MapeamentosSku)
                .HasForeignKey(m => m.SalesChannelId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(m => m.Product)
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Um SKU externo só pode apontar pra um Product por canal.
            // Índice FILTRADO — sem isso, um SkuMapping soft-deleted continua
            // ocupando a chave única e bloqueia reinserção do mesmo (canal, SKU).
            e.HasIndex(m => new { m.SalesChannelId, m.SkuExterno })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<ExternalOrder>(e =>
        {
            e.HasOne(o => o.SalesChannel)
                .WithMany()
                .HasForeignKey(o => o.SalesChannelId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(o => o.Venda)
                .WithMany()
                .HasForeignKey(o => o.VendaId)
                .OnDelete(DeleteBehavior.Restrict);

            // Não processar o mesmo pedido do canal duas vezes. Filtrado pelo mesmo
            // motivo do SkuMapping acima — um pedido soft-deleted não pode travar
            // a chave se o mesmo ExternalOrderId for reprocessado depois.
            e.HasIndex(o => new { o.SalesChannelId, o.ExternalOrderId })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0");

            // CorrelationId é a chave de rastreio ("um SELECT, vê a vida inteira do
            // pedido") — sem índice, essa consulta faz table scan em produção.
            e.HasIndex(o => o.CorrelationId);
        });

        // ── ExternalOrder: filhos que não fazem sentido sem o pai ───────
        // Cascade aqui — Item/Event/Action/Conflict/Reservation não têm valor
        // sozinhos se o ExternalOrder em si for removido.
        modelBuilder.Entity<ExternalOrderItem>()
            .HasOne(i => i.ExternalOrder)
            .WithMany(o => o.Itens)
            .HasForeignKey(i => i.ExternalOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExternalOrderItem>()
            .HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<OrderEvent>()
            .HasOne(e => e.ExternalOrder)
            .WithMany()
            .HasForeignKey(e => e.ExternalOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderAction>()
            .HasOne(a => a.ExternalOrder)
            .WithMany()
            .HasForeignKey(a => a.ExternalOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderConflict>()
            .HasOne(c => c.ExternalOrder)
            .WithMany()
            .HasForeignKey(c => c.ExternalOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ShadowStockReservation>(e =>
        {
            e.HasOne(r => r.ExternalOrder)
                .WithMany()
                .HasForeignKey(r => r.ExternalOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(r => r.Product)
                .WithMany()
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Consulta mais comum do módulo: "quanto está reservado agora, por produto".
            e.HasIndex(r => new { r.ProductId, r.Status });
        });

        modelBuilder.Entity<OrderEvent>().HasIndex(e => e.CorrelationId);
        modelBuilder.Entity<OrderAction>().HasIndex(a => a.CorrelationId);
        modelBuilder.Entity<OrderConflict>().HasIndex(c => c.CorrelationId);

        // ── ProcessingSession: opcional por canal, então SetNull ────────
        modelBuilder.Entity<ProcessingSession>()
            .HasOne(p => p.SalesChannel)
            .WithMany()
            .HasForeignKey(p => p.SalesChannelId)
            .OnDelete(DeleteBehavior.SetNull);

        // Dashboards futuros vão consultar sessões por canal + período.
        modelBuilder.Entity<ProcessingSession>()
            .HasIndex(p => new { p.SalesChannelId, p.IniciadoEm });
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
            var userId    = _requestTenant?.UserId   ?? _currentUserId;
            var userName  = _requestTenant?.UserName ?? _currentUserName;

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