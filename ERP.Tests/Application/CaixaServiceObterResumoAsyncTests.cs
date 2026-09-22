// ERP.Tests/Application/CaixaServiceObterResumoAsyncTests.cs
using ERP.Application.Interfaces;
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Application;

/// <summary>
/// Fase C, módulo Caixa — a agregação por tipo de movimento (o "Resumo de
/// Caixa") saiu do WPF e foi pra CaixaService.ObterResumoAsync. Essa lógica
/// já teve pelo menos 2 bugs reais em produção (PagamentoDespesa nunca
/// descontava do total em espécie; CancelamentoVenda não era reconhecido,
/// o dinheiro "continuava no caixa" mesmo com a venda cancelada) — daí o
/// cuidado extra de testar CADA branch de TipoMovimentoCaixa isoladamente,
/// não só o caminho feliz.
/// </summary>
public class CaixaServiceObterResumoAsyncTests
{
    private static (CaixaService svc, Mock<ICaixaRepository> repo) Build(Caixa? caixaRetornado)
    {
        var repo = new Mock<ICaixaRepository>();
        repo.Setup(r => r.ObterCaixaPorDataEUsuarioAsync(It.IsAny<DateTime>(), It.IsAny<Guid>()))
            .ReturnsAsync(caixaRetornado);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Caixas).Returns(repo.Object);

