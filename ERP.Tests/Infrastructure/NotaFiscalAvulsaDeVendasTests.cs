// ERP.Tests/Infrastructure/NotaFiscalAvulsaDeVendasTests.cs
using ERP.Application.DTOs;
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using ERP.Tests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// S28 FIX — "NF-e única": junta os itens de várias vendas do mesmo cliente
/// num único RASCUNHO de NF-e avulsa (cliente quitou várias vendas a prazo e
/// quer uma nota só). Nunca emite: o teste de cada caso olha o rascunho
/// gravado e as travas contra faturar duas vezes a mesma venda.
/// </summary>
public class NotaFiscalAvulsaDeVendasTests
{
    private static (NotaFiscalAvulsaService Service, AppDbContext Ctx) Build(
        Guid tenantId, Action<AppDbContext> seed)
    {
        var ctx = TestDbSqlite.Create(tenantId, seed);

        var uowMock = new Mock<IUnitOfWork>();
        var configMock = new Mock<IFiscalConfigurationProvider>();
        configMock.Setup(c => c.ObterConfiguracaoAsync())
            .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

        var nfeMock = new Mock<INfeEmissionService>();
        var cancelMock = new Mock<INfeCancellationService>();
        var statusMock = new Mock<INfeStatusService>();
        var motorFiscalMock = new Mock<IMotorFiscalService>();
        var tenant = new FakeRequestTenant { TenantId = tenantId };

        var service = new NotaFiscalAvulsaService(
            ctx, uowMock.Object, motorFiscalMock.Object, configMock.Object, nfeMock.Object,
            cancelMock.Object, statusMock.Object, tenant);

        return (service, ctx);
    }

    private static Product NovoProduto(Guid tenantId, string nome = "Cimento CP-II 50kg", string? cfop = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Name = nome, SalePrice = 35m,
        NCM = "25232900", CSOSN = "102", SKU = Guid.NewGuid().ToString("N")[..8], CFOPPadrao = cfop
    };

