// ERP.Tests/RelatorioServicesTests.cs
// ─────────────────────────────────────────────────────────────────────────────
// Testes unitários para os serviços de relatório e os cache decorators.
//
// Stack: xUnit + Moq + Microsoft.EntityFrameworkCore.InMemory
//
// Adicionar ao ERP.Tests.csproj:
//   <PackageReference Include="xunit"                    Version="2.9.0" />
//   <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
//   <PackageReference Include="Moq"                      Version="4.20.72" />
//   <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
//   <PackageReference Include="Microsoft.Extensions.Caching.Memory"   Version="8.0.0" />
// ─────────────────────────────────────────────────────────────────────────────
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

namespace ERP.Tests;

// ═══════════════════════════════════════════════════════════════════════════════
//  HELPER — cria um IServiceProvider com InMemory DB + TenantId configurado
// ═══════════════════════════════════════════════════════════════════════════════
// FakeRequestTenant: substitui o static _globalTenantId para evitar race condition em testes paralelos
internal class FakeRequestTenant : ERP.Application.Interfaces.IRequestTenant
{
    public Guid   TenantId { get; set; }
    public Guid   UserId   { get; set; }
    public string UserName { get; set; } = "test";
}

internal static class TestDb
{
    /// <param name="tenantId">
    /// Quando fornecido, usa ESTE tenantId (para testes que semeiam com TenantId explícito).
    /// Quando nulo, gera um novo GUID. O IRequestTenant registrado usa o mesmo valor,
    /// garantindo que HasQueryFilter e dados semeados estejam no mesmo tenant.
    /// </param>
    public static IServiceProvider Create(
        string dbName,
        Action<AppDbContext>? seed = null,
        Guid? tenantId = null)
    {
        var tid    = tenantId ?? Guid.NewGuid();
        var tenant = new FakeRequestTenant { TenantId = tid };

        // Mantém SetGlobalTenantId para compatibilidade com construtor de 1 argumento (WPF)
        AppDbContext.SetGlobalTenantId(tid);

        // Constrói as opções do banco InMemory
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var services = new ServiceCollection();
        services.AddSingleton<ERP.Application.Interfaces.IRequestTenant>(tenant);
        // Factory explícita: garante que o construtor de 2 args é sempre usado,
        // independente de como o EF Core resolve AddDbContext.
        // Isso elimina a race condition do _globalTenantId estático.
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
        // Arrange
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);

        var sp = TestDb.Create("dre_receita", ctx =>
        {
            var produto = new Product { Id = Guid.NewGuid(), Name = "Prod A", SalePrice = 100, CostPrice = 60, TenantId = tid };
            ctx.Products.Add(produto);

            var venda = new Sale
            {
                Id         = Guid.NewGuid(),
                SaleNumber = "V001",
                Total      = 200m,
                Subtotal   = 200m,
                SaleDate   = DateTime.Today,
                Status     = SaleStatus.SemNota,
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
        });

        var service = new DreService(sp);

        // Act
        var result = await service.CalcularAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        // Assert
        Assert.Equal(200m, result.ReceitaBruta);
        Assert.Equal(120m, result.CustoMercadorias); // 2 × 60
        Assert.Equal(80m,  result.LucroBruto);
    }

    [Fact]
    public async Task CalcularAsync_SemVendas_RetornaZeros()
    {
        var sp      = TestDb.Create("dre_zeros");
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

        var sp = TestDb.Create("dre_canceladas", ctx =>
        {
            ctx.Sales.Add(new Sale
            {
                Id         = Guid.NewGuid(),
                SaleNumber = "V002",
                Total      = 500m,
                Subtotal   = 500m,
                SaleDate   = DateTime.Today,
                Status     = SaleStatus.Cancelada,
                TenantId   = tid,
            });
        });

        var result = await new DreService(sp).CalcularAsync(DateTime.Today, DateTime.Today);

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

        var sp = TestDb.Create("abc_ordem", ctx =>
        {
            var venda = new Sale
            {
                Id = Guid.NewGuid(), SaleNumber = "V001", Total = 300, Subtotal = 300,
                SaleDate = DateTime.Today, Status = SaleStatus.SemNota, TenantId = tid,
            };
            venda.Items.Add(new SaleItem { Id = Guid.NewGuid(), ProductName = "Produto B", Quantity = 1, UnitPrice = 100 });
            venda.Items.Add(new SaleItem { Id = Guid.NewGuid(), ProductName = "Produto A", Quantity = 1, UnitPrice = 200 });
            ctx.Sales.Add(venda);
        });

        var itens = await new AbcService(sp).CalcularAsync(DateTime.Today, DateTime.Today);

        Assert.Equal("Produto A", itens[0].Nome);  // maior faturamento primeiro
        Assert.Equal("Produto B", itens[1].Nome);
    }

    [Fact]
    public async Task CalcularAsync_SemVendas_ListaVazia()
    {
        var sp    = TestDb.Create("abc_vazio");
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

        var sp = TestDb.Create("comissao_calc", ctx =>
        {
            ctx.Sales.Add(new Sale
            {
                Id = Guid.NewGuid(), SaleNumber = "V001", Total = 1000m, Subtotal = 1000m,
                SellerName = "João", SaleDate = DateTime.Today, Status = SaleStatus.SemNota, TenantId = tid,
            });
        });

        var result = await new ComissaoService(sp).CalcularAsync(DateTime.Today, DateTime.Today, 3.5m);

        Assert.Single(result.Vendedores);
        Assert.Equal(35m, result.Vendedores[0].ValorComissao);  // 1000 × 3,5%
        Assert.Equal(35m, result.TotalComissaoPagar);
    }

