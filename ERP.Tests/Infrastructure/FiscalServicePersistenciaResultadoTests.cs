// ERP.Tests/Infrastructure/FiscalServicePersistenciaResultadoTests.cs
using System.Data.Common;
using System.Linq.Expressions;
using ERP.Application.Fiscal;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// F-2: PersistirEmissaoAutorizadaComResultadoAsync diz o que foi REALMENTE gravado (conferido por leitura
/// de volta no banco), e o metodo antigo continua tolerante. SQLite real, como os testes de NumeroItemFiscal:
/// a persistencia usa ExecuteUpdateAsync (SQL bruto), que o InMemory nao suporta.
///
/// As falhas de banco sao simuladas por um interceptor que derruba SO o comando de escrita da tabela pedida.
/// </summary>
public class FiscalServicePersistenciaResultadoTests
{
    private const string UrlDanfe = "https://focus/danfe.html";
    private const string Chave = "41260912820608000141650010000033031335484399";
    private const string Numero = "9901";

    // ── Infraestrutura ───────────────────────────────────────────────────

    private sealed class FalhaEmComandoInterceptor : DbCommandInterceptor
    {
        /// <summary>Nome da tabela, ja entre aspas (ex.: "\"SaleItems\""). Nulo = nao falha nada.</summary>
        public string? Tabela { get; set; }

        private void Verificar(DbCommand command)
        {
            if (Tabela is null)
                return;

            var texto = command.CommandText;
            var escreve = texto.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                       || texto.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase);

            if (escreve && texto.Contains(Tabela, StringComparison.Ordinal))
                throw new InvalidOperationException("falha simulada de banco em " + Tabela);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Verificar(command);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Verificar(command);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Verificar(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Verificar(command);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private enum ComoAVendaGrava { Grava, NaoFazNada, Lanca }

    private sealed class Cena : IDisposable
    {
        public Cena(AppDbContext ctx, FalhaEmComandoInterceptor falha, Guid vendaId, Guid item1, Guid item2,
                    Mock<ISaleService> vendas, FiscalService service)
        {
            Ctx = ctx; Falha = falha; VendaId = vendaId; Item1 = item1; Item2 = item2; Vendas = vendas; Service = service;
        }

        public AppDbContext Ctx { get; }
        public FalhaEmComandoInterceptor Falha { get; }
        public Guid VendaId { get; }
        public Guid Item1 { get; }
        public Guid Item2 { get; }
        public Mock<ISaleService> Vendas { get; }
        public FiscalService Service { get; }

        public void Dispose() => Ctx.Dispose();
    }

    private static AppDbContext CriarContexto(Guid tenantId, FalhaEmComandoInterceptor falha, Action<AppDbContext> seed)
    {
        var connection = new SqliteConnection($"DataSource=persres_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        connection.Open();

        AppDbContext.SetGlobalTenantId(tenantId);
        AppDbContext.SetQueryTenantId(tenantId);
        var tenant = new ERP.Tests.FakeRequestTenant { TenantId = tenantId };

        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).AddInterceptors(falha).Options;
        var db = new AppDbContext(options, tenant);
        db.Database.EnsureCreated();

        seed(db);
        db.SaveChanges();
        return db;
    }

    private static (Guid VendaId, Guid Item1Id, Guid Item2Id) SeedVenda(AppDbContext ctx, Guid tenantId, int quantidadeItens)
    {
        var vendaId = Guid.NewGuid();
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
            Id = vendaId, TenantId = tenantId, SaleNumber = "TESTE-PERSRES",
            SaleDate = DateTime.Now, CustomerId = customerId, Customer = customer,
            Items = items
        });

        return (vendaId, item1Id, item2Id);
    }