        private static Customer NovoCliente(Guid tenantId, string? ie = null, string documento = "123.456.789-09") => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Name = "ENZO GABRIEL", Document = documento,
        StateRegistration = ie, Street = "Rua A", Number = "10", Neighborhood = "Centro",
        City = "Curitiba", State = "PR", ZipCode = "80000-000"
    };

    private static Sale NovaVenda(Guid tenantId, Guid? clienteId, Product produto, string numero,
        decimal qtd, decimal total, SaleStatus status = SaleStatus.SemNota)
    {
        var id = Guid.NewGuid();
        return new Sale
        {
            Id = id, TenantId = tenantId, SaleNumber = numero, CustomerId = clienteId,
            SaleDate = new DateTime(2026, 10, 8), Status = status,
            Items =
            {
                new SaleItem
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, SaleId = id, ProductId = produto.Id,
                    ProductName = produto.Name, Quantity = qtd, UnitPrice = total / qtd, TotalItem = total
                }
            }
        };
    }

    [Fact(DisplayName = "Junta os itens de duas vendas do mesmo cliente num rascunho único, com o pagamento real")]
    public async Task JuntaDuasVendas_UmRascunho_ItensSomados()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 2, 70m);
        var v2 = NovaVenda(t, cli.Id, prod, "VND-002", 1, 35m);
        var (service, ctx) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.AddRange(v1, v2); });

        var r = await service.CriarRascunhoDeVendasAsync(new[] { v1.Id, v2.Id }, "17");

        r.QtdVendas.Should().Be(2);
        r.TotalNota.Should().Be(105m);
        r.TotalEsperado.Should().Be(105m);
        r.Avisos.Should().BeEmpty();

        var nota = await ctx.NotasFiscais.Include(n => n.Itens).Include(n => n.Pagamentos)
            .AsNoTracking().SingleAsync(n => n.Id == r.NotaId);
        nota.Status.Should().Be("Rascunho");
        nota.Itens.Should().ContainSingle();
        nota.Itens.Single().Quantidade.Should().Be(3m);
        nota.Itens.Single().ValorUnitario.Should().Be(35m);
        nota.Pagamentos.Should().ContainSingle();
        nota.Pagamentos.Single().FormaPagamento.Should().Be("17");
        nota.Pagamentos.Single().Valor.Should().Be(105m);
        nota.InformacoesComplementares.Should().Contain("VND-001").And.Contain("VND-002");
        nota.DestinatarioNome.Should().Be("ENZO GABRIEL");
        nota.DestinatarioDocumento.Should().Be("12345678909");
    }

    [Fact(DisplayName = "Mesmo produto com preço diferente fica em linhas separadas")]
    public async Task PrecoDiferente_LinhasSeparadas()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 1, 35m);
        var v2 = NovaVenda(t, cli.Id, prod, "VND-002", 1, 40m);
        var (service, ctx) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.AddRange(v1, v2); });

        var r = await service.CriarRascunhoDeVendasAsync(new[] { v1.Id, v2.Id }, "01");

        r.QtdItens.Should().Be(2);
        r.TotalNota.Should().Be(75m);
    }

    [Fact(DisplayName = "Desconto é rateado nos itens e o total da nota fecha")]
    public async Task Desconto_RateadoNosItens()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 2, 70m);
        var v2 = NovaVenda(t, cli.Id, prod, "VND-002", 1, 35m);
        var (service, ctx) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.AddRange(v1, v2); });

        var r = await service.CriarRascunhoDeVendasAsync(new[] { v1.Id, v2.Id }, "01", desconto: 15m);

        r.TotalEsperado.Should().Be(90m);
        r.TotalNota.Should().Be(90m);
        r.Avisos.Should().BeEmpty();
        var nota = await ctx.NotasFiscais.Include(n => n.Itens).Include(n => n.Pagamentos)
            .AsNoTracking().SingleAsync(n => n.Id == r.NotaId);
        nota.Itens.Single().ValorUnitario.Should().Be(30m);
        nota.Pagamentos.Single().Valor.Should().Be(90m);
    }

    [Fact(DisplayName = "Desconto que não fecha em centavos gera aviso e o pagamento acompanha o total da nota")]
    public async Task Desconto_ComSobraDeCentavos_GeraAviso()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 3, 105m);
        var (service, ctx) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.Add(v1); });

        var r = await service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01", desconto: 5m);

        r.TotalEsperado.Should().Be(100m);
        r.TotalNota.Should().Be(99.99m);
        r.Avisos.Should().ContainSingle(a => a.Contains("arredondamento"));
        var nota = await ctx.NotasFiscais.Include(n => n.Pagamentos).AsNoTracking().SingleAsync(n => n.Id == r.NotaId);
        nota.Pagamentos.Single().Valor.Should().Be(99.99m); // pagamento = total declarado, senão a SEFAZ rejeita
    }

    [Fact(DisplayName = "CFOP vem do cadastro do produto; sem cadastro cai em 5102")]
    public async Task Cfop_DoProduto()
    {
        var t = Guid.NewGuid();
        var comCfop = NovoProduto(t, "Tinta", cfop: "5.405");
        var semCfop = NovoProduto(t, "Cimento");
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, comCfop, "VND-001", 1, 100m);
        var v2 = NovaVenda(t, cli.Id, semCfop, "VND-002", 1, 35m);
        var (service, ctx) = Build(t, c => { c.Products.AddRange(comCfop, semCfop); c.Customers.Add(cli); c.Sales.AddRange(v1, v2); });

        var r = await service.CriarRascunhoDeVendasAsync(new[] { v1.Id, v2.Id }, "01");

        var nota = await ctx.NotasFiscais.Include(n => n.Itens).AsNoTracking().SingleAsync(n => n.Id == r.NotaId);
        nota.Itens.Single(i => i.ProductId == comCfop.Id).Cfop.Should().Be("5405");
        nota.Itens.Single(i => i.ProductId == semCfop.Id).Cfop.Should().Be("5102");
    }

    [Fact(DisplayName = "Cliente com IE vira contribuinte (1); sem IE fica não contribuinte (9)")]
    public async Task IndicadorDeIe()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
                var comIe = NovoCliente(t, ie: "9053865574", documento: "111.444.777-35");
        var semIe = NovoCliente(t, ie: "ISENTO", documento: "529.982.247-25");
        var v1 = NovaVenda(t, comIe.Id, prod, "VND-001", 1, 35m);
        var v2 = NovaVenda(t, semIe.Id, prod, "VND-002", 1, 35m);
        var (service, ctx) = Build(t, c => { c.Products.Add(prod); c.Customers.AddRange(comIe, semIe); c.Sales.AddRange(v1, v2); });

        var r1 = await service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01");
        var r2 = await service.CriarRascunhoDeVendasAsync(new[] { v2.Id }, "01");

        var n1 = await ctx.NotasFiscais.AsNoTracking().SingleAsync(n => n.Id == r1.NotaId);
        var n2 = await ctx.NotasFiscais.AsNoTracking().SingleAsync(n => n.Id == r2.NotaId);
        n1.IndicadorIeDestinatario.Should().Be("1");
        n1.DestinatarioIe.Should().Be("9053865574");
        n2.IndicadorIeDestinatario.Should().Be("9");
        n2.DestinatarioIe.Should().BeNull();
    }

    // ── Travas ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Bloqueia venda que já tem nota emitida (NotaEmitida)")]
    public async Task Bloqueia_VendaComNotaEmitida()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 1, 35m, SaleStatus.NotaEmitida);
        var (service, ctx) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.Add(v1); });

        Func<Task> act = () => service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*já têm nota*VND-001*");
        (await ctx.NotasFiscais.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "Bloqueia venda com NFC-e autorizada registrada na própria venda")]
    public async Task Bloqueia_VendaComNfceAutorizada()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 1, 35m);
        v1.NfceStatusFocus = "Autorizada";
        var (service, _) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.Add(v1); });

        Func<Task> act = () => service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*já têm nota*");
    }

    [Fact(DisplayName = "Bloqueia venda com NotaFiscal viva ligada a ela")]
    public async Task Bloqueia_VendaComNotaFiscalLigada()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 1, 35m);
        var nf = new NotaFiscal { Id = Guid.NewGuid(), TenantId = t, Tipo = "NFCE", Status = "Autorizada", VendaId = v1.Id };
        var (service, _) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.Add(v1); c.NotasFiscais.Add(nf); });

        Func<Task> act = () => service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*já têm nota*");
    }

    [Fact(DisplayName = "Bloqueia venda já faturada em outra NF-e única autorizada")]
    public async Task Bloqueia_VendaJaFaturadaEmOutraNfeUnica()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 1, 35m);
        var anterior = new NotaFiscal
        {
            Id = Guid.NewGuid(), TenantId = t, Tipo = "NFE", Status = "Autorizada", VendaId = null,
            InformacoesComplementares = "Vendas faturadas: VND-001, VND-009 (vendas de 08/10/2026 a 13/10/2026)."
        };
        var (service, ctx) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.Add(v1); c.NotasFiscais.Add(anterior); });

        Func<Task> act = () => service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*já foram faturadas em outra NF-e única*VND-001*");
    }

    [Fact(DisplayName = "Rascunho anterior (não emitido) NÃO trava criar de novo")]
    public async Task RascunhoAnterior_NaoTrava()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 1, 35m);
        var rascunho = new NotaFiscal
        {
            Id = Guid.NewGuid(), TenantId = t, Tipo = "NFE", Status = "Rascunho", VendaId = null,
            InformacoesComplementares = "Vendas faturadas: VND-001 (vendas de 08/10/2026 a 08/10/2026)."
        };
        var (service, _) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.Add(v1); c.NotasFiscais.Add(rascunho); });

        var r = await service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01");

        r.NotaId.Should().NotBeEmpty();
    }

    [Fact(DisplayName = "Bloqueia vendas de clientes diferentes")]
    public async Task Bloqueia_ClientesDiferentes()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var c1 = NovoCliente(t);
                var c2 = NovoCliente(t, documento: "111.444.777-35");
        var v1 = NovaVenda(t, c1.Id, prod, "VND-001", 1, 35m);
        var v2 = NovaVenda(t, c2.Id, prod, "VND-002", 1, 35m);
        var (service, _) = Build(t, c => { c.Products.Add(prod); c.Customers.AddRange(c1, c2); c.Sales.AddRange(v1, v2); });

        Func<Task> act = () => service.CriarRascunhoDeVendasAsync(new[] { v1.Id, v2.Id }, "01");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*mesmo cliente*");
    }

    [Fact(DisplayName = "Bloqueia venda sem cliente cadastrado")]
    public async Task Bloqueia_VendaSemCliente()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var v1 = NovaVenda(t, null, prod, "VND-001", 1, 35m);
        var (service, _) = Build(t, c => { c.Products.Add(prod); c.Sales.Add(v1); });

        Func<Task> act = () => service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*mesmo cliente*");
    }

    [Fact(DisplayName = "Bloqueia venda cancelada")]
    public async Task Bloqueia_VendaCancelada()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 1, 35m, SaleStatus.Cancelada);
        var (service, _) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.Add(v1); });

        Func<Task> act = () => service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cancelada*");
    }

    [Theory(DisplayName = "Forma de pagamento vazia ou 90 (sem pagamento) é recusada")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("90")]
    public async Task Bloqueia_FormaDePagamentoInvalida(string forma)
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 1, 35m);
        var (service, _) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.Add(v1); });

        Func<Task> act = () => service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, forma);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*forma de pagamento*");
    }

    [Fact(DisplayName = "Venda inexistente e lista vazia são recusadas")]
    public async Task Bloqueia_VendaInexistente_ListaVazia()
    {
        var t = Guid.NewGuid();
        var (service, _) = Build(t, _ => { });

        Func<Task> inexistente = () => service.CriarRascunhoDeVendasAsync(new[] { Guid.NewGuid() }, "01");
        Func<Task> vazia = () => service.CriarRascunhoDeVendasAsync(Array.Empty<Guid>(), "01");

        await inexistente.Should().ThrowAsync<KeyNotFoundException>();
        await vazia.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact(DisplayName = "Desconto maior ou igual ao total dos itens é recusado")]
    public async Task Bloqueia_DescontoMaiorQueTotal()
    {
        var t = Guid.NewGuid();
        var prod = NovoProduto(t);
        var cli = NovoCliente(t);
        var v1 = NovaVenda(t, cli.Id, prod, "VND-001", 1, 35m);
        var (service, _) = Build(t, c => { c.Products.Add(prod); c.Customers.Add(cli); c.Sales.Add(v1); });

        Func<Task> act = () => service.CriarRascunhoDeVendasAsync(new[] { v1.Id }, "01", desconto: 35m);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*desconto*");
    }
}
