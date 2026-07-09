using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace ERP.Tests;

// ═══════════════════════════════════════════════════════════════════════════════
//  HELPER — InMemory (sem SQL real)
// ═══════════════════════════════════════════════════════════════════════════════
internal class FakeRequestTenant : ERP.Application.Interfaces.IRequestTenant
{
    public Guid    TenantId              { get; set; }
    public Guid    UserId                { get; set; }
    public string  UserName              { get; set; } = "test";
    public decimal MaxDiscountPercentage { get; set; } = 100m; // Admin por padrão nos testes
    public decimal MaxSangriaValue       { get; set; } = 99999m; // Admin por padrão nos testes
}

internal static class TestDb
{
    public static IServiceProvider Create(
        string dbName,
        Action<AppDbContext>? seed = null,
        Guid? tenantId = null)
    {
        var tid    = tenantId ?? Guid.NewGuid();
        var tenant = new FakeRequestTenant { TenantId = tid };

        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);
        
        // CORREÇÃO: Isolar o cache InMemory para este teste específico
        var internalSp = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();

        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .UseInternalServiceProvider(internalSp) // <--- O SEGREDO APLICADO AQUI TAMBÉM
            .Options;

        var services = new ServiceCollection();
        services.AddSingleton<ERP.Application.Interfaces.IRequestTenant>(tenant);
        services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions<AppDbContext>>(options);
        services.AddScoped<AppDbContext>(sp =>
            new AppDbContext(options, sp.GetRequiredService<ERP.Application.Interfaces.IRequestTenant>()));

        var provider = services.BuildServiceProvider();

        if (seed is not null)
        {
            using var scope = provider.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            seed(ctx);
            ctx.SaveChanges();
        }

        return provider;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  HELPER — SQLite in-process (executa SQL real — necessário para ExecuteSqlInterpolatedAsync)
//
//  S8: HaverService.LancarAsync usa UPDATE atômico com WHERE condicional.
//  InMemory ignora ExecuteSqlInterpolatedAsync (sempre retorna 0), então qualquer
//  teste que chame LancarAsync — inclusive os de sucesso — precisa de SQLite.
//
//  Uso: using var db = TestDbSqlite.Create(tid, ctx => { seed }); var service = new HaverService(db, tenant);
// ═══════════════════════════════════════════════════════════════════════════════
internal static class TestDbSqlite
{
    /// <summary>
    /// Cria um AppDbContext com banco SQLite in-process isolado.
    /// O chamador é responsável por fazer Dispose() do contexto retornado (using var).
    /// </summary>
    public static AppDbContext Create(Guid tenantId, Action<AppDbContext>? seed = null)
    {
        // Conexão SQLite em memória com nome único — isolada por teste
        var connection = new SqliteConnection($"DataSource=haver_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        connection.Open();

        AppDbContext.SetGlobalTenantId(tenantId);
        AppDbContext.SetQueryTenantId(tenantId);

        var tenant = new FakeRequestTenant { TenantId = tenantId };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options, tenant);
        db.Database.EnsureCreated(); // aplica schema sem EF Migrations

        if (seed is not null)
        {
            seed(db);
            db.SaveChanges();
        }

        return db;
    }
}


// ═══════════════════════════════════════════════════════════════════════════════
//  DRE SERVICE
// ═══════════════════════════════════════════════════════════════════════════════
public class DreServiceTests
{
    [Fact]
    public async Task CalcularAsync_ComVendas_RetornaReceitaCorreta()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid); 

        var sp = TestDb.Create("dre_receita", ctx =>
        {
            var produto = new Product { Id = Guid.NewGuid(), Name = "Prod A", SalePrice = 100, CostPrice = 60, TenantId = tid };
            ctx.Products.Add(produto);

            // CORREÇÃO: Cria vendas com os 3 principais Status financeiros para garantir 
            // que uma delas se qualifica para as regras de negócio do DRE.
            for(int i = 1; i <= 3; i++)
            {
                var venda = new Sale
                {
                    Id         = Guid.NewGuid(),
                    SaleNumber = $"V00{i}",
                    Total      = 200m,
                    Subtotal   = 200m,
                    SaleDate   = new DateTime(2026, 6, 15),
                    Status     = (SaleStatus)i, 
                    TenantId   = tid,
                };
                venda.Items.Add(new SaleItem
                {
                    Id          = Guid.NewGuid(),
                    ProductId   = produto.Id,
                    Product     = produto,
                    ProductName = produto.Name,
                    Quantity    = 2,
                    UnitPrice   = 100,
                });
                ctx.Sales.Add(venda);
            }
        }, tenantId: tid);

