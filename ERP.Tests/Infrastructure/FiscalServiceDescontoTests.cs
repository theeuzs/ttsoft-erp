// ERP.Tests/Infrastructure/FiscalServiceDescontoTests.cs
using ERP.Domain.Entities;
using ERP.Infrastructure.Services;
using FluentAssertions;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ERP.Tests.Infrastructure;

/// <summary>
/// O desconto do fechamento (Sale.DiscountAmount) não era enviado à Focus:
/// a nota saía pelo valor cheio. Agora é rateado por item em valor_desconto.
/// </summary>
public class FiscalServiceDescontoTests
{
    private static Sale Venda(decimal desconto, params decimal[] totais) => new()
    {
        DiscountAmount = desconto,
        Items = totais.Select(t => new SaleItem { Quantity = 1, UnitPrice = t, TotalItem = t }).ToList()
    };

    [Fact(DisplayName = "Sem desconto: nenhum item recebe valor_desconto")]
    public void SemDesconto_Zeros()
        => FiscalService.RatearDescontoDaVenda(Venda(0m, 10m, 20m)).Should().OnlyContain(d => d == 0m);

    [Fact(DisplayName = "Desconto é rateado proporcionalmente e a soma bate exata")]
    public void Proporcional_SomaExata()
    {
        var r = FiscalService.RatearDescontoDaVenda(Venda(10m, 100m, 300m));
        r[0].Should().Be(2.50m);
        r[1].Should().Be(7.50m);
        r.Sum().Should().Be(10m);
    }

    [Fact(DisplayName = "Dízima: último item absorve o centavo restante")]
    public void Dizima_UltimoAbsorve()
    {
        var r = FiscalService.RatearDescontoDaVenda(Venda(10m, 10m, 10m, 10m));
        r.Sum().Should().Be(10m);
        r[0].Should().Be(3.33m);
        r[2].Should().Be(3.34m);
    }

    [Fact(DisplayName = "Desconto maior ou igual ao total não é enviado")]
    public void DescontoInvalido_Ignorado()
    {
        FiscalService.RatearDescontoDaVenda(Venda(30m, 10m, 20m)).Should().OnlyContain(d => d == 0m);
        FiscalService.RatearDescontoDaVenda(Venda(50m, 10m, 20m)).Should().OnlyContain(d => d == 0m);
    }
}
