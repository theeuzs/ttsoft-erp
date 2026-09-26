// ERP.Tests/Infrastructure/FiscalServiceReconciliationTests.cs
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
/// Achado real (25/09) — uma devolução foi autorizada pela SEFAZ (Status
/// 100) mas o ERP nunca soube: a resposta chegou depois da janela de
/// consulta de 3s de NfeEmissionService, e nem EmitirNotaAsync nem
/// EmitirNotaDevolucaoAsync persistiam nada nesse caso — caía direto no
/// fallback de falha. Corrigido replicando o mesmo padrão já provado em
/// NotaFiscalAvulsaService (S27, 19-20/08), nunca antes conectado a venda ou
/// devolução: persistir "Processando" e reconciliar depois, via
/// NfeStatusReconciliationHostedService. NUNCA reenvia o documento — só
/// consulta (reenviar um "processando" arriscaria duplicidade fiscal).
///
/// Devolução usa Rota A: a NotaFiscal é criada ANTES de chamar a Focus, com
/// Status="Processando" e referência determinística ("devolucao-{Id}") — a
/// referência antiga (com timestamp embutido) nunca era reconstruível depois,
/// o que por si só já impedia qualquer reconciliação de devolução.
///
/// Usa TestDbSqlite (SQLite real) — ExecuteUpdateAsync e verificação de
/// persistência real não funcionam com o provider InMemory (S26).
/// </summary>
public class FiscalServiceReconciliationTests
{
    private static AppDbContext CriarContexto(Guid tenantId, Action<AppDbContext> seed)
    {
        var connection = new SqliteConnection($"DataSource=reconc_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
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

    private const string ChaveOriginal = "41260912820608000141650010000033031335484324";

    private static (Guid VendaId, Guid SaleItemId, Guid ProdutoId, Guid CustomerId) SeedVendaComNota(
        AppDbContext ctx, Guid tenantId, string? nfceStatusFocus, int? numeroItemFiscal)
    {
        var vendaId    = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var produtoId  = Guid.NewGuid();
        var saleItemId = Guid.NewGuid();

        var produto = new Product { Id = produtoId, Name = "Produto Teste", TenantId = tenantId, SalePrice = 10m, NCM = "39172300", CSOSN = "102" };
        ctx.Products.Add(produto);

        var customer = new Customer { Id = customerId, TenantId = tenantId, Name = "Cliente Teste", Document = "10087600994" };
        ctx.Customers.Add(customer);

        ctx.Sales.Add(new Sale
        {
            Id = vendaId, TenantId = tenantId, SaleNumber = "TESTE-RECONC",
            SaleDate = DateTime.Now, CustomerId = customerId, Customer = customer,
            NfceChave        = ChaveOriginal,
            NfceStatusFocus  = nfceStatusFocus,
            NfceReferencia   = vendaId.ToString(),
            Items = new List<SaleItem>
            {
                new() { Id = saleItemId, TenantId = tenantId, ProductId = produtoId, Product = produto,
                        ProductName = produto.Name, Quantity = 1, UnitPrice = 10m, TotalItem = 10m,
                        NumeroItemFiscal = numeroItemFiscal }
            }
        });

        return (vendaId, saleItemId, produtoId, customerId);
    }

    private static (FiscalService Service, Mock<INfeStatusService> StatusMock, Mock<ISaleService> SaleServiceMock)
        CriarServiceParaReconciliacao(AppDbContext ctx)
    {
        var configProvider = new Mock<IFiscalConfigurationProvider>();
        configProvider.Setup(c => c.ObterConfiguracaoAsync())
            .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

        var statusMock = new Mock<INfeStatusService>();
        var saleServiceMock = new Mock<ISaleService>();
        saleServiceMock.Setup(s => s.AtualizarDadosNfceAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var service = new FiscalService(
            ctx, configProvider.Object,
            new Mock<INfceEmissionService>().Object,
            new Mock<INfeEmissionService>().Object,
            new Mock<INfeContingencyService>().Object,
            saleServiceMock.Object,
            statusMock.Object);

        return (service, statusMock, saleServiceMock);
    }

    // ── VENDA — EmitirNotaAsync persiste "Processando" ──────────────────

    [Fact(DisplayName = "EmitirNotaAsync — processando_autorizacao chama AtualizarDadosNfceAsync com Status=Processando (achado real: antes caía em Falha e nada era persistido)")]
    public async Task EmitirNotaAsync_Processando_PersisteStatusProcessando()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, _, _, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: null, numeroItemFiscal: null);
        });

        var configProvider = new Mock<IFiscalConfigurationProvider>();
        configProvider.Setup(c => c.ObterConfiguracaoAsync())
            .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

