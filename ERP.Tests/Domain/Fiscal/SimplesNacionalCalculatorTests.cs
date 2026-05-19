using System;
using Xunit;
using FluentAssertions;
using ERP.Domain.Services.Fiscal;

namespace ERP.Tests.Domain.Services.Fiscal
{
    public class SimplesNacionalCalculatorTests
    {
        private readonly SimplesNacionalCalculator _calculator;

        public SimplesNacionalCalculatorTests()
        {
            _calculator = new SimplesNacionalCalculator();
        }

        [Theory(DisplayName = "CSOSN 101 e 201 - Deve calcular o valor do ICMS para aproveitamento de crédito")]
        [Trait("Categoria", "Fiscal - ICMS Simples Nacional")]
        [InlineData("101")]
        [InlineData("201")]
        public void CalcularIcms_CSOSNComPermissaoDeCredito_DeveRetornarValorCalculado(string csosn)
        {
            // Arrange
            decimal baseCalculo = 1000m;
            decimal aliquotaIcms = 2.5m; // 2,5%

            // Act
            var resultado = _calculator.CalcularIcms(baseCalculo, csosn, aliquotaIcms);

            // Assert
            resultado.Should().Be(25.00m); // 1000 * 2.5% = 25
        }

        [Theory(DisplayName = "CSOSNs sem crédito - Deve retornar zero")]
        [Trait("Categoria", "Fiscal - ICMS Simples Nacional")]
        [InlineData("102")] // Tributada sem permissão de crédito
        [InlineData("103")] // Isenção de ICMS
        [InlineData("300")] // Imune
        [InlineData("400")] // Não tributada
        [InlineData("500")] // ICMS cobrado anteriormente por ST
        [InlineData("900")] // Outros
        public void CalcularIcms_CSOSNSemPermissaoDeCredito_DeveRetornarZero(string csosn)
        {
            // Arrange
            decimal baseCalculo = 1000m;
            decimal aliquotaIcms = 2.5m;

            // Act
            var resultado = _calculator.CalcularIcms(baseCalculo, csosn, aliquotaIcms);

            // Assert
            resultado.Should().Be(0m);
        }

        [Fact(DisplayName = "Tributos Aproximados - Deve calcular corretamente usando a alíquota padrão (13.45%)")]
        [Trait("Categoria", "Fiscal - Tributos Aproximados (Lei 12.741)")]
        public void CalcularTributosAproximados_AliquotaPadrao_DeveCalcularCorretamente()
        {
            // Arrange
            decimal valorTotal = 150.50m;

            // Act
            var resultado = _calculator.CalcularTributosAproximados(valorTotal);

            // Assert
            // 150.50 * 13.45% = 20.24225. Com Math.Round(..., 2), deve arredondar para 20.24
            resultado.Should().Be(20.24m);
        }

        [Fact(DisplayName = "Tributos Aproximados - Deve calcular corretamente usando uma alíquota customizada")]
        [Trait("Categoria", "Fiscal - Tributos Aproximados (Lei 12.741)")]
        public void CalcularTributosAproximados_AliquotaCustomizada_DeveCalcularCorretamente()
        {
            // Arrange
            decimal valorTotal = 200m;
            decimal aliquotaCustomizada = 10.0m;

            // Act
            var resultado = _calculator.CalcularTributosAproximados(valorTotal, aliquotaCustomizada);

            // Assert
            // 200 * 10.0% = 20.00
            resultado.Should().Be(20.00m);
        }
    }
}