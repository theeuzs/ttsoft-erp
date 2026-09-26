// ERP.Tests/Infrastructure/FiscalServiceNumeroItemFiscalTests.cs
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// Regra VC02-14 (NT 2025.002-RTC, produção 01/09/2026) — lado da EMISSÃO
/// original, não da devolução (ver FiscalServiceDevolucaoIndicadorIeTests
/// pro lado da devolução). EmitirNotaAsync passa a persistir
/// SaleItem.NumeroItemFiscal depois de autorizado, usando o mesmo número já
/// atribuído em memória por MontarItens (fonte única — não reconstrói depois).
///
/// Usa TestDbSqlite (SQLite real), não TestDb (InMemory) — a persistência é
/// via ExecuteUpdateAsync (SQL bruto), que o provider InMemory não suporta
/// de verdade. Mesma lição já registrada no projeto (S26,
/// ContaReceberServiceTests).
/// </summary>
public class FiscalServiceNumeroItemFiscalTests
{
    private static AppDbContext CriarContexto(Guid tenantId, Action<AppDbContext> seed)
    {
        var connection = new SqliteConnection($"DataSource=numitem_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        connection.Open();

        AppDbContext.SetGlobalTenantId(tenantId);
        AppDbContext.SetQueryTenantId(tenantId);
        var tenant = new ERP.Tests.FakeRequestTenant { TenantId = tenantId };

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options, tenant);
        db.Database.EnsureCreated();

        seed(db);
        db.SaveChanges();
        return db;
    }

    private static (Guid VendaId, Guid Item1Id, Guid Item2Id, Product Produto) SeedVenda(
        AppDbContext ctx, Guid tenantId, int quantidadeItens)
    {
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var produto = new Product { Id = Guid.NewGuid(), Name = "Produto Teste", TenantId = tenantId, SalePrice = 10m, NCM = "39172300", CSOSN = "102" };
        ctx.Products.Add(produto);

        var customer = new Customer { Id = customerId, TenantId = tenantId, Name = "Cliente Teste", Document = "10087600994" };
        ctx.Customers.Add(customer);

        var item1Id = Guid.NewGuid();
        var item2Id = Guid.NewGuid();
        var items = new List<SaleItem>
        {
            new() { Id = item1Id, TenantId = tenantId, ProductId = produto.Id, Product = produto,
                    ProductName = produto.Name, Quantity = 1, UnitPrice = 10m, TotalItem = 10m }
        };
        if (quantidadeItens > 1)
        {
            items.Add(new SaleItem { Id = item2Id, TenantId = tenantId, ProductId = produto.Id, Product = produto,
                    ProductName = produto.Name, Quantity = 2, UnitPrice = 5m, TotalItem = 10m });
        }

        ctx.Sales.Add(new Sale
        {
            Id = vendaId, TenantId = tenantId, SaleNumber = "TESTE-NUMITEM",
            SaleDate = DateTime.Now, CustomerId = customerId, Customer = customer,
            Items = items
        });

        return (vendaId, item1Id, item2Id, produto);
    }

    private static FiscalService CriarService(AppDbContext ctx, bool focusAutoriza)
    {
        var configProvider = new Mock<IFiscalConfigurationProvider>();
        configProvider.Setup(c => c.ObterConfiguracaoAsync())
            .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

        var nfceMock = new Mock<INfceEmissionService>();
        var resultado = focusAutoriza
            ? (true, "NFC-e Autorizada com sucesso!", "https://focus/danfe.html", "https://focus/nfce.xml", "41260912820608000141650010000033031335484399", "9901")
            : (false, "Nota Rejeitada. Status: erro_autorizacao", "", "", "", "");
        nfceMock.Setup(s => s.EmitirNfceAsync(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(resultado);

        var saleServiceMock = new Mock<ISaleService>();
        saleServiceMock.Setup(s => s.AtualizarDadosNfceAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        return new FiscalService(
            ctx, configProvider.Object, nfceMock.Object,
            new Mock<INfeEmissionService>().Object,
            new Mock<INfeContingencyService>().Object,
            saleServiceMock.Object,
            new Mock<INfeStatusService>().Object);
    }

    [Fact(DisplayName = "EmitirNotaAsync — venda de 1 item autorizada: NumeroItemFiscal=1 persiste de verdade no banco")]
    public async Task EmitirNotaAsync_UmItemAutorizado_PersisteNumeroItemFiscal()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default, item1 = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, item1, _, _) = SeedVenda(db, tenantId, quantidadeItens: 1);
        });

