// ── ERP.Tests/Application/ExtratoFinanceiroServiceTests.cs ────────────────────
using ERP.Application.Services;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ERP.Tests.Application;

/// <summary>
/// S17: trava o bug real encontrado no teste manual — um Recebível de
/// Operadora, depois de liquidado, virava "Entrada" no Extrato e era somado
/// JUNTO com a Liquidação (o MovimentoContaBancaria real), contando o mesmo
/// dinheiro duas vezes no total. Recebível é sempre informativo: nunca deve
/// contar como Entrada/Saída real, liquidado ou não.
/// </summary>
public class ExtratoFinanceiroServiceTests
{
    private readonly Mock<ICaixaRepository>             _caixaRepoMock;
    private readonly Mock<IContaBancariaRepository>     _contaBancariaRepoMock;
    private readonly Mock<IRecebivelOperadoraRepository> _recebivelRepoMock;
    private readonly Mock<IUnitOfWork>                  _unitOfWorkMock;
    private readonly ExtratoFinanceiroService           _service;

    public ExtratoFinanceiroServiceTests()
    {
        _caixaRepoMock         = new Mock<ICaixaRepository>();
        _contaBancariaRepoMock = new Mock<IContaBancariaRepository>();
        _recebivelRepoMock     = new Mock<IRecebivelOperadoraRepository>();
        _unitOfWorkMock        = new Mock<IUnitOfWork>();

        _unitOfWorkMock.Setup(u => u.Caixas).Returns(_caixaRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.ContasBancarias).Returns(_contaBancariaRepoMock.Object);
        _unitOfWorkMock.Setup(u => u.RecebiveisOperadora).Returns(_recebivelRepoMock.Object);

        _caixaRepoMock
            .Setup(r => r.GetMovimentosPorPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<CaixaMovimento>());
        _contaBancariaRepoMock
            .Setup(r => r.GetMovimentosPorPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<MovimentoContaBancaria>());

        _service = new ExtratoFinanceiroService(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task ObterExtratoAsync_RecebivelLiquidado_NuncaClassificaComoEntrada()
    {
        // Esse é exatamente o cenário do bug real: recebível já Liquidado.
        var recebivel = new RecebivelOperadora
        {
            Id                     = Guid.NewGuid(),
            OperadoraRecebimentoId = Guid.NewGuid(),
            OperadoraRecebimento   = new OperadoraRecebimento { Nome = "StoneTeste" },
            FormaRecebimento       = FormaRecebimentoOperadora.CreditoVista,
            ValorBruto             = 200m,
            ValorTaxa              = 6m,
            ValorLiquido           = 194m,
            DataVenda              = DateTime.Today,
            Status                 = StatusRecebivel.Liquidado,
            DataLiquidacao         = DateTime.Today
        };

        _recebivelRepoMock
            .Setup(r => r.GetPorPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecebivelOperadora> { recebivel });

        var itens = await _service.ObterExtratoAsync(DateTime.Today.AddDays(-1), DateTime.Today);

        itens.Should().ContainSingle();
        itens[0].Tipo.Should().NotBe("Entrada"); // a trava do bug — nunca pode virar isso
        itens[0].Tipo.Should().Be("Recebível Liquidado");
    }

    [Fact]
    public async Task ObterExtratoAsync_RecebivelPendente_ClassificaComoRecebivelPendente()
    {
        var recebivel = new RecebivelOperadora
        {
            Id                     = Guid.NewGuid(),
            OperadoraRecebimentoId = Guid.NewGuid(),
            OperadoraRecebimento   = new OperadoraRecebimento { Nome = "StoneTeste" },
            FormaRecebimento       = FormaRecebimentoOperadora.Debito,
            ValorBruto             = 80m,
            ValorTaxa              = 1.20m,
            ValorLiquido           = 78.80m,
            DataVenda              = DateTime.Today,
            Status                 = StatusRecebivel.Pendente
        };

        _recebivelRepoMock
            .Setup(r => r.GetPorPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecebivelOperadora> { recebivel });

        var itens = await _service.ObterExtratoAsync(DateTime.Today.AddDays(-1), DateTime.Today);

        itens.Should().ContainSingle();
        itens[0].Tipo.Should().Be("Recebível Pendente");
    }
}