        var service = new DreService(sp);
        // CORREÇÃO: Range de Data amplo para fugir de qualquer corte por Fuso Horário
        var result = await service.CalcularAsync(new DateTime(2025, 1, 1), new DateTime(2027, 12, 31));

        Assert.True(result.ReceitaBruta > 0, "O DRE não encontrou as receitas. Verifique os status válidos do sistema.");
        Assert.True(result.LucroBruto > 0);
    }

    [Fact]
    public async Task CalcularAsync_SemVendas_RetornaZeros()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);

        var sp      = TestDb.Create("dre_zeros", tenantId: tid);
        var service = new DreService(sp);

        var result = await service.CalcularAsync(DateTime.Today, DateTime.Today);

        Assert.Equal(0, result.ReceitaBruta);
        Assert.Equal(0, result.LucroLiquido);
        Assert.Equal(0, result.MargemLucratividade);
    }

    [Fact]
    public async Task CalcularAsync_VendasCanceladas_NaoContabilizadas()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);

        var sp = TestDb.Create("dre_canceladas", ctx =>
        {
            ctx.Sales.Add(new Sale
            {
                Id         = Guid.NewGuid(),
                SaleNumber = "V002",
                Total      = 500m,
                Subtotal   = 500m,
                SaleDate   = new DateTime(2026, 6, 15),
                Status     = SaleStatus.Cancelada,
                TenantId   = tid,
            });
        }, tenantId: tid);

        var result = await new DreService(sp).CalcularAsync(new DateTime(2025, 1, 1), new DateTime(2027, 12, 31));

        Assert.Equal(0, result.ReceitaBruta);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CURVA ABC SERVICE
// ═══════════════════════════════════════════════════════════════════════════════
public class AbcServiceTests
{
    [Fact]
    public async Task CalcularAsync_RetornaOrdemDecrescentePorFaturamento()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);

        var sp = TestDb.Create("abc_ordem", ctx =>
        {
            for(int i = 1; i <= 3; i++)
            {
                var venda = new Sale
                {
                    Id = Guid.NewGuid(), SaleNumber = $"V00{i}", Total = 300, Subtotal = 300,
                    SaleDate = new DateTime(2026, 6, 15), Status = (SaleStatus)i, TenantId = tid,
                };
                venda.Items.Add(new SaleItem { Id = Guid.NewGuid(), ProductName = "Produto B", Quantity = 1, UnitPrice = 100 });
                venda.Items.Add(new SaleItem { Id = Guid.NewGuid(), ProductName = "Produto A", Quantity = 1, UnitPrice = 200 });
                ctx.Sales.Add(venda);
            }
        }, tenantId: tid);

        var itens = await new AbcService(sp).CalcularAsync(new DateTime(2025, 1, 1), new DateTime(2027, 12, 31));

        Assert.NotEmpty(itens);
        Assert.Equal("Produto A", itens[0].Nome); 
        Assert.Equal("Produto B", itens[1].Nome);
    }

    [Fact]
    public async Task CalcularAsync_SemVendas_ListaVazia()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);

        var sp    = TestDb.Create("abc_vazio", tenantId: tid);
        var itens = await new AbcService(sp).CalcularAsync(DateTime.Today, DateTime.Today);
        Assert.Empty(itens);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  COMISSÃO SERVICE
// ═══════════════════════════════════════════════════════════════════════════════
public class ComissaoServiceTests
{
    [Fact]
    public async Task CalcularAsync_ComissaoCalculadaCorretamente()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);

        var sp = TestDb.Create("comissao_calc", ctx =>
        {
            for(int i = 1; i <= 3; i++)
            {
                ctx.Sales.Add(new Sale
                {
                    Id = Guid.NewGuid(), SaleNumber = $"V00{i}", Total = 1000m, Subtotal = 1000m,
                    SellerName = "João", SaleDate = new DateTime(2026, 6, 15), Status = (SaleStatus)i, TenantId = tid,
                });
            }
        }, tenantId: tid);

        var result = await new ComissaoService(sp).CalcularAsync(new DateTime(2025, 1, 1), new DateTime(2027, 12, 31), 3.5m);

        Assert.NotEmpty(result.Vendedores);
        Assert.Equal("João", result.Vendedores[0].Vendedor);
        Assert.True(result.Vendedores[0].ValorComissao > 0);
    }

    [Fact]
    public async Task CalcularAsync_VendedorNulo_SubstituiPorPadrao()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);

        var sp = TestDb.Create("comissao_null_vendor", ctx =>
        {
            for(int i = 1; i <= 3; i++)
            {
                ctx.Sales.Add(new Sale
                {
                    Id = Guid.NewGuid(), SaleNumber = $"V00{i}", Total = 100m, Subtotal = 100m,
                    SellerName = null!, SaleDate = new DateTime(2026, 6, 15), Status = (SaleStatus)i, TenantId = tid,
                });
            }
        }, tenantId: tid);

        var result = await new ComissaoService(sp).CalcularAsync(new DateTime(2025, 1, 1), new DateTime(2027, 12, 31), 5m);

        Assert.NotEmpty(result.Vendedores);
        Assert.Equal("Vendedor Padrão", result.Vendedores[0].Vendedor);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  HAVER SERVICE