        return (new CaixaService(uow.Object), repo);
    }

    private static CaixaMovimento Mov(TipoMovimentoCaixa tipo, decimal valor, PaymentMethod? forma = null, string descricao = "")
        => new() { Id = Guid.NewGuid(), Tipo = tipo, Valor = valor, FormaPagamento = forma, Descricao = descricao, DataHora = DateTime.Today };

    [Fact(DisplayName = "Nenhum caixa encontrado pra data → retorna null")]
    public async Task SemCaixa_RetornaNull()
    {
        var (svc, _) = Build(null);
        (await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today)).Should().BeNull();
    }

    [Fact(DisplayName = "Abertura soma em SaldoInicial")]
    public async Task Abertura_SomaSaldoInicial()
    {
        var caixa = new Caixa { Movimentos = new List<CaixaMovimento> { Mov(TipoMovimentoCaixa.Abertura, 100m) } };
        var (svc, _) = Build(caixa);

        var r = await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today);
        r!.SaldoInicial.Should().Be(100m);
        r.Extrato.Should().ContainSingle(l => l.Contains("ABERTURA"));
    }

    [Theory(DisplayName = "Venda em cada forma de pagamento cai no total certo")]
    [InlineData(PaymentMethod.Dinheiro)]
    [InlineData(PaymentMethod.Pix)]
    [InlineData(PaymentMethod.CartaoDebito)]
    [InlineData(PaymentMethod.CartaoCredito)]
    [InlineData(PaymentMethod.Haver)]
    public async Task Venda_CaiNoTotalCertoPorFormaDePagamento(PaymentMethod forma)
    {
        var caixa = new Caixa { Movimentos = new List<CaixaMovimento> { Mov(TipoMovimentoCaixa.Venda, 50m, forma) } };
        var (svc, _) = Build(caixa);
        var r = await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today);

        var total = forma switch
        {
            PaymentMethod.Dinheiro      => r!.VendasDinheiro,
            PaymentMethod.Pix           => r!.VendasPix,
            PaymentMethod.CartaoDebito  => r!.VendasCartaoDebito,
            PaymentMethod.CartaoCredito => r!.VendasCartaoCredito,
            PaymentMethod.Haver         => r!.VendasHaver,
            _ => throw new NotSupportedException()
        };
        total.Should().Be(50m);
    }

    [Fact(DisplayName = "Suprimento soma em Suprimentos")]
    public async Task Suprimento_SomaSuprimentos()
    {
        var caixa = new Caixa { Movimentos = new List<CaixaMovimento> { Mov(TipoMovimentoCaixa.Suprimento, 200m) } };
        var (svc, _) = Build(caixa);
        (await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today))!.Suprimentos.Should().Be(200m);
    }

    [Fact(DisplayName = "Sangria normal (sem 'estorno' na descrição) soma em Sangrias, só se for Dinheiro")]
    public async Task SangriaNormal_SomaSangrias()
    {
        var caixa = new Caixa { Movimentos = new List<CaixaMovimento> { Mov(TipoMovimentoCaixa.Sangria, 30m, PaymentMethod.Dinheiro, "Sangria de rotina") } };
        var (svc, _) = Build(caixa);
        (await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today))!.Sangrias.Should().Be(30m);
    }

    [Fact(DisplayName = "Sangria com 'estorno' na descrição desconta da venda, não soma em Sangrias")]
    public async Task SangriaComEstornoNaDescricao_DescontaDaVenda_NaoSomaSangrias()
    {
        var caixa = new Caixa
        {
            Movimentos = new List<CaixaMovimento>
            {
                Mov(TipoMovimentoCaixa.Venda, 100m, PaymentMethod.Pix),
                Mov(TipoMovimentoCaixa.Sangria, 100m, PaymentMethod.Pix, "Estorno de venda cancelada"),
            }
        };
        var (svc, _) = Build(caixa);
        var r = await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today);

        r!.Sangrias.Should().Be(0m, "estorno via Sangria não é sangria de verdade, não deve contar como retirada");
        r.VendasPix.Should().Be(0m, "100 de venda − 100 de estorno = 0");
    }

    [Fact(DisplayName = "PagamentoDespesa (S17) — desconta de Despesas usando valor absoluto, mesmo vindo negativo")]
    public async Task PagamentoDespesa_DescontaComValorAbsoluto()
    {
        var caixa = new Caixa { Movimentos = new List<CaixaMovimento> { Mov(TipoMovimentoCaixa.PagamentoDespesa, -80m, PaymentMethod.Dinheiro, "Conta de luz") } };
        var (svc, _) = Build(caixa);
        var r = await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today);

        r!.Despesas.Should().Be(80m);
        r.Extrato.Should().ContainSingle(l => l.Contains("Conta de luz") && l.Contains("80,00"));
    }

    [Fact(DisplayName = "CancelamentoVenda — desconta da venda usando +=, porque o valor já vem negativo")]
    public async Task CancelamentoVenda_DescontaDaVendaCorretamente()
    {
        var caixa = new Caixa
        {
            Movimentos = new List<CaixaMovimento>
            {
                Mov(TipoMovimentoCaixa.Venda, 150m, PaymentMethod.CartaoCredito),
                Mov(TipoMovimentoCaixa.CancelamentoVenda, -150m, PaymentMethod.CartaoCredito, "Cancelamento"),
            }
        };
        var (svc, _) = Build(caixa);
        var r = await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today);

        r!.VendasCartaoCredito.Should().Be(0m,
            "venda de 150 cancelada precisa voltar a 0 — este é exatamente o bug real que existia " +
            "(o dinheiro 'continuava no caixa' porque CancelamentoVenda não tinha branch próprio)");
    }

    [Fact(DisplayName = "Tipo de movimento desconhecido/futuro ainda aparece no extrato (rede de segurança)")]
    public async Task TipoDesconhecido_AindaApareceNoExtrato()
    {
        var caixa = new Caixa
        {
            Movimentos = new List<CaixaMovimento>
            {
                new() { Id = Guid.NewGuid(), Tipo = TipoMovimentoCaixa.Fechamento, Valor = 0, Descricao = "FECHAMENTO DE CAIXA", DataHora = DateTime.Today }
            }
        };
        var (svc, _) = Build(caixa);
        var r = await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today);

        r!.Extrato.Should().ContainSingle(l => l.Contains("FECHAMENTO DE CAIXA"),
            "nenhum tipo de movimento deve sumir silenciosamente do extrato — era exatamente esse " +
            "o defeito que causou o bug do PagamentoDespesa antes de ganhar um branch próprio");
    }

    [Fact(DisplayName = "Movimentos são processados em ordem cronológica, não na ordem em que estão na lista")]
    public async Task ProcessaEmOrdemCronologica()
    {
        var tarde = Mov(TipoMovimentoCaixa.Venda, 10m, PaymentMethod.Dinheiro);
        tarde.DataHora = DateTime.Today.AddHours(15);
        var cedo = Mov(TipoMovimentoCaixa.Venda, 20m, PaymentMethod.Dinheiro);
        cedo.DataHora = DateTime.Today.AddHours(8);

        // lista fora de ordem de propósito
        var caixa = new Caixa { Movimentos = new List<CaixaMovimento> { tarde, cedo } };
        var (svc, _) = Build(caixa);
        var r = await svc.ObterResumoAsync(Guid.NewGuid(), DateTime.Today);

        r!.VendasDinheiro.Should().Be(30m); // soma é a mesma independente da ordem, mas confirma que ambos entram
        r.Extrato.Should().HaveCount(2);
        r.Extrato[0].Should().Contain("+ R$ 20,00", "o movimento das 8h deve aparecer primeiro no extrato");
    }
}
