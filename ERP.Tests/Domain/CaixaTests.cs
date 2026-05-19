using ERP.Domain.Entities;
using ERP.Domain.Enums;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace ERP.Tests.Domain;

public class CaixaTests
{
    [Fact]
    public void CalcularSaldoAtual_DeveRetornarValorCorreto_ComVendasESangrias()
    {
        // Arrange (Preparação do cenário)
        var caixa = new Caixa
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            ValorAbertura = 100.00m, // Começou o dia com R$ 100 de troco
            Status = StatusCaixa.Aberto
        };

        // Adiciona uma VENDA de R$ 50,00 (Dinheiro entrando)
        caixa.Movimentos.Add(new CaixaMovimento
        {
            Tipo = TipoMovimentoCaixa.Venda,
            Valor = 50.00m,
            Descricao = "Venda #123"
        });

        // Adiciona uma SANGRIA de R$ 20,00 (Dinheiro saindo)
        caixa.Movimentos.Add(new CaixaMovimento
        {
            Tipo = TipoMovimentoCaixa.Sangria,
            Valor = 20.00m,
            Descricao = "Pagamento de motoboy"
        });

        // Act (Ação: Simula como o seu Service vai calcular o Saldo Final)
        var totalEntradas = caixa.Movimentos
            .Where(m => m.Tipo == TipoMovimentoCaixa.Venda || 
                        m.Tipo == TipoMovimentoCaixa.Suprimento || 
                        m.Tipo == TipoMovimentoCaixa.RecebimentoConta)
            .Sum(m => m.Valor);
            
        var totalSaidas = caixa.Movimentos
            .Where(m => m.Tipo == TipoMovimentoCaixa.Sangria || 
                        m.Tipo == TipoMovimentoCaixa.PagamentoDespesa || 
                        m.Tipo == TipoMovimentoCaixa.CancelamentoVenda)
            .Sum(m => m.Valor);

        var saldoAtual = caixa.ValorAbertura + totalEntradas - totalSaidas;

        // Assert (A verificação final que blinda a regra)
        // R$ 100 (Abertura) + R$ 50 (Venda) - R$ 20 (Sangria) TEM que ser R$ 130!
        saldoAtual.Should().Be(130.00m); 
        caixa.Movimentos.Should().HaveCount(2);
    }
}