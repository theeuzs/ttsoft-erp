// ── ERP.Tests/Application/MotorFinanceiroServiceTests.cs ──────────────────────
using ERP.Application.Interfaces;
using ERP.Application.Services;
using ERP.Domain.Enums;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Application;

/// <summary>
/// S17: escreve como teste automatizado os dois bugs reais encontrados no
/// teste manual da sessão financeira — cancelamento de PIX/Cartão quebrado
/// (tratava tudo como Sangria de dinheiro físico) e a trava de segurança pro
/// caso de recebível já liquidado. Sem isso, a próxima alteração no Motor
/// Financeiro pode reintroduzir o mesmo bug sem ninguém perceber até rodar
/// o checklist manual inteiro de novo.
/// </summary>
public class MotorFinanceiroServiceTests
{
    private readonly Mock<ICaixaService>             _caixaServiceMock;
    private readonly Mock<IContaReceberService>       _contaReceberServiceMock;
    private readonly Mock<IHaverService>              _haverServiceMock;
    private readonly Mock<IContaBancariaService>      _contaBancariaServiceMock;
    private readonly Mock<IRecebivelOperadoraService> _recebivelOperadoraServiceMock;
    private readonly MotorFinanceiroService           _motorFinanceiro;

    public MotorFinanceiroServiceTests()
    {
        _caixaServiceMock              = new Mock<ICaixaService>();
        _contaReceberServiceMock       = new Mock<IContaReceberService>();
        _haverServiceMock              = new Mock<IHaverService>();
        _contaBancariaServiceMock      = new Mock<IContaBancariaService>();
        _recebivelOperadoraServiceMock = new Mock<IRecebivelOperadoraService>();

        _motorFinanceiro = new MotorFinanceiroService(
            _caixaServiceMock.Object,
            _contaReceberServiceMock.Object,
            _haverServiceMock.Object,
            _contaBancariaServiceMock.Object,
            _recebivelOperadoraServiceMock.Object);
    }

    [Fact]
    public async Task VerificarPodeCancelarVendaAsync_DeveLancarExcecao_QuandoRecebivelJaLiquidado()
    {
        var vendaId = Guid.NewGuid();
        _recebivelOperadoraServiceMock
            .Setup(s => s.TemLiquidadoPorVendaAsync(vendaId))
            .ReturnsAsync(true);

        Func<Task> acao = async () => await _motorFinanceiro.VerificarPodeCancelarVendaAsync(vendaId);

        await acao.Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("*já está confirmado na Conta Bancária*");
    }