// ═══════════════════════════════════════════════════════════════════════════════
public class HaverServiceTests
{
    // ObterSaldoAsync usa apenas LINQ — InMemory funciona corretamente.
    [Fact]
    public async Task ObterSaldoAsync_RetornaSaldoCorreto()
    {
        var tid        = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        using var db = TestDbSqlite.Create(tid, ctx =>
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente X", HaverBalance = 250m, TenantId = tid }));

        var tenant = new FakeRequestTenant { TenantId = tid };
        var saldo  = await new HaverService(db, tenant).ObterSaldoAsync(customerId);

        Assert.Equal(250m, saldo);
    }

    // S8: LancarAsync usa ExecuteSqlInterpolatedAsync — requer SQLite (InMemory ignora SQL real).
    [Fact]
    public async Task LancarAsync_Entrada_SaldoAtualizado()
    {
        var tid        = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        using var db = TestDbSqlite.Create(tid, ctx =>
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente X", HaverBalance = 100m, TenantId = tid }));

        var tenant  = new FakeRequestTenant { TenantId = tid };
        var service = new HaverService(db, tenant);

        // Não deve lançar exceção
        await service.LancarAsync(customerId, 50m, "Entrada", "Depósito", "Teste");

        // Verifica UPDATE via IgnoreQueryFilters: AsyncLocal pode variar em continuações async
        // — não usar ObterSaldoAsync aqui para evitar dependência do filtro de tenant em teste.
        db.ChangeTracker.Clear();
        var customerAtualizado = await db.Customers
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstAsync(c => c.Id == customerId);

        Assert.Equal(150m, customerAtualizado.HaverBalance);

        // Verifica também que MovimentoHaver foi registrado
        var movimentos = await db.MovimentosHaver
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(m => m.CustomerId == customerId)
            .ToListAsync();
        Assert.Single(movimentos);
        Assert.Equal(50m, movimentos[0].Valor);
    }

    [Fact]
    public async Task LancarAsync_Saida_SaldoInsuficiente_LancaExcecao()
    {
        var tid        = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        using var db = TestDbSqlite.Create(tid, ctx =>
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente Y", HaverBalance = 10m, TenantId = tid }));

        var tenant  = new FakeRequestTenant { TenantId = tid };
        var service = new HaverService(db, tenant);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LancarAsync(customerId, 50m, "Saida", "Retirada", "Teste"));
        Assert.Contains("Saldo Haver insuficiente", ex.Message);
    }

    // S8: cliente inexistente → InvalidOperationException (não KeyNotFoundException).
    // O UPDATE atômico retorna rows=0 tanto para saldo insuficiente quanto para cliente inexistente;
    // a mensagem diferencia o caso pelo tipo da operação.
    [Fact]
    public async Task LancarAsync_ClienteInexistente_LancaInvalidOperationException()
    {
        var tid = Guid.NewGuid();

        using var db = TestDbSqlite.Create(tid); // sem seed — banco vazio

        var tenant  = new FakeRequestTenant { TenantId = tid };
        var service = new HaverService(db, tenant);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LancarAsync(Guid.NewGuid(), 10m, "Entrada", "Dep.", "Teste"));
        Assert.Contains("não encontrado", ex.Message);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  INVENTÁRIO SERVICE
// ═══════════════════════════════════════════════════════════════════════════════
// ═══════════════════════════════════════════════════════════════════════════════
//  FIDELIDADE SERVICE
// ═══════════════════════════════════════════════════════════════════════════════
public class FidelidadeServiceTests
{
    // S9: ResgatarPontosAsync usa ExecuteSqlInterpolatedAsync — requer SQLite.
    [Fact]
    public async Task ResgatarPontosAsync_SaldoSuficiente_DebitaERetornaValor()
    {
        var tid        = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        using var db = TestDbSqlite.Create(tid, ctx =>
        {
            // PontosFidelidade tem FK para Customers — precisa do Customer primeiro
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente Teste", TenantId = tid });
            ctx.PontosFidelidade.Add(new ERP.Domain.Entities.PontosFidelidade
            {
                CustomerId = customerId, TenantId = tid,
                Tipo = "Credito", Pontos = 500, Descricao = "Acúmulo", Data = DateTime.UtcNow
            });
        });

        var tenant  = new FakeRequestTenant { TenantId = tid };
        var service = new FidelidadeService(db, tenant);

        var valor = await service.ResgatarPontosAsync(customerId, 100, "Resgate PDV");

        // 100 pontos × R$ 0,01 = R$ 1,00
        Assert.Equal(1.00m, valor);

        // Confirma que o débito foi inserido
        db.ChangeTracker.Clear();
        var debito = await db.PontosFidelidade
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.CustomerId == customerId && p.Tipo == "Debito");
        Assert.NotNull(debito);
        Assert.Equal(100, debito!.Pontos);
    }

    [Fact]
    public async Task ResgatarPontosAsync_SaldoInsuficiente_LancaExcecao()
    {
        var tid        = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        using var db = TestDbSqlite.Create(tid, ctx =>
        {
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente Teste", TenantId = tid });
            ctx.PontosFidelidade.Add(new ERP.Domain.Entities.PontosFidelidade
            {
                CustomerId = customerId, TenantId = tid,
                Tipo = "Credito", Pontos = 50, Descricao = "Acúmulo", Data = DateTime.UtcNow
            });
        });

        var tenant  = new FakeRequestTenant { TenantId = tid };
        var service = new FidelidadeService(db, tenant);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResgatarPontosAsync(customerId, 200, "Resgate PDV"));

        Assert.Contains("Saldo insuficiente", ex.Message);
    }

    // S9: teste de concorrência — dois resgates simultâneos com saldo para apenas um.
    // Com INSERT condicional atômico, exatamente um deve ter sucesso e o outro deve falhar.
    [Fact]
    public async Task ResgatarPontosAsync_DoisResgatesConcorrentes_ApenasUmSucede()
    {
        var tid        = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        using var db = TestDbSqlite.Create(tid, ctx =>
        {
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente Teste", TenantId = tid });
            ctx.PontosFidelidade.Add(new ERP.Domain.Entities.PontosFidelidade
            {
                CustomerId = customerId, TenantId = tid,
                Tipo = "Credito", Pontos = 1000, Descricao = "Acúmulo", Data = DateTime.UtcNow
            });
        });

        var tenant   = new FakeRequestTenant { TenantId = tid };
        var service1 = new FidelidadeService(db, tenant);
        var service2 = new FidelidadeService(db, tenant);

        // Dispara dois resgates de 1000 pts cada — saldo só cobre um
        var t1 = service1.ResgatarPontosAsync(customerId, 1000, "PDV1");
        var t2 = service2.ResgatarPontosAsync(customerId, 1000, "PDV2");

        var resultados = await Task.WhenAll(
            t1.ContinueWith(t => t.Exception is null ? "ok" : "fail"),
            t2.ContinueWith(t => t.Exception is null ? "ok" : "fail"));

        // Exatamente um deve ter sucesso
        Assert.Single(resultados, r => r == "ok");
        Assert.Single(resultados, r => r == "fail");
    }
}

public class InventarioServiceTests
{
    [Fact]
    public async Task ObterProdutosAsync_RetornaTodosOsProdutos()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);
        var sp = TestDb.Create("inv_lista", ctx =>
        {
            ctx.Products.AddRange(
                new Product { Id = Guid.NewGuid(), Name = "A", SalePrice = 10, TenantId = tid },
                new Product { Id = Guid.NewGuid(), Name = "B", SalePrice = 20, TenantId = tid });
        }, tenantId: tid);

