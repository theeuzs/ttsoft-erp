// ERP.Tests/Infrastructure/ContaReceberServiceTests.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.Repositories;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using ERP.Tests;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// Motor de crediário, sem cobertura de teste nenhuma até aqui (achado real
/// da auditoria de 13/08). A maioria dos métodos de escrita usa
/// ExecuteSqlInterpolatedAsync direto (SQL bruto, com o UPDATE ... WHERE
/// como guarda contra corrida) — por isso os testes aqui usam
/// TestDbSqlite (SQLite in-process real), não o provider InMemory, que
/// ignora SQL bruto silenciosamente.
///
/// S26 FIX (18/08): a primeira versão desses testes usava Guid.NewGuid()
/// solto como CustomerId, sem nunca seedar o Customer correspondente — o
/// InMemory (que ignora FK) deixaria passar, mas o SQLite real (por isso
/// escolhido aqui) valida de verdade e rejeitou com "FOREIGN KEY constraint
/// failed". Bug meu, na seed dos testes — não no código de produção.
/// Corrigido: todo teste agora seeda um Customer real primeiro.
///
/// Foco nas propriedades que os próprios comentários do código marcam como
/// já tendo sido bugs reais uma vez: teto de baixa considerando desconto já
/// aplicado, guarda contra cancelar conta paga, guarda contra desconto maior
/// que o saldo, e o rateio proporcional da baixa em lote.
/// </summary>
public class ContaReceberServiceTests
{
    private static (ContaReceberService Service, AppDbContext Ctx) Build(Guid tenantId, Action<AppDbContext>? seed = null)
    {
        var ctx = TestDbSqlite.Create(tenantId, seed);
        var tenant = new FakeRequestTenant { TenantId = tenantId };

        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.ContasReceber).Returns(new ContaReceberRepository(ctx));
        uowMock.Setup(u => u.CommitAsync()).Returns(() => ctx.SaveChangesAsync());