        var nfceMock = new Mock<INfceEmissionService>();
        nfceMock.Setup(s => s.EmitirNfceAsync(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((true, "A Nota está processando na SEFAZ. Consulte o status em instantes.", "", "", "", ""));

        var saleServiceMock = new Mock<ISaleService>();
        saleServiceMock.Setup(s => s.AtualizarDadosNfceAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var service = new FiscalService(
            ctx, configProvider.Object, nfceMock.Object,
            new Mock<INfeEmissionService>().Object,
            new Mock<INfeContingencyService>().Object,
            saleServiceMock.Object,
            new Mock<INfeStatusService>().Object);

        var resultado = await service.EmitirNotaAsync(vendaId, "NFCE");

        resultado.Sucesso.Should().BeTrue("processando não é falha — o documento pode vir a ser autorizado depois");
        resultado.Status.Should().Be("Processando");
        saleServiceMock.Verify(s => s.AtualizarDadosNfceAsync(
            vendaId, "", "Processando", It.IsAny<string>(), vendaId.ToString(), null, null),
            Times.Once,
            "antes desta correção, esse ramo nunca chamava AtualizarDadosNfceAsync — caía direto no fallback de Falha, sem persistir nada");
    }

    // ── VENDA — ReconciliarVendaProcessandoAsync ────────────────────────

    [Fact(DisplayName = "ReconciliarVendaProcessandoAsync — Focus autoriza: persiste chave/numero, NumeroItemFiscal e cria a NotaFiscal")]
    public async Task ReconciliarVenda_Autorizada_PersisteTudo()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default, saleItemId = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, saleItemId, _, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: "Processando", numeroItemFiscal: null);
        });

        var (service, statusMock, saleServiceMock) = CriarServiceParaReconciliacao(ctx);
        statusMock.Setup(s => s.ConsultarStatusNotaAsync(vendaId.ToString(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((true, "autorizado", "https://focus/danfe.html", "41260912820608000141650010000033181976148900", "3318", "https://focus/x.xml", "NFCE"));

        await service.ReconciliarVendaProcessandoAsync(vendaId);

        saleServiceMock.Verify(s => s.AtualizarDadosNfceAsync(
            vendaId, "https://focus/danfe.html", "Autorizada", It.IsAny<string>(), vendaId.ToString(),
            "41260912820608000141650010000033181976148900", "3318"), Times.Once);

        var item = await ctx.SaleItems.AsNoTracking().FirstAsync(i => i.Id == saleItemId);
        item.NumeroItemFiscal.Should().Be(1, "reconciliação precisa atribuir a mesma numeração que a emissão original teria usado");

        var nota = await ctx.NotasFiscais.AsNoTracking().FirstOrDefaultAsync(n => n.VendaId == vendaId);
        nota.Should().NotBeNull("antes desta correção, uma venda 'processando' que resolvia depois nunca ganhava NotaFiscal nenhuma");
        nota!.Status.Should().Be("Autorizada");
        nota.Tipo.Should().Be("NFCE");
    }

    [Fact(DisplayName = "ReconciliarVendaProcessandoAsync — ainda processando: não altera nada, tenta de novo depois")]
    public async Task ReconciliarVenda_AindaProcessando_NaoAltera()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default, saleItemId = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, saleItemId, _, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: "Processando", numeroItemFiscal: null);
        });

        var (service, statusMock, saleServiceMock) = CriarServiceParaReconciliacao(ctx);
        statusMock.Setup(s => s.ConsultarStatusNotaAsync(vendaId.ToString(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((true, "processando_autorizacao", "", "", "", "", ""));

        await service.ReconciliarVendaProcessandoAsync(vendaId);

        saleServiceMock.Verify(s => s.AtualizarDadosNfceAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        var item = await ctx.SaleItems.AsNoTracking().FirstAsync(i => i.Id == saleItemId);
        item.NumeroItemFiscal.Should().BeNull();
        (await ctx.NotasFiscais.AsNoTracking().AnyAsync(n => n.VendaId == vendaId)).Should().BeFalse();
    }

    [Fact(DisplayName = "ReconciliarVendaProcessandoAsync — SEFAZ rejeitou: marca Rejeitada, não fica Processando pra sempre")]
    public async Task ReconciliarVenda_Rejeitada_MarcaRejeitada()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, _, _, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: "Processando", numeroItemFiscal: null);
        });

        var (service, statusMock, saleServiceMock) = CriarServiceParaReconciliacao(ctx);
        statusMock.Setup(s => s.ConsultarStatusNotaAsync(vendaId.ToString(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((true, "erro_autorizacao", "", "", "", "", ""));

        await service.ReconciliarVendaProcessandoAsync(vendaId);

        saleServiceMock.Verify(s => s.AtualizarDadosNfceAsync(
            vendaId, "", "Rejeitada", It.IsAny<string>(), vendaId.ToString(), null, null), Times.Once);
    }

    [Fact(DisplayName = "ReconciliarVendaProcessandoAsync — venda que não está Processando (ex: já Autorizada) é ignorada, nunca consulta a Focus")]
    public async Task ReconciliarVenda_NaoProcessando_NaoConsulta()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, _, _, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: "Autorizada", numeroItemFiscal: 1);
        });

        var (service, statusMock, _) = CriarServiceParaReconciliacao(ctx);

        await service.ReconciliarVendaProcessandoAsync(vendaId);

        statusMock.Verify(s => s.ConsultarStatusNotaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    // ── DEVOLUÇÃO — Rota A: NotaFiscal criada ANTES da Focus ────────────

    private static (FiscalService Service, Mock<INfeEmissionService> NfeMock, Mock<INfeStatusService> StatusMock)
        CriarServiceParaDevolucao(AppDbContext ctx)
    {
        var configProvider = new Mock<IFiscalConfigurationProvider>();
        configProvider.Setup(c => c.ObterConfiguracaoAsync())
            .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

        var nfeMock = new Mock<INfeEmissionService>();
        var statusMock = new Mock<INfeStatusService>();

        var service = new FiscalService(
            ctx, configProvider.Object,
            new Mock<INfceEmissionService>().Object,
            nfeMock.Object,
            new Mock<INfeContingencyService>().Object,
            new Mock<ISaleService>().Object,
            statusMock.Object);

        return (service, nfeMock, statusMock);
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — processando: NotaFiscal já existe com Status=Processando ANTES de saber o resultado, referência é devolucao-{Id}")]
    public async Task EmitirNotaDevolucao_Processando_CriaNotaAntesComReferenciaDeterministica()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default, saleItemId = default, produtoId = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, saleItemId, produtoId, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: "Autorizada", numeroItemFiscal: 1);
        });

        var (service, nfeMock, _) = CriarServiceParaDevolucao(ctx);
        string? referenciaRecebida = null;
        nfeMock.Setup(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Callback<string, FocusNfceRequest, string, bool>((referencia, _, _, _) => referenciaRecebida = referencia)
            .ReturnsAsync((true, "A Nota está processando na SEFAZ. Consulte o status em instantes.", "", "", "", ""));

        var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
            { (saleItemId, produtoId, "Produto Teste", 1m, 10m) };

        var resultado = await service.EmitirNotaDevolucaoAsync(vendaId, itens, "motivo");

        resultado.Sucesso.Should().BeTrue("processando não é falha");
        resultado.Status.Should().Be("Processando");

        var nota = await ctx.NotasFiscais.AsNoTracking().FirstOrDefaultAsync(n => n.VendaId == vendaId && n.Finalidade == "4");
        nota.Should().NotBeNull("Rota A: a NotaFiscal precisa existir MESMO quando ainda processando — é o que a torna reconciliável depois");
        nota!.Status.Should().Be("Processando");

        referenciaRecebida.Should().Be($"devolucao-{nota.Id}",
            "achado real: a referência antiga tinha timestamp embutido e nunca era reconstruível depois — agora é determinística, baseada no próprio Id já persistido");
    }

    [Fact(DisplayName = "EmitirNotaDevolucaoAsync — já existe devolução Processando pra essa venda: bloqueia, NUNCA chama a Focus de novo")]
    public async Task EmitirNotaDevolucao_JaExistePendente_BloqueiaSemChamarFocus()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default, saleItemId = default, produtoId = default;
        using var ctx = CriarContexto(tenantId, db =>
        {
            (vendaId, saleItemId, produtoId, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: "Autorizada", numeroItemFiscal: 1);
            db.NotasFiscais.Add(new NotaFiscal
            {
                Id = Guid.NewGuid(), TenantId = tenantId, VendaId = vendaId, Tipo = "NFE",
                Finalidade = "4", Status = "Processando", Ambiente = "Homologação"
            });
        });

        var (service, nfeMock, _) = CriarServiceParaDevolucao(ctx);
        var itens = new List<(Guid SaleItemId, Guid ProductId, string ProductName, decimal Quantidade, decimal ValorUnitario)>
            { (saleItemId, produtoId, "Produto Teste", 1m, 10m) };

        var resultado = await service.EmitirNotaDevolucaoAsync(vendaId, itens, "motivo");

        resultado.Mensagem.Should().Contain("aguardando confirmação");
        nfeMock.Verify(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never,
            "processando nunca pode reenviar — reenviar arriscaria duplicidade fiscal");

        (await ctx.NotasFiscais.AsNoTracking().CountAsync(n => n.VendaId == vendaId && n.Finalidade == "4")).Should().Be(1,
            "não pode nascer uma segunda NotaFiscal pra mesma tentativa pendente");
    }

    [Fact(DisplayName = "ReconciliarDevolucaoProcessandoAsync — Focus autoriza: atualiza a MESMA NotaFiscal, não cria outra")]
    public async Task ReconciliarDevolucao_Autorizada_AtualizaMesmaLinha()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default;
        var notaId = Guid.NewGuid();
        using var ctx = CriarContexto(tenantId, db =>
        {
            // SeedVendaComNota cria a Sale de verdade — NotaFiscal.VendaId
            // tem foreign key pra Sales.Id, e o SQLite (diferente do
            // InMemory) aplica essa constraint de verdade.
            (vendaId, _, _, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: "Autorizada", numeroItemFiscal: 1);
            db.NotasFiscais.Add(new NotaFiscal
            {
                Id = notaId, TenantId = tenantId, VendaId = vendaId, Tipo = "NFE",
                Finalidade = "4", Status = "Processando", Ambiente = "Homologação", RefNFe = ChaveOriginal
            });
        });

        var (service, _, statusMock) = CriarServiceParaDevolucao(ctx);
        statusMock.Setup(s => s.ConsultarStatusNotaAsync($"devolucao-{notaId}", It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((true, "autorizado", "https://focus/dev.pdf", "41260912820608000141650010000033151859707005", "319", "https://focus/dev.xml", "NFE"));

        await service.ReconciliarDevolucaoProcessandoAsync(notaId);

        var nota = await ctx.NotasFiscais.AsNoTracking().SingleAsync(n => n.VendaId == vendaId && n.Finalidade == "4");
        nota.Id.Should().Be(notaId, "precisa ser a mesma linha, nunca uma nova");
        nota.Status.Should().Be("Autorizada");
        nota.Chave.Should().Be("41260912820608000141650010000033151859707005");
        nota.Numero.Should().Be("319");
        nota.UrlDanfe.Should().Be("https://focus/dev.pdf");
    }

    [Fact(DisplayName = "ReconciliarDevolucaoProcessandoAsync — SEFAZ rejeitou: marca Rejeitada, libera nova tentativa de devolução")]
    public async Task ReconciliarDevolucao_Rejeitada_MarcaRejeitada()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default;
        var notaId = Guid.NewGuid();
        using var ctx = CriarContexto(tenantId, db =>
        {
            // SeedVendaComNota cria a Sale de verdade — NotaFiscal.VendaId
            // tem foreign key pra Sales.Id, e o SQLite (diferente do
            // InMemory) aplica essa constraint de verdade.
            (vendaId, _, _, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: "Autorizada", numeroItemFiscal: 1);
            db.NotasFiscais.Add(new NotaFiscal
            {
                Id = notaId, TenantId = tenantId, VendaId = vendaId, Tipo = "NFE",
                Finalidade = "4", Status = "Processando", Ambiente = "Homologação", RefNFe = ChaveOriginal
            });
        });

        var (service, _, statusMock) = CriarServiceParaDevolucao(ctx);
        statusMock.Setup(s => s.ConsultarStatusNotaAsync($"devolucao-{notaId}", It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((true, "erro_autorizacao", "", "", "", "", ""));

        await service.ReconciliarDevolucaoProcessandoAsync(notaId);

        var nota = await ctx.NotasFiscais.AsNoTracking().SingleAsync(n => n.Id == notaId);
        nota.Status.Should().Be("Rejeitada");
    }

    [Fact(DisplayName = "ReconciliarDevolucaoProcessandoAsync — ainda processando: não altera nada")]
    public async Task ReconciliarDevolucao_AindaProcessando_NaoAltera()
    {
        var tenantId = Guid.NewGuid();
        Guid vendaId = default;
        var notaId = Guid.NewGuid();
        using var ctx = CriarContexto(tenantId, db =>
        {
            // SeedVendaComNota cria a Sale de verdade — NotaFiscal.VendaId
            // tem foreign key pra Sales.Id, e o SQLite (diferente do
            // InMemory) aplica essa constraint de verdade.
            (vendaId, _, _, _) = SeedVendaComNota(db, tenantId, nfceStatusFocus: "Autorizada", numeroItemFiscal: 1);
            db.NotasFiscais.Add(new NotaFiscal
            {
                Id = notaId, TenantId = tenantId, VendaId = vendaId, Tipo = "NFE",
                Finalidade = "4", Status = "Processando", Ambiente = "Homologação", RefNFe = ChaveOriginal
            });
        });

        var (service, _, statusMock) = CriarServiceParaDevolucao(ctx);
        statusMock.Setup(s => s.ConsultarStatusNotaAsync($"devolucao-{notaId}", It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync((true, "processando_autorizacao", "", "", "", "", ""));

        await service.ReconciliarDevolucaoProcessandoAsync(notaId);

        var nota = await ctx.NotasFiscais.AsNoTracking().SingleAsync(n => n.Id == notaId);
        nota.Status.Should().Be("Processando");
    }
}