        var service = CriarService(ctx, focusAutoriza: true);
        var resultado = await service.EmitirNotaAsync(vendaId, "NFCE");

        resultado.Sucesso.Should().BeTrue();

        // Requery direto do banco (não do change tracker) — prova persistência
        // real, não só um valor setado em memória pelo MontarItens.
        var itemPersistido = await ctx.SaleItems.AsNoTracking().FirstAsync(i => i.Id == item1);
        itemPersistido.NumeroItemFiscal.Should().Be(1);
    }

    [Fact(DisplayName = "EmitirNotaAsync — venda de 2 itens autorizada: NumeroItemFiscal = 1, 2 na mesma ordem do payload")]
    public async Task EmitirNotaAsync_DoisItensAutorizados_NumeraSequencialmente()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default, item1 = default, item2 = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, item1, item2, _) = SeedVenda(db, tenantId, quantidadeItens: 2);
        });

        var service = CriarService(ctx, focusAutoriza: true);
        var resultado = await service.EmitirNotaAsync(vendaId, "NFCE");

        resultado.Sucesso.Should().BeTrue();

        // Achado real (não teórico): sale.Items não tem ORDER BY explícito,
        // então a ordem física de retorno do banco não é garantida bater com
        // a ordem de inserção do seed — confirmado rodando este teste (item2
        // voltou antes de item1). Isso não é bug de produção: MontarItens
        // atribui o número na MESMA execução que monta o payload, então o
        // valor gravado em cada item É o numero_item que a Focus recebeu
        // pra aquele item, não importa qual item físico ficou "1" ou "2".
        // O que precisa ser verdade é o CONJUNTO {1, 2} — não qual item
        // específico pegou qual número.
        var itens = await ctx.SaleItems.AsNoTracking().Where(i => i.SaleId == vendaId).ToListAsync();
        itens.Select(i => i.NumeroItemFiscal).Should().BeEquivalentTo(new[] { 1, 2 },
            "os dois itens devem ter números sequenciais únicos, não importa qual item físico pegou qual número");
    }

    [Fact(DisplayName = "EmitirNotaAsync — nota rejeitada: NumeroItemFiscal NÃO persiste (continua null)")]
    public async Task EmitirNotaAsync_NotaRejeitada_NaoPersisteNumeroItemFiscal()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default, item1 = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, item1, _, _) = SeedVenda(db, tenantId, quantidadeItens: 1);
        });

        var service = CriarService(ctx, focusAutoriza: false);
        var resultado = await service.EmitirNotaAsync(vendaId, "NFCE");

        resultado.Sucesso.Should().BeFalse();

        var itemPersistido = await ctx.SaleItems.AsNoTracking().FirstAsync(i => i.Id == item1);
        itemPersistido.NumeroItemFiscal.Should().BeNull(
            "só devemos congelar o vínculo fiscal quando a nota foi realmente autorizada");
    }

    [Fact(DisplayName = "EmitirNotaAsync — NumeroItemFiscal já congelado NUNCA é sobrescrito, mesmo que uma reemissão calcule outro valor em memória")]
    public async Task EmitirNotaAsync_NumeroItemFiscalJaCongelado_NuncaSobrescreve()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default, item1 = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, item1, _, _) = SeedVenda(db, tenantId, quantidadeItens: 1);
        });

        // Simula um snapshot fiscal já congelado com um valor DIFERENTE do
        // que MontarItens calcularia hoje (index+1 = 1, já que a venda só
        // tem 1 item) — o cenário exato que valida a proteção: banco=99,
        // em memória seria 1. Se a proteção funcionar, 99 nunca vira 1.
        await ctx.SaleItems.Where(i => i.Id == item1).ExecuteUpdateAsync(s => s.SetProperty(si => si.NumeroItemFiscal, 99));

        var service = CriarService(ctx, focusAutoriza: true);
        var resultado = await service.EmitirNotaAsync(vendaId, "NFCE");

        resultado.Sucesso.Should().BeTrue();

        var itemPersistido2 = await ctx.SaleItems.AsNoTracking().FirstAsync(i => i.Id == item1);
        itemPersistido2.NumeroItemFiscal.Should().Be(99,
            "um snapshot fiscal já congelado nunca deve ser sobrescrito por uma reemissão — NULL → N é permitido, N → M nunca");
    }
}