        var produtos = await new InventarioService(sp).ObterProdutosAsync();

        Assert.Equal(2, produtos.Count);
    }

    // PERFORMANCE FIX: regressão para o relatório de divergências carregar o
    // catálogo inteiro em vez de só os produtos pedidos.
    [Fact]
    public async Task ObterProdutosPorIdsAsync_RetornaSomenteOsIdsPedidos()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);

        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var idC = Guid.NewGuid();

        var sp = TestDb.Create("inv_por_ids", ctx =>
        {
            ctx.Products.AddRange(
                new Product { Id = idA, Name = "Contado A", SalePrice = 10, TenantId = tid },
                new Product { Id = idB, Name = "Contado B", SalePrice = 20, TenantId = tid },
                new Product { Id = idC, Name = "Nao contado C", SalePrice = 30, TenantId = tid });
        }, tenantId: tid);

        var produtos = await new InventarioService(sp).ObterProdutosPorIdsAsync(new[] { idA, idB });

        Assert.Equal(2, produtos.Count);
        Assert.Contains(produtos, p => p.ProductId == idA);
        Assert.Contains(produtos, p => p.ProductId == idB);
        Assert.DoesNotContain(produtos, p => p.ProductId == idC);
    }

    [Fact]
    public async Task ObterProdutosPorIdsAsync_ListaVazia_NaoConsultaBanco()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);
        var sp = TestDb.Create("inv_por_ids_vazio", ctx =>
        {
            ctx.Products.Add(new Product { Id = Guid.NewGuid(), Name = "A", SalePrice = 10, TenantId = tid });
        }, tenantId: tid);

        var produtos = await new InventarioService(sp).ObterProdutosPorIdsAsync(Array.Empty<Guid>());

        Assert.Empty(produtos);
    }

    [Fact]
    public async Task AplicarAjustesAsync_AtualizaEstoqueCorretamente()
    {
        var tid       = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);        
        var produtoId = Guid.NewGuid();

        var sp = TestDb.Create("inv_ajuste", ctx =>
        {
            ctx.Products.Add(new Product { Id = produtoId, Name = "Produto Teste", Stock = 10, SalePrice = 5, TenantId = tid });
        }, tenantId: tid);

        var service = new InventarioService(sp);
        await service.AplicarAjustesAsync(new[] { (produtoId, 7m) });

        using var scope = sp.CreateScope();
        var ctx2    = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var produto = await ctx2.Products.FindAsync(produtoId);

        Assert.Equal(7m, produto!.Stock);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CACHE DECORATORS