        return (new ContaReceberService(uowMock.Object, ctx, tenant, asaas: null), ctx);
    }

    /// <summary>Cria (sem salvar) um Customer válido pro tenant — chamar dentro
    /// do callback de seed do Build, antes de adicionar qualquer ContaReceber
    /// que referencie o Id devolvido (FK obrigatória).</summary>
    private static Customer NovoCliente(Guid tenantId) => new()
    {
        Id       = Guid.NewGuid(),
        TenantId = tenantId,
        Name     = "Cliente Teste",
        Document = "12345678900"
    };

    private static ContaReceber ContaPendente(Guid tenantId, Guid customerId, decimal valorTotal, decimal valorRecebido = 0, decimal valorDesconto = 0) => new()
    {
        Id             = Guid.NewGuid(),
        TenantId       = tenantId,
        CustomerId     = customerId,
        ValorTotal     = valorTotal,
        ValorRecebido  = valorRecebido,
        ValorDesconto  = valorDesconto,
        Status         = "Pendente",
        DataEmissao    = DateTime.Now,
        DataVencimento = DateTime.Now.AddDays(30),
        Descricao      = "Teste"
    };

    // ── DarBaixaParcialAsync ────────────────────────────────────────────

    [Fact(DisplayName = "Baixa parcial que não fecha o total mantém a conta Pendente")]
    public async Task DarBaixaParcial_NaoFechaOTotal_ContinuaPendente()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 100m);
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            await service.DarBaixaParcialAsync(conta.Id, 40m);

            var atualizada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            atualizada.ValorRecebido.Should().Be(40m);
            atualizada.Status.Should().Be("Pendente");
            atualizada.DataPagamento.Should().BeNull();
        }
    }

    [Fact(DisplayName = "Baixa que atinge o total exato marca Pago e registra DataPagamento")]
    public async Task DarBaixaParcial_AtingeOTotal_MarcaPago()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 100m, valorRecebido: 60m);
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            await service.DarBaixaParcialAsync(conta.Id, 40m);

            var atualizada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            atualizada.ValorRecebido.Should().Be(100m);
            atualizada.Status.Should().Be("Pago");
            atualizada.DataPagamento.Should().NotBeNull();
        }
    }

    [Fact(DisplayName = "Teto da baixa considera o desconto já aplicado, não só o ValorTotal")]
    public async Task DarBaixaParcial_ComDescontoJaAplicado_TetoConsideraODesconto()
    {
        // Conta de R$100 com R$20 de desconto já dado -> saldo real é R$80.
        // Tentar receber R$90 (mais do que o saldo real) tem que travar em R$80,
        // não em R$100 (que seria ignorar o desconto já concedido).
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 100m, valorRecebido: 0, valorDesconto: 20m);
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            await service.DarBaixaParcialAsync(conta.Id, 90m);

            var atualizada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            atualizada.ValorRecebido.Should().Be(80m, "o teto é ValorTotal - ValorDesconto, não ValorTotal puro");
            atualizada.Status.Should().Be("Pago");
        }
    }

    [Fact(DisplayName = "Baixa parcial em conta cancelada lança exceção e não altera nada")]
    public async Task DarBaixaParcial_ContaCancelada_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 100m);
        conta.Status = "Cancelado";
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            var act = async () => await service.DarBaixaParcialAsync(conta.Id, 40m);
            await act.Should().ThrowAsync<InvalidOperationException>();

            var inalterada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            inalterada.ValorRecebido.Should().Be(0m);
        }
    }

    [Fact(DisplayName = "Baixa parcial em conta inexistente lança exceção")]
    public async Task DarBaixaParcial_ContaInexistente_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx) = Build(tenantId);
        using (ctx)
        {
            var act = async () => await service.DarBaixaParcialAsync(Guid.NewGuid(), 10m);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    // ── DarBaixaTotalAsync ──────────────────────────────────────────────

    [Fact(DisplayName = "Baixa total marca Pago com ValorRecebido = ValorTotal")]
    public async Task DarBaixaTotal_MarcaPagoComValorTotal()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 150m, valorRecebido: 30m);
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            await service.DarBaixaTotalAsync(conta.Id);

            var atualizada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            atualizada.ValorRecebido.Should().Be(150m);
            atualizada.Status.Should().Be("Pago");
        }
    }

    [Fact(DisplayName = "Baixa total em conta inexistente lança KeyNotFoundException")]
    public async Task DarBaixaTotal_ContaInexistente_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx) = Build(tenantId);
        using (ctx)
        {
            var act = async () => await service.DarBaixaTotalAsync(Guid.NewGuid());
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }
    }

    // ── CancelarAsync ────────────────────────────────────────────────────

    [Fact(DisplayName = "Cancela conta pendente normalmente, registra motivo")]
    public async Task Cancelar_ContaPendente_Cancela()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 100m);
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            await service.CancelarAsync(conta.Id, "Cliente desistiu");

            var atualizada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            atualizada.Status.Should().Be("Cancelado");
            atualizada.MotivoCancelamento.Should().Be("Cliente desistiu");
        }
    }

    [Fact(DisplayName = "CRÍTICO: não permite cancelar conta já paga")]
    public async Task Cancelar_ContaJaPaga_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 100m, valorRecebido: 100m);
        conta.Status = "Pago";
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            var act = async () => await service.CancelarAsync(conta.Id, "tentativa indevida");
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*já paga*");

            var inalterada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            inalterada.Status.Should().Be("Pago", "cancelamento não pode reverter uma conta já paga");
        }
    }

    [Fact(DisplayName = "Cancelar conta inexistente lança KeyNotFoundException")]
    public async Task Cancelar_ContaInexistente_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx) = Build(tenantId);
        using (ctx)
        {
            var act = async () => await service.CancelarAsync(Guid.NewGuid(), "motivo");
            await act.Should().ThrowAsync<KeyNotFoundException>();
        }
    }

    // ── DarDescontoAsync ─────────────────────────────────────────────────

    [Fact(DisplayName = "Desconto dentro do saldo é aplicado normalmente")]
    public async Task DarDesconto_DentroDoSaldo_Aplica()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 100m);
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            await service.DarDescontoAsync(conta.Id, 20m, "fidelidade");

            var atualizada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            atualizada.ValorDesconto.Should().Be(20m);
            atualizada.Status.Should().Be("Pendente");
        }
    }

    [Fact(DisplayName = "CRÍTICO: desconto maior que o saldo devido é rejeitado")]
    public async Task DarDesconto_MaiorQueOSaldo_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 100m, valorRecebido: 70m); // saldo real = 30
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            var act = async () => await service.DarDescontoAsync(conta.Id, 50m, "tentativa indevida");
            await act.Should().ThrowAsync<InvalidOperationException>();

            var inalterada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            inalterada.ValorDesconto.Should().Be(0m);
        }
    }

    [Fact(DisplayName = "Desconto que fecha o saldo exatamente marca Pago")]
    public async Task DarDesconto_FechaOSaldoExato_MarcaPago()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var conta = ContaPendente(tenantId, cliente.Id, valorTotal: 100m, valorRecebido: 80m); // saldo = 20
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.Add(conta); });
        using (ctx)
        {
            await service.DarDescontoAsync(conta.Id, 20m, "quitação por desconto");

            var atualizada = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == conta.Id);
            atualizada.Status.Should().Be("Pago");
        }
    }

    // ── DarBaixaEmLoteAsync ──────────────────────────────────────────────

    [Fact(DisplayName = "Baixa em lote: pagamento cobrindo o total de duas contas quita as duas")]
    public async Task DarBaixaEmLote_PagamentoCobreTudo_QuitaAsDuas()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var contaA = ContaPendente(tenantId, cliente.Id, valorTotal: 50m);
        var contaB = ContaPendente(tenantId, cliente.Id, valorTotal: 30m);
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.AddRange(contaA, contaB); });
        using (ctx)
        {
            await service.DarBaixaEmLoteAsync(new[] { contaA.Id, contaB.Id }, valorAPagar: 80m, valorDesconto: 0m, formaPagamento: "Dinheiro");

            var a = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == contaA.Id);
            var b = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == contaB.Id);
            a.Status.Should().Be("Pago");
            b.Status.Should().Be("Pago");
        }
    }

    [Fact(DisplayName = "Baixa em lote: desconto é rateado proporcionalmente ao saldo de cada conta")]
    public async Task DarBaixaEmLote_RateiaODescontoProporcionalmente()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        // ContaA (75% do total) deve receber 75% do desconto, ContaB (25%) os outros 25%.
        var contaA = ContaPendente(tenantId, cliente.Id, valorTotal: 75m);
        var contaB = ContaPendente(tenantId, cliente.Id, valorTotal: 25m);
        var (service, ctx) = Build(tenantId, c => { c.Customers.Add(cliente); c.ContasReceber.AddRange(contaA, contaB); });
        using (ctx)
        {
            await service.DarBaixaEmLoteAsync(new[] { contaA.Id, contaB.Id }, valorAPagar: 0m, valorDesconto: 20m, formaPagamento: "Dinheiro");

            var a = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == contaA.Id);
            var b = await ctx.ContasReceber.AsNoTracking().FirstAsync(c => c.Id == contaB.Id);
            a.ValorDesconto.Should().Be(15m); // 75% de 20
            b.ValorDesconto.Should().Be(5m);  // 25% de 20
        }
    }

    [Fact(DisplayName = "Baixa em lote com lista vazia não faz nada, não lança exceção")]
    public async Task DarBaixaEmLote_ListaVazia_NaoFazNada()
    {
        var tenantId = Guid.NewGuid();
        var (service, ctx) = Build(tenantId);
        using (ctx)
        {
            var act = async () => await service.DarBaixaEmLoteAsync(Array.Empty<Guid>(), 100m, 0m, "Dinheiro");
            await act.Should().NotThrowAsync();
        }
    }

    // ── GerarParcelasAsync ───────────────────────────────────────────────

    [Fact(DisplayName = "Gerar parcelas: resto da divisão vai pra última parcela (não perde centavos)")]
    public async Task GerarParcelas_RestoDaDivisao_VaiProUltimaParcela()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var (service, ctx) = Build(tenantId, c => c.Customers.Add(cliente));
        using (ctx)
        {
            var dto = new GerarParcelasDto
            {
                CustomerId       = cliente.Id,
                ValorTotal       = 100m,
                NumeroParcelas   = 3, // 100/3 = 33,33... — resto de 0,01
                PrimeiroVencimento = DateTime.Today.AddDays(30),
                IntervalosDias   = 30,
                FormaPagamento   = "Cartão"
            };

            var parcelas = (await service.GerarParcelasAsync(dto)).OrderBy(p => p.NumeroParcela).ToList();

            parcelas.Should().HaveCount(3);
            parcelas[0].ValorTotal.Should().Be(33.33m);
            parcelas[1].ValorTotal.Should().Be(33.33m);
            parcelas[2].ValorTotal.Should().Be(33.34m); // pegou o centavo que sobrou
            parcelas.Sum(p => p.ValorTotal).Should().Be(100m, "a soma das parcelas nunca pode divergir do total");
        }
    }

    [Fact(DisplayName = "Gerar parcelas com número inválido (0 ou negativo) lança ArgumentException")]
    public async Task GerarParcelas_NumeroInvalido_LancaExcecao()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var (service, ctx) = Build(tenantId, c => c.Customers.Add(cliente));
        using (ctx)
        {
            var dto = new GerarParcelasDto { CustomerId = cliente.Id, ValorTotal = 100m, NumeroParcelas = 0, PrimeiroVencimento = DateTime.Today, IntervalosDias = 30 };
            var act = async () => await service.GerarParcelasAsync(dto);
            await act.Should().ThrowAsync<ArgumentException>();
        }
    }

    // ── GerarContaAPrazoAsync ────────────────────────────────────────────

    [Fact(DisplayName = "Gerar conta a prazo cria com vencimento em 30 dias e status Pendente")]
    public async Task GerarContaAPrazo_CriaComVencimentoEm30Dias()
    {
        var tenantId = Guid.NewGuid();
        var cliente = NovoCliente(tenantId);
        var (service, ctx) = Build(tenantId, c => c.Customers.Add(cliente));
        using (ctx)
        {
            var vendaId = Guid.NewGuid();

            await service.GerarContaAPrazoAsync(cliente.Id, vendaId, 49.89m, "Repasse marketplace");

            var conta = await ctx.ContasReceber.AsNoTracking().SingleAsync(c => c.CustomerId == cliente.Id);
            conta.ValorTotal.Should().Be(49.89m);
            conta.SaleId.Should().Be(vendaId);
            conta.Status.Should().Be("Pendente");
            conta.DataVencimento.Should().BeCloseTo(DateTime.Now.AddDays(30), TimeSpan.FromMinutes(1));
        }
    }
}