    [Fact]
    public async Task CalcularAsync_VendedorNulo_SubstituiPorPadrao()
    {
        var tid = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);

        var sp = TestDb.Create("comissao_null_vendor", ctx =>
        {
            ctx.Sales.Add(new Sale
            {
                Id = Guid.NewGuid(), SaleNumber = "V001", Total = 100m, Subtotal = 100m,
                SellerName = null!, SaleDate = DateTime.Today, Status = SaleStatus.SemNota, TenantId = tid,
            });
        });

        var result = await new ComissaoService(sp).CalcularAsync(DateTime.Today, DateTime.Today, 5m);

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
        var customerId = Guid.NewGuid();

        var sp = TestDb.Create("haver_saldo", ctx =>
        {
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente X", HaverBalance = 250m, TenantId = tid });
        });

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
        var customerId = Guid.NewGuid();

        var sp = TestDb.Create("haver_insuficiente", ctx =>
        {
            ctx.Customers.Add(new Customer { Id = customerId, Name = "Cliente Y", HaverBalance = 10m, TenantId = tid });
        });

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
        var sp      = TestDb.Create("haver_naoexiste");
        using var scope3 = sp.CreateScope();
        var emptyTenant = new Mock<IRequestTenant>();
        emptyTenant.Setup(t => t.TenantId).Returns(Guid.NewGuid());
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

        var sp = TestDb.Create("inv_lista", ctx =>
        {
            ctx.Products.AddRange(
                new Product { Id = Guid.NewGuid(), Name = "A", SalePrice = 10, TenantId = tid },
                new Product { Id = Guid.NewGuid(), Name = "B", SalePrice = 20, TenantId = tid });
        });

        var produtos = await new InventarioService(sp).ObterProdutosAsync();

        Assert.Equal(2, produtos.Count);
    }

    [Fact]
    public async Task AplicarAjustesAsync_AtualizaEstoqueCorretamente()
    {
        var tid       = Guid.NewGuid();
        AppDbContext.SetGlobalTenantId(tid);
        var produtoId = Guid.NewGuid();

        var sp = TestDb.Create("inv_ajuste", ctx =>
        {
            ctx.Products.Add(new Product { Id = produtoId, Name = "Produto Teste", Stock = 10, SalePrice = 5, TenantId = tid });
        });

        var service = new InventarioService(sp);
        await service.AplicarAjustesAsync(new[] { (produtoId, 7m) });

        // Verifica diretamente no banco
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
        await cached.CalcularAsync(d, d); // segunda chamada — deve usar cache

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
        await cached.CalcularAsync(DateTime.Today.AddDays(-7), DateTime.Today); // chave diferente

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

        await cached.ObterProdutosAsync();                    // popula cache
        await cached.AplicarAjustesAsync(Array.Empty<(Guid, decimal)>()); // invalida
        await cached.ObterProdutosAsync();                    // deve chamar inner novamente

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

// ═══════════════════════════════════════════════════════════════════════════════
//  PEDIDO COMPRA SERVICE (Application layer)
// ═══════════════════════════════════════════════════════════════════════════════
public class PedidoCompraServiceTests
{
    // Helper simples — mocks do UoW
    private static (ERP.Application.Services.PedidoCompraService svc, Mock<ERP.Domain.Interfaces.IUnitOfWork> uowMock) BuildSvc()
    {
        var uowMock          = new Mock<ERP.Domain.Interfaces.IUnitOfWork>();
        var repoMock         = new Mock<ERP.Domain.Interfaces.IPedidoCompraRepository>();
        var productRepoMock  = new Mock<ERP.Domain.Interfaces.IProductRepository>();

        repoMock.Setup(r => r.GerarProximoNumeroAsync()).ReturnsAsync("PC-2026-001");
        repoMock.Setup(r => r.AddAsync(It.IsAny<PedidoCompra>())).Returns(Task.CompletedTask);
        uowMock.Setup(u => u.PedidosCompra).Returns(repoMock.Object);
        uowMock.Setup(u => u.Products).Returns(productRepoMock.Object);
        uowMock.Setup(u => u.CommitAsync()).ReturnsAsync(1);

        return (new ERP.Application.Services.PedidoCompraService(uowMock.Object), uowMock);
    }

    [Fact]
    public async Task CriarAsync_GeraNumeroEPersiste()
    {
        var (svc, uowMock) = BuildSvc();

        var dto = new CreatePedidoCompraDto
        {
            SupplierId     = Guid.NewGuid(),
            FornecedorNome = "Fornecedor Teste",
            CriadoPor      = "Tester",
            Itens          = new List<CreatePedidoCompraItemDto>
            {
                new() { ProductId = Guid.NewGuid(), ProductName = "Item A", Quantidade = 5, PrecoUnitario = 10 }
            }
        };

        var result = await svc.CriarAsync(dto);

        Assert.Equal("PC-2026-001", result.Numero);
        Assert.Single(result.Itens);
        uowMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task DeletarAsync_PedidoNaoEncontrado_LancaKeyNotFoundException()
    {
        var (svc, _) = BuildSvc();
        // O GetByIdAsync mockado retorna null por padrão

        var uow     = new Mock<ERP.Domain.Interfaces.IUnitOfWork>();
        var repo    = new Mock<ERP.Domain.Interfaces.IPedidoCompraRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((PedidoCompra?)null);
        uow.Setup(u => u.PedidosCompra).Returns(repo.Object);

        var svc2 = new ERP.Application.Services.PedidoCompraService(uow.Object);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc2.DeletarAsync(Guid.NewGuid()));
    }
}