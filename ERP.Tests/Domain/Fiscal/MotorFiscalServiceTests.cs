using System;
using Xunit;
using FluentAssertions;
using ERP.Application.Services;
using ERP.Domain.Entities;

namespace ERP.Tests.Application.Services // Ou o namespace que preferir
{
    public class MotorFiscalServiceTests
    {
        private readonly MotorFiscalService _motorFiscal;

        public MotorFiscalServiceTests()
        {
            // Como é um serviço puro de matemática, não precisamos de Mocks!
            _motorFiscal = new MotorFiscalService();
        }

        [Fact(DisplayName = "CalcularTributosVenda - ICMS: Deve calcular 18% sobre o valor bruto")]
        [Trait("Categoria", "Fiscal - Motor de Impostos")]
        public void CalcularTributosVenda_IcmsPadrao_DeveCalcularCorretamente()
        {
            // Arrange
            var produtoFake = new Product { Name = "Tinta Suvinil", IcmsPercent = 18m, IpiPercent = 0m };
            decimal quantidade = 2m;
            decimal valorUnitario = 50.00m; // Total = 100,00

            // Act
            var resultado = _motorFiscal.CalcularTributosVenda(produtoFake, quantidade, valorUnitario);

            // Assert
            resultado.ValorProduto.Should().Be(100.00m);
            resultado.ValorIcms.Should().Be(18.00m); // 18% de 100
            
            // Pela sua fórmula atual: BaseBruta + IPI + ICMS_ST + ICMS
            resultado.ValorTotalItem.Should().Be(118.00m); 
        }

        [Fact(DisplayName = "CalcularTributosVenda - IPI: Deve calcular o imposto de fábrica e somar ao total")]
        [Trait("Categoria", "Fiscal - Motor de Impostos")]
        public void CalcularTributosVenda_ComIpi_DeveCalcularESomarAoTotal()
        {
            // Arrange
            var produtoFake = new Product { Name = "Cimento", IcmsPercent = 0m, IpiPercent = 5m };
            decimal quantidade = 10m;
            decimal valorUnitario = 20.00m; // Total = 200,00

            // Act
            var resultado = _motorFiscal.CalcularTributosVenda(produtoFake, quantidade, valorUnitario);

            // Assert
            resultado.ValorIpi.Should().Be(10.00m); // 5% de 200
            resultado.ValorTotalItem.Should().Be(210.00m); // 200 + 10 de IPI
        }

        [Fact(DisplayName = "CalcularTributosVenda - Descontos e Fretes: Devem impactar a base de cálculo dos impostos")]
        [Trait("Categoria", "Fiscal - Motor de Impostos")]
        public void CalcularTributosVenda_ComFreteEDesconto_DeveAjustarBaseDeCalculo()
        {
            // Arrange
            var produtoFake = new Product { Name = "Piso Porcelanato", IcmsPercent = 10m, IpiPercent = 0m };
            decimal quantidade = 1m;
            decimal valorUnitario = 100.00m;
            decimal desconto = 20.00m; 
            decimal frete = 30.00m; 
            // Matemática: 100 + 30 (Frete) - 20 (Desconto) = Base Bruta 110,00

            // Act
            var resultado = _motorFiscal.CalcularTributosVenda(produtoFake, quantidade, valorUnitario, desconto, frete);

            // Assert
            resultado.BaseCalculoIcms.Should().Be(110.00m);
            resultado.ValorIcms.Should().Be(11.00m); // 10% de 110
        }

        [Fact(DisplayName = "CalcularTributosVenda - ICMS-ST: MVA zerada não deve gerar imposto de substituição por enquanto")]
        [Trait("Categoria", "Fiscal - Motor de Impostos")]
        public void CalcularTributosVenda_SemMva_NaoDeveCalcularST()
        {
            // Arrange
            var produtoFake = new Product { Name = "Fio de Cobre", IcmsPercent = 18m, IpiPercent = 0m };
            
            // Act
            var resultado = _motorFiscal.CalcularTributosVenda(produtoFake, 1m, 1000m);

            // Assert
            // Como no seu código a MVA está cravada em 0 para a Fase 2, a ST deve ser 0.
            resultado.MargemValorAgregado.Should().Be(0m);
            resultado.ValorIcmsSt.Should().Be(0m);
        }
    }
}