    [Fact]
    public async Task VerificarPodeCancelarVendaAsync_NaoLancaExcecao_QuandoSemRecebivelLiquidado()
    {
        var vendaId = Guid.NewGuid();
        _recebivelOperadoraServiceMock
            .Setup(s => s.TemLiquidadoPorVendaAsync(vendaId))
            .ReturnsAsync(false);

        Func<Task> acao = async () => await _motorFinanceiro.VerificarPodeCancelarVendaAsync(vendaId);

        await acao.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EstornarVendaAsync_Pix_DeveGerarSaidaContaBancaria_NuncaSangriaCaixa()
    {
        var vendaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)> { (Guid.NewGuid(), PaymentMethod.Pix, 150m) };

        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO VENDA TESTE", 0m, pagamentos);

        _contaBancariaServiceMock.Verify(
            s => s.RegistrarEstornoVendaAsync(vendaId, 150m, "ESTORNO VENDA TESTE", It.IsAny<Guid?>()), Times.Once);

        // O bug original fazia exatamente isso — Sangria de Caixa pra PIX.
        // Essa linha é a trava contra reintroduzir o mesmo erro.
        _caixaServiceMock.Verify(
            s => s.RegistrarMovimentoAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<PaymentMethod>(), It.IsAny<TipoMovimentoCaixa>(), It.IsAny<decimal>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task EstornarVendaAsync_CartaoPendente_DeveCancelarRecebivel_SemGerarMovimentoFinanceiro()
    {
        var vendaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)> { (Guid.NewGuid(), PaymentMethod.CartaoCredito, 200m) };

        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO VENDA TESTE", 0m, pagamentos);

        _recebivelOperadoraServiceMock.Verify(
            s => s.CancelarPendentesPorVendaAsync(vendaId), Times.Once);

        // Nenhum dinheiro chegou a se mover — cancelar recebível pendente não
        // deve tocar nem Caixa nem Conta Bancária.
        _caixaServiceMock.Verify(
            s => s.RegistrarMovimentoAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<PaymentMethod>(), It.IsAny<TipoMovimentoCaixa>(), It.IsAny<decimal>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()),
            Times.Never);
        _contaBancariaServiceMock.Verify(
            s => s.RegistrarEstornoVendaAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task EstornarVendaAsync_Dinheiro_DeveDescontarTroco_AntesDaSangria()
    {
        var vendaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)> { (Guid.NewGuid(), PaymentMethod.Dinheiro, 100m) };

        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO VENDA TESTE", 20m, pagamentos);

        _caixaServiceMock.Verify(
            s => s.RegistrarMovimentoAsync(
                usuarioId, 80m, "ESTORNO VENDA TESTE", PaymentMethod.Dinheiro, TipoMovimentoCaixa.Sangria, It.IsAny<decimal>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task EstornarVendaAsync_Haver_NaoDeveChamarNenhumServicoFinanceiro()
    {
        // Haver já é revertido dentro do CancelAsync da própria venda (saldo do
        // cliente) — o Motor Financeiro não deve tocar em nada pra essa forma.
        var vendaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)> { (Guid.NewGuid(), PaymentMethod.Haver, 50m) };

        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO VENDA TESTE", 0m, pagamentos);

        _caixaServiceMock.Verify(
            s => s.RegistrarMovimentoAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<PaymentMethod>(), It.IsAny<TipoMovimentoCaixa>(), It.IsAny<decimal>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()),
            Times.Never);
        _contaBancariaServiceMock.Verify(
            s => s.RegistrarEstornoVendaAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<Guid?>()),
            Times.Never);
        _recebivelOperadoraServiceMock.Verify(
            s => s.CancelarPendentesPorVendaAsync(It.IsAny<Guid>()), Times.Never);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Idempotência financeira granular (achado de auditoria pré-Fase-2 do
    // Offline-First, 08/2026) — os 8 cenários pedidos antes de conectar o
    // Sync Engine ao financeiro. Confirmado contra dado real de produção:
    // uma venda pode ter várias linhas de pagamento legítimas (2 cartões,
    // débito+crédito, PIX+dinheiro) — idempotência tem que ser por
    // SalePaymentId, nunca por VendaId inteira.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ProcessarRecebimento_UmPagamento_GeraUmLancamento()
    {
        var vendaId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)>
        {
            (salePaymentId, PaymentMethod.Dinheiro, 100m)
        };

        await _motorFinanceiro.ProcessarRecebimentoVendaAsync(
            vendaId, Guid.NewGuid(), null, "Consumidor", "Vendedor", "Operador", 0m, pagamentos);

        _caixaServiceMock.Verify(s => s.RegistrarMovimentoAsync(
            It.IsAny<Guid>(), 100m, It.IsAny<string>(), PaymentMethod.Dinheiro, TipoMovimentoCaixa.Venda,
            It.IsAny<decimal>(), vendaId, salePaymentId), Times.Once);
    }

    [Fact]
    public async Task ProcessarRecebimento_DoisCartoes_GeraDoisRecebiveisIndependentes()
    {
        var vendaId = Guid.NewGuid();
        var salePaymentA = Guid.NewGuid();
        var salePaymentB = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)>
        {
            (salePaymentA, PaymentMethod.CartaoDebito, 100m),
            (salePaymentB, PaymentMethod.CartaoCredito, 50m)
        };

        await _motorFinanceiro.ProcessarRecebimentoVendaAsync(
            vendaId, Guid.NewGuid(), null, "Consumidor", "Vendedor", "Operador", 0m, pagamentos);

        // Dois SalePaymentId diferentes, mesma venda — os dois têm que gerar
        // recebível próprio; nenhum é "duplicata" do outro.
        _recebivelOperadoraServiceMock.Verify(s => s.RegistrarRecebivelVendaAsync(
            vendaId, PaymentMethod.CartaoDebito, 100m, salePaymentA), Times.Once);
        _recebivelOperadoraServiceMock.Verify(s => s.RegistrarRecebivelVendaAsync(
            vendaId, PaymentMethod.CartaoCredito, 50m, salePaymentB), Times.Once);
    }

    [Fact]
    public async Task ProcessarRecebimento_LinhaJaProcessada_NaoDuplicaLancamento()
    {
        var vendaId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)>
        {
            (salePaymentId, PaymentMethod.Dinheiro, 100m)
        };

        // Simula: essa linha já foi processada antes (ex: primeira tentativa
        // do Sync Engine já rodou com sucesso).
        _caixaServiceMock.Setup(s => s.ExisteMovimentoParaSalePaymentAsync(salePaymentId)).ReturnsAsync(true);

        await _motorFinanceiro.ProcessarRecebimentoVendaAsync(
            vendaId, Guid.NewGuid(), null, "Consumidor", "Vendedor", "Operador", 0m, pagamentos);

        _caixaServiceMock.Verify(s => s.RegistrarMovimentoAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<PaymentMethod>(),
            It.IsAny<TipoMovimentoCaixa>(), It.IsAny<decimal>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task ProcessarRecebimento_Pix_GeraUmaEntradaNaContaBancaria()
    {
        var vendaId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)>
        {
            (salePaymentId, PaymentMethod.Pix, 159.90m)
        };

        await _motorFinanceiro.ProcessarRecebimentoVendaAsync(
            vendaId, Guid.NewGuid(), null, "Consumidor", "Vendedor", "Operador", 0m, pagamentos);

        _contaBancariaServiceMock.Verify(s => s.RegistrarRecebimentoVendaAsync(
            vendaId, 159.90m, It.IsAny<string>(), salePaymentId), Times.Once);
    }

    [Fact]
    public async Task PixRecebidoDepoisEstornado_GeraUmaEntradaEUmaSaida_ComMesmoSalePaymentId()
    {
        var vendaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var recebimento = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)>
        {
            (salePaymentId, PaymentMethod.Pix, 159.90m)
        };

        // 1) Recebe o PIX (entrada)
        await _motorFinanceiro.ProcessarRecebimentoVendaAsync(
            vendaId, usuarioId, null, "Consumidor", "Vendedor", "Operador", 0m, recebimento);

        // 2) Venda é cancelada depois — estorna o mesmo pagamento (saída)
        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO", 0m, recebimento);

        _contaBancariaServiceMock.Verify(s => s.RegistrarRecebimentoVendaAsync(
            vendaId, 159.90m, It.IsAny<string>(), salePaymentId), Times.Once);
        _contaBancariaServiceMock.Verify(s => s.RegistrarEstornoVendaAsync(
            vendaId, 159.90m, "ESTORNO", salePaymentId), Times.Once);
    }

