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
        var pagamentos = new List<(PaymentMethod Forma, decimal Valor)> { (PaymentMethod.Pix, 150m) };

        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO VENDA TESTE", 0m, pagamentos);

        _contaBancariaServiceMock.Verify(
            s => s.RegistrarEstornoVendaAsync(vendaId, 150m, "ESTORNO VENDA TESTE"), Times.Once);

        // O bug original fazia exatamente isso — Sangria de Caixa pra PIX.
        // Essa linha é a trava contra reintroduzir o mesmo erro.
        _caixaServiceMock.Verify(
            s => s.RegistrarMovimentoAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<PaymentMethod>(), It.IsAny<TipoMovimentoCaixa>(), It.IsAny<decimal>()),
            Times.Never);
    }

    [Fact]
    public async Task EstornarVendaAsync_CartaoPendente_DeveCancelarRecebivel_SemGerarMovimentoFinanceiro()
    {
        var vendaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var pagamentos = new List<(PaymentMethod Forma, decimal Valor)> { (PaymentMethod.CartaoCredito, 200m) };

        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO VENDA TESTE", 0m, pagamentos);

        _recebivelOperadoraServiceMock.Verify(
            s => s.CancelarPendentesPorVendaAsync(vendaId), Times.Once);

        // Nenhum dinheiro chegou a se mover — cancelar recebível pendente não
        // deve tocar nem Caixa nem Conta Bancária.
        _caixaServiceMock.Verify(
            s => s.RegistrarMovimentoAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<PaymentMethod>(), It.IsAny<TipoMovimentoCaixa>(), It.IsAny<decimal>()),
            Times.Never);
        _contaBancariaServiceMock.Verify(
            s => s.RegistrarEstornoVendaAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task EstornarVendaAsync_Dinheiro_DeveDescontarTroco_AntesDaSangria()
    {
        var vendaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var pagamentos = new List<(PaymentMethod Forma, decimal Valor)> { (PaymentMethod.Dinheiro, 100m) };

        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO VENDA TESTE", 20m, pagamentos);

        _caixaServiceMock.Verify(
            s => s.RegistrarMovimentoAsync(
                usuarioId, 80m, "ESTORNO VENDA TESTE", PaymentMethod.Dinheiro, TipoMovimentoCaixa.Sangria, It.IsAny<decimal>()),
            Times.Once);
    }

    [Fact]
    public async Task EstornarVendaAsync_Haver_NaoDeveChamarNenhumServicoFinanceiro()
    {
        // Haver já é revertido dentro do CancelAsync da própria venda (saldo do
        // cliente) — o Motor Financeiro não deve tocar em nada pra essa forma.
        var vendaId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var pagamentos = new List<(PaymentMethod Forma, decimal Valor)> { (PaymentMethod.Haver, 50m) };

        await _motorFinanceiro.EstornarVendaAsync(vendaId, usuarioId, "ESTORNO VENDA TESTE", 0m, pagamentos);

        _caixaServiceMock.Verify(
            s => s.RegistrarMovimentoAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(),
                It.IsAny<PaymentMethod>(), It.IsAny<TipoMovimentoCaixa>(), It.IsAny<decimal>()),
            Times.Never);
        _contaBancariaServiceMock.Verify(
            s => s.RegistrarEstornoVendaAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>()),
            Times.Never);
        _recebivelOperadoraServiceMock.Verify(
            s => s.CancelarPendentesPorVendaAsync(It.IsAny<Guid>()), Times.Never);
    }
}