    private static Expression<Func<ISaleService, Task>> QualquerAtualizar() =>
        s => s.AtualizarDadosNfceAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>());

    /// <summary>Faz o que o SaleService real faz: grava status/url/ambiente/ref e, so se nao vazios, chave e numero.</summary>
    private static Mock<ISaleService> SaleServiceQueGrava(AppDbContext ctx)
    {
        var mock = new Mock<ISaleService>();
        mock.Setup(QualquerAtualizar())
            .Returns<Guid, string, string, string, string, string?, string?>(async (id, url, status, ambiente, referencia, chave, numero) =>
            {
                await ctx.Sales.Where(s => s.Id == id).ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.NfceUrlDanfe, url)
                    .SetProperty(s => s.NfceStatusFocus, status)
                    .SetProperty(s => s.NfceAmbiente, ambiente)
                    .SetProperty(s => s.NfceReferencia, referencia));

                if (!string.IsNullOrWhiteSpace(chave))
                    await ctx.Sales.Where(s => s.Id == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.NfceChave, chave));

                if (!string.IsNullOrWhiteSpace(numero))
                    await ctx.Sales.Where(s => s.Id == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.NfceNumero, numero));
            });
        return mock;
    }

    /// <summary>O que o SaleService real faz quando nao acha a venda: volta sem lancar e sem gravar nada.</summary>
    private static Mock<ISaleService> SaleServiceQueNaoFazNada()
    {
        var mock = new Mock<ISaleService>();
        mock.Setup(QualquerAtualizar()).Returns(Task.CompletedTask);
        return mock;
    }

    private static Mock<ISaleService> SaleServiceQueLanca()
    {
        var mock = new Mock<ISaleService>();
        mock.Setup(QualquerAtualizar()).Throws(new InvalidOperationException("banco fora do ar"));
        return mock;
    }

    private static Cena Montar(ComoAVendaGrava vendas, int itens = 2)
    {
        var tenantId = Guid.NewGuid();
        var falha = new FalhaEmComandoInterceptor();
        Guid vendaId = default, item1 = default, item2 = default;
        var ctx = CriarContexto(tenantId, falha, db => { (vendaId, item1, item2) = SeedVenda(db, tenantId, itens); });

        var saleService = vendas switch
        {
            ComoAVendaGrava.Grava => SaleServiceQueGrava(ctx),
            ComoAVendaGrava.Lanca => SaleServiceQueLanca(),
            _ => SaleServiceQueNaoFazNada()
        };

        var configProvider = new Mock<IFiscalConfigurationProvider>();
        configProvider.Setup(c => c.ObterConfiguracaoAsync())
            .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

        var service = new FiscalService(
            ctx, configProvider.Object, new Mock<INfceEmissionService>().Object,
            new Mock<INfeEmissionService>().Object, new Mock<INfeContingencyService>().Object,
            saleService.Object, new Mock<INfeStatusService>().Object);

        return new Cena(ctx, falha, vendaId, item1, item2, saleService, service);
    }

    private static Task<PersistenciaAutorizacaoResultado> Persistir(Cena c, Guid? vendaId = null, string chave = Chave, string numero = Numero)
    {
        var id = vendaId ?? c.VendaId;
        return c.Service.PersistirEmissaoAutorizadaComResultadoAsync(
            id, "NFCE", UrlDanfe, "Homologação", id.ToString(), "https://focus/nfce.xml", chave, numero);
    }

    private static async Task<int?> NumeroDoItem(Cena c, Guid itemId) =>
        (await c.Ctx.SaleItems.AsNoTracking().FirstAsync(i => i.Id == itemId)).NumeroItemFiscal;

    /// <summary>
    /// Os numeros dos dois itens, em ordem crescente. A numeracao segue a ordem em que a venda e carregada
    /// (o EF ordena os itens filhos por Id, que aqui e um Guid aleatorio), entao o teste NAO pode assumir que
    /// o Item1 recebe o 1: compara-se o CONJUNTO. Os testes que precisam de um numero previsivel usam 1 item.
    /// </summary>
    private static async Task<List<int?>> NumerosDosItens(Cena c) =>
        (await c.Ctx.SaleItems.AsNoTracking()
            .Where(i => i.Id == c.Item1 || i.Id == c.Item2)
            .Select(i => i.NumeroItemFiscal)
            .ToListAsync())
        .OrderBy(n => n)
        .ToList();

    // ── Resultado ────────────────────────────────────────────────────────

    [Fact(DisplayName = "COMPLETO: venda encontrada, dados e numero do item gravados DE VERDADE; NotaFiscal Autorizada")]
    public async Task Completo()
    {
        using var c = Montar(ComoAVendaGrava.Grava);

        var r = await Persistir(c);

        r.Should().Be(new PersistenciaAutorizacaoResultado(true, true, true));
        r.Completa.Should().BeTrue();
        var venda = await c.Ctx.Sales.AsNoTracking().FirstAsync(s => s.Id == c.VendaId);
        venda.NfceStatusFocus.Should().Be("Autorizada");
        venda.NfceChave.Should().Be(Chave);
        venda.NfceNumero.Should().Be(Numero);
        (await NumerosDosItens(c)).Should().Equal(1, 2);
        var notas = await c.Ctx.NotasFiscais.AsNoTracking().Where(n => n.VendaId == c.VendaId).ToListAsync();
        notas.Should().ContainSingle();
        notas[0].Status.Should().Be("Autorizada");
        notas[0].Tipo.Should().Be("NFCE");
    }

    [Fact(DisplayName = "VENDA AUSENTE: devolve VendaAusente, nao chama o SaleService e nao cria NotaFiscal")]
    public async Task VendaAusente()
    {
        using var c = Montar(ComoAVendaGrava.Grava);

        var r = await Persistir(c, vendaId: Guid.NewGuid());

        r.Should().Be(PersistenciaAutorizacaoResultado.VendaAusente);
        r.Completa.Should().BeFalse();
        r.ResumoDoQueFalta().Should().Be("venda nao encontrada");
        c.Vendas.Verify(QualquerAtualizar(), Times.Never());
        (await c.Ctx.NotasFiscais.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "AtualizarDadosNfceAsync LANCA: dados nao gravados, mas o numero do item e a NotaFiscal seguem (comportamento de sempre)")]
    public async Task AtualizarLanca()
    {
        using var c = Montar(ComoAVendaGrava.Lanca, itens: 1);

        var r = await Persistir(c);

        r.Should().Be(new PersistenciaAutorizacaoResultado(true, false, true));
        r.Completa.Should().BeFalse();
        r.ResumoDoQueFalta().Should().Contain("dados da venda");
        (await NumeroDoItem(c, c.Item1)).Should().Be(1);
        (await c.Ctx.NotasFiscais.AsNoTracking().CountAsync(n => n.VendaId == c.VendaId && n.Status == "Autorizada")).Should().Be(1);
    }

    [Fact(DisplayName = "AtualizarDadosNfceAsync NAO LANCA mas NAO GRAVA (como o SaleService com venda nula): a leitura de volta pega isso")]
    public async Task AtualizarSemEfeito_ELeituraDeVoltaPega()
    {
        using var c = Montar(ComoAVendaGrava.NaoFazNada);

        var r = await Persistir(c);

        r.Should().Be(new PersistenciaAutorizacaoResultado(true, false, true));
        var venda = await c.Ctx.Sales.AsNoTracking().FirstAsync(s => s.Id == c.VendaId);
        venda.NfceStatusFocus.Should().BeNull("a chamada nao gravou nada: o resultado nao pode dizer o contrario");
    }

    [Fact(DisplayName = "NumeroItemFiscal NAO GRAVA (UPDATE de SaleItems falha): numero do item nao confirmado, o resto segue")]
    public async Task NumeroItemFalha()
    {
        using var c = Montar(ComoAVendaGrava.Grava);
        c.Falha.Tabela = "\"SaleItems\"";

        var r = await Persistir(c);

        r.Should().Be(new PersistenciaAutorizacaoResultado(true, true, false));
        r.ResumoDoQueFalta().Should().Contain("numero fiscal do item");
        (await NumeroDoItem(c, c.Item1)).Should().BeNull();
        (await c.Ctx.Sales.AsNoTracking().FirstAsync(s => s.Id == c.VendaId)).NfceStatusFocus.Should().Be("Autorizada");
        (await c.Ctx.NotasFiscais.AsNoTracking().CountAsync(n => n.VendaId == c.VendaId)).Should().Be(1);
    }

    [Fact(DisplayName = "Numero fiscal ja CONGELADO conta como gravado e nunca e sobrescrito")]
    public async Task NumeroCongelado_ContaComoGravado()
    {
        using var c = Montar(ComoAVendaGrava.Grava);
        await c.Ctx.SaleItems.Where(i => i.Id == c.Item1).ExecuteUpdateAsync(s => s.SetProperty(si => si.NumeroItemFiscal, 99));

        var r = await Persistir(c);

        r.Completa.Should().BeTrue();
        (await NumeroDoItem(c, c.Item1)).Should().Be(99, "um numero ja congelado nunca e sobrescrito");
        var outro = await NumeroDoItem(c, c.Item2);
        outro.Should().NotBeNull("o item que estava sem numero recebe um");
        outro!.Value.Should().BeInRange(1, 2);
    }

    [Fact(DisplayName = "Chave e numero em branco: basta o status 'Autorizada' gravado para a venda contar como gravada")]
    public async Task ChaveENumeroEmBranco()
    {
        using var c = Montar(ComoAVendaGrava.Grava);

        var r = await Persistir(c, chave: "", numero: "");

        r.Completa.Should().BeTrue();
    }

    [Fact(DisplayName = "Falha no registro da NotaFiscal continua sendo LANCADA (nos dois metodos), com o resto ja persistido como hoje")]
    public async Task RegistroDaNotaFiscalFalha_ContinuaLancando()
    {
        using var c = Montar(ComoAVendaGrava.Grava, itens: 1);
        c.Falha.Tabela = "\"NotasFiscais\"";

        Func<Task> novo = async () => await Persistir(c);
        Func<Task> antigo = async () => await c.Service.PersistirEmissaoAutorizadaAsync(
            c.VendaId, "NFCE", UrlDanfe, "Homologação", c.VendaId.ToString(), "https://focus/nfce.xml", Chave, Numero);

        await novo.Should().ThrowAsync<Exception>();
        await antigo.Should().ThrowAsync<Exception>();
        (await c.Ctx.Sales.AsNoTracking().FirstAsync(s => s.Id == c.VendaId)).NfceStatusFocus.Should().Be("Autorizada");
        (await NumeroDoItem(c, c.Item1)).Should().Be(1);
    }

    [Fact(DisplayName = "IDEMPOTENTE: chamar duas vezes devolve completo nas duas, sem duplicar a NotaFiscal nem mexer nos numeros")]
    public async Task Idempotente()
    {
        using var c = Montar(ComoAVendaGrava.Grava);

        var r1 = await Persistir(c);
        var numerosDepoisDaPrimeira = await NumerosDosItens(c);
        var item1Primeira = await NumeroDoItem(c, c.Item1);
        var r2 = await Persistir(c);

        r1.Completa.Should().BeTrue();
        r2.Completa.Should().BeTrue();
        (await c.Ctx.NotasFiscais.AsNoTracking().CountAsync(n => n.VendaId == c.VendaId)).Should().Be(1);
        numerosDepoisDaPrimeira.Should().Equal(1, 2);
        (await NumerosDosItens(c)).Should().Equal(numerosDepoisDaPrimeira, "a segunda chamada nao mexe nos numeros");
        (await NumeroDoItem(c, c.Item1)).Should().Be(item1Primeira, "cada item mantem o seu numero");
    }

    // ── O metodo ANTIGO segue tolerante ──────────────────────────────────

    [Theory(DisplayName = "O metodo ANTIGO continua tolerante nos mesmos cenarios (nao lanca, como sempre)")]
    [InlineData("venda-ausente")]
    [InlineData("atualizar-lanca")]
    [InlineData("atualizar-sem-efeito")]
    [InlineData("numero-item-falha")]
    public async Task MetodoAntigo_ContinuaTolerante(string cenario)
    {
        var como = cenario switch
        {
            "atualizar-lanca" => ComoAVendaGrava.Lanca,
            "atualizar-sem-efeito" => ComoAVendaGrava.NaoFazNada,
            _ => ComoAVendaGrava.Grava
        };
        using var c = Montar(como);
        if (cenario == "numero-item-falha")
            c.Falha.Tabela = "\"SaleItems\"";
        var vendaId = cenario == "venda-ausente" ? Guid.NewGuid() : c.VendaId;

        Func<Task> act = async () => await c.Service.PersistirEmissaoAutorizadaAsync(
            vendaId, "NFCE", UrlDanfe, "Homologação", vendaId.ToString(), "https://focus/nfce.xml", Chave, Numero);

        await act.Should().NotThrowAsync();
    }

    // ── O tipo do resultado ──────────────────────────────────────────────

    [Theory(DisplayName = "Completa so e verdadeira com os TRES indicadores verdadeiros")]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public void Completa(bool venda, bool dados, bool numero, bool esperado)
    {
        new PersistenciaAutorizacaoResultado(venda, dados, numero).Completa.Should().Be(esperado);
    }
}