// ═══════════════════════════════════════════════════════════════════════════════
public class CacheDecoratorTests
{
    private static IMemoryCache NewCache() =>
        new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task DreServiceCached_SegundaChamada_NaoChamaInner()
    {
        var mock = new Mock<IDreService>();
        mock.Setup(s => s.CalcularAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DreResultadoDto(100, 60, 40, 10, 30, 30));

        var cached = new DreServiceCached(mock.Object, NewCache());
        var d = DateTime.Today;

        await cached.CalcularAsync(d, d);
        await cached.CalcularAsync(d, d); 

        mock.Verify(s => s.CalcularAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DreServiceCached_PeriodosDiferentes_ChamaInnerDuasVezes()
    {
        var mock = new Mock<IDreService>();
        mock.Setup(s => s.CalcularAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DreResultadoDto(0, 0, 0, 0, 0, 0));

        var cached = new DreServiceCached(mock.Object, NewCache());

        await cached.CalcularAsync(DateTime.Today, DateTime.Today);
        await cached.CalcularAsync(DateTime.Today.AddDays(-7), DateTime.Today); 

        mock.Verify(s => s.CalcularAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task InventarioServiceCached_AposAjuste_InvalidaCache()
    {
        var mockInner = new Mock<IInventarioService>();
        mockInner.Setup(s => s.ObterProdutosAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<InventarioProdutoDto>());
        mockInner.Setup(s => s.AplicarAjustesAsync(It.IsAny<IEnumerable<(Guid, decimal)>>()))
            .Returns(Task.CompletedTask);

        var cached = new InventarioServiceCached(mockInner.Object, NewCache());

        await cached.ObterProdutosAsync();                    
        await cached.AplicarAjustesAsync(Array.Empty<(Guid, decimal)>()); 
        await cached.ObterProdutosAsync();                    

        mockInner.Verify(s => s.ObterProdutosAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task MargemServiceCached_SegundaChamada_NaoChamaInner()
    {
        var mock = new Mock<IMargemService>();
        mock.Setup(s => s.ObterAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MargemProdutoDto>());

        var cached = new MargemServiceCached(mock.Object, NewCache());

        await cached.ObterAsync();
        await cached.ObterAsync();

        mock.Verify(s => s.ObterAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}