    [Fact]
    public async Task ProcessarRecebimento_MultiplasFormas_CadaSalePaymentProcessadoIndependente()
    {
        var vendaId = Guid.NewGuid();
        var salePaymentDinheiro = Guid.NewGuid();
        var salePaymentPix      = Guid.NewGuid();
        var salePaymentCartao   = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)>
        {
            (salePaymentDinheiro, PaymentMethod.Dinheiro, 30m),
            (salePaymentPix,      PaymentMethod.Pix,      40m),
            (salePaymentCartao,   PaymentMethod.CartaoCredito, 30m)
        };

        await _motorFinanceiro.ProcessarRecebimentoVendaAsync(
            vendaId, Guid.NewGuid(), null, "Consumidor", "Vendedor", "Operador", 0m, pagamentos);

        _caixaServiceMock.Verify(s => s.RegistrarMovimentoAsync(
            It.IsAny<Guid>(), 30m, It.IsAny<string>(), PaymentMethod.Dinheiro, TipoMovimentoCaixa.Venda,
            It.IsAny<decimal>(), vendaId, salePaymentDinheiro), Times.Once);
        _contaBancariaServiceMock.Verify(s => s.RegistrarRecebimentoVendaAsync(
            vendaId, 40m, It.IsAny<string>(), salePaymentPix), Times.Once);
        _recebivelOperadoraServiceMock.Verify(s => s.RegistrarRecebivelVendaAsync(
            vendaId, PaymentMethod.CartaoCredito, 30m, salePaymentCartao), Times.Once);
    }

    [Fact]
    public async Task SincronizacaoOfflineRepetida_NaoDuplicaLancamentoFinanceiro()
    {
        // Simula exatamente o cenário perigoso do Offline-First (§17 do
        // OFFLINE_FIRST_ARCHITECTURE.md): API grava, resposta se perde, Sync
        // Engine tenta de novo. A venda em si já é protegida (idempotência do
        // SaleService.CreateAsync) — esse teste confirma que o PROCESSAMENTO
        // FINANCEIRO da mesma linha, chamado duas vezes, também não duplica.
        var vendaId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)>
        {
            (salePaymentId, PaymentMethod.CartaoCredito, 200m)
        };

        // Primeira tentativa: linha ainda não processada.
        _recebivelOperadoraServiceMock.SetupSequence(s => s.ExisteRecebivelParaSalePaymentAsync(salePaymentId))
            .ReturnsAsync(false)   // 1a tentativa
            .ReturnsAsync(true);   // 2a tentativa (retry) — já processou

        await _motorFinanceiro.ProcessarRecebimentoVendaAsync(
            vendaId, Guid.NewGuid(), null, "Consumidor", "Vendedor", "Operador", 0m, pagamentos);
        await _motorFinanceiro.ProcessarRecebimentoVendaAsync(
            vendaId, Guid.NewGuid(), null, "Consumidor", "Vendedor", "Operador", 0m, pagamentos);

        _recebivelOperadoraServiceMock.Verify(s => s.RegistrarRecebivelVendaAsync(
            vendaId, PaymentMethod.CartaoCredito, 200m, salePaymentId), Times.Once);
    }

    [Fact]
    public async Task EstornoRepetido_NaoDuplicaSaidaNaContaBancaria()
    {
        var vendaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var salePaymentId = Guid.NewGuid();
        var pagamentos = new List<(Guid SalePaymentId, PaymentMethod Forma, decimal Valor)>
        {
            (salePaymentId, PaymentMethod.Pix, 159.90m)
        };

        // Primeira tentativa de estorno: ainda não tinha Saída registrada.
        // Segunda tentativa (retry): já tem — não pode gerar uma segunda Saída.
        _contaBancariaServiceMock.SetupSequence(
                s => s.ExisteMovimentoParaSalePaymentAsync(salePaymentId, TipoMovimentoContaBancaria.Saida))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO", 0m, pagamentos);
        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO", 0m, pagamentos);

        _contaBancariaServiceMock.Verify(s => s.RegistrarEstornoVendaAsync(
            vendaId, 159.90m, "ESTORNO", salePaymentId), Times.Once);
    }
}