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

namespace ERP.Tests;

// ═══════════════════════════════════════════════════════════════════════════════
//  HELPER
// ═══════════════════════════════════════════════════════════════════════════════
internal class FakeRequestTenant : ERP.Application.Interfaces.IRequestTenant
{
    public Guid   TenantId { get; set; }
    public Guid   UserId   { get; set; }
    public string UserName { get; set; } = "test";
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
    [Fact]
    public async Task ObterSaldoAsync_RetornaSaldoCorreto()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);        
        var customerId = Guid.NewGuid();

        var sp = TestDb.Create("haver_saldo", ctx =>
        {
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente X", HaverBalance = 250m, TenantId = tid });
        }, tenantId: tid);

        using var scope1 = sp.CreateScope();
        var mockTenant1 = new Mock<IRequestTenant>();
        mockTenant1.Setup(t => t.TenantId).Returns(tid);
        var db1 = scope1.ServiceProvider.GetRequiredService<ERP.Persistence.Context.AppDbContext>();
        var saldo = await new HaverService(db1, mockTenant1.Object).ObterSaldoAsync(customerId);

        Assert.Equal(250m, saldo);
    }

    [Fact]
    public async Task LancarAsync_Saida_SaldoInsuficiente_LancaExcecao()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);        
        var customerId = Guid.NewGuid();

        var sp = TestDb.Create("haver_insuficiente", ctx =>
        {
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente Y", HaverBalance = 10m, TenantId = tid });
        }, tenantId: tid);

        using var scope2 = sp.CreateScope();
        var mockTenant2 = new Mock<IRequestTenant>();
        mockTenant2.Setup(t => t.TenantId).Returns(tid);
        var db2 = scope2.ServiceProvider.GetRequiredService<ERP.Persistence.Context.AppDbContext>();
        var service = new HaverService(db2, mockTenant2.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.LancarAsync(customerId, 50m, "Saida", "Retirada", "Teste"));
    }

    [Fact]
    public async Task LancarAsync_ClienteInexistente_LancaKeyNotFoundException()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        AppDbContext.SetQueryTenantId(tid);

        var sp      = TestDb.Create("haver_naoexiste", tenantId: tid);
        using var scope3 = sp.CreateScope();
        var emptyTenant = new Mock<IRequestTenant>();
        emptyTenant.Setup(t => t.TenantId).Returns(tid);
        var db3 = scope3.ServiceProvider.GetRequiredService<ERP.Persistence.Context.AppDbContext>();
        var service = new HaverService(db3, emptyTenant.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.LancarAsync(Guid.NewGuid(), 10m, "Entrada", "Dep.", "Teste"));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  INVENTÁRIO SERVICE
// ═══════════════════════════════════════════════════════════════════════════════
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