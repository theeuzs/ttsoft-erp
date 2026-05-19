using ERP.Domain.Services.Fiscal;
using ERP.Application.DTOs;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Application
{
    // ── Testes do Motor ICMS-ST ────────────────────────────────────────────────

    [Trait("Categoria", "Fiscal")]
    public class ICMSSTCalculatorTests
    {
        private readonly ICMSSTCalculator _calc = new();

        [Fact(DisplayName = "Operação interna: ICMS-ST com MVA original")]
        public void CalcularST_OperacaoInterna_UsaMVAOriginal()
        {
            // Produto: R$100, alíquota interna 18%, MVA 40% — operação interna (PR→PR)
            var result = _calc.Calcular(
                valorProduto:          100m,
                aliquotaInternaUFDest: 18m,
                mvaOriginal:           40m,
                aliquotaInterestadual: 0m,
                aliquotaIcmsOrigem:    0m);

            result.IsInterestadual.Should().BeFalse();
            result.MVAUtilizado.Should().Be(40m);
            result.BaseCalculoST.Should().Be(140m);  // 100 * (1 + 0.40)
            result.ValorICMSST.Should().Be(25.2m);   // 140 * 0.18
        }

        [Fact(DisplayName = "Operação interestadual: MVA ajustado é calculado")]
        public void CalcularST_OperacaoInterestadual_AjustaMVA()
        {
            // SP → PR: alíquota interestadual 12%, interna PR 19.5%, MVA 40%
            var result = _calc.Calcular(
                valorProduto:          100m,
                aliquotaInternaUFDest: 19.5m,
                mvaOriginal:           40m,
                aliquotaInterestadual: 12m,
                aliquotaIcmsOrigem:    0m);

            result.IsInterestadual.Should().BeTrue();
            result.MVAUtilizado.Should().BeGreaterThan(40m); // MVA ajustado > original
            result.ValorICMSST.Should().BeGreaterThan(0m);
        }

        [Fact(DisplayName = "Valor ST nunca é negativo")]
        public void CalcularST_ResultadoNuncaNegativo()
        {
            // Alíquota interna baixa com ICMS próprio alto — ST deve ser zero, não negativo
            var result = _calc.Calcular(
                valorProduto:          100m,
                aliquotaInternaUFDest: 5m,
                mvaOriginal:           10m,
                aliquotaInterestadual: 12m,
                aliquotaIcmsOrigem:    12m);

            result.ValorICMSST.Should().BeGreaterThanOrEqualTo(0m);
        }
    }

    // ── Testes do Motor Fiscal Brasileiro ──────────────────────────────────────

    [Trait("Categoria", "Fiscal")]
    public class MotorFiscalBrasileiroTests
    {
        [Theory(DisplayName = "Alíquota ICMS interna por UF")]
[InlineData("SP", 18.0)]
[InlineData("PR", 19.5)]
[InlineData("MG", 18.0)]
[InlineData("RJ", 22.0)]
[InlineData("RS", 17.5)]
public void ObterAliquotaInterna_UFConhecida_RetornaAliquotaCorreta(string uf, double aliqEsperada)
{
    // O teste agora recebe um double e a gente converte para decimal aqui dentro
    var aliq = ERP.Infrastructure.Services.MotorFiscalBrasileiro.ObterAliquotaInterna(uf);
    aliq.Should().Be((decimal)aliqEsperada);
}

        [Fact(DisplayName = "UF desconhecida retorna alíquota padrão 17%")]
        public void ObterAliquotaInterna_UFDesconhecida_RetornaPadrao()
        {
            var aliq = ERP.Infrastructure.Services.MotorFiscalBrasileiro
                .ObterAliquotaInterna("XX");
            aliq.Should().Be(17m);
        }

        [Fact(DisplayName = "Operação interna: sem alíquota interestadual")]
        public void ObterAliquotaInterestadual_MesmoEstado_RetornaZero()
        {
            var aliq = ERP.Infrastructure.Services.MotorFiscalBrasileiro
                .ObterAliquotaInterestadual("SP", "SP");
            aliq.Should().Be(0m);
        }

        [Fact(DisplayName = "CFOP 5102 para operação interna de mercadoria")]
        public void ObterCFOP_OperacaoInterna_Retorna5102()
        {
            var cfop = ERP.Infrastructure.Services.MotorFiscalBrasileiro
                .ObterCFOP("PR", "PR", false);
            cfop.Should().Be("5102");
        }

        [Fact(DisplayName = "CFOP 6102 para operação interestadual de mercadoria")]
        public void ObterCFOP_OperacaoInterestadual_Retorna6102()
        {
            var cfop = ERP.Infrastructure.Services.MotorFiscalBrasileiro
                .ObterCFOP("PR", "SP", false);
            cfop.Should().Be("6102");
        }
    }

    // ── Testes de DTOs de Parcelamento ─────────────────────────────────────────

    [Trait("Categoria", "Financeiro")]
    public class ParcelamentoDtoTests
    {
        [Fact(DisplayName = "ParcelaDto: ValorRestante calculado corretamente")]
        public void ParcelaDto_ValorRestante_EhDiferenca()
        {
            var parcela = new ParcelaDto
            {
                ValorTotal    = 100m,
                ValorRecebido = 40m
            };

            parcela.ValorRestante.Should().Be(60m);
        }

        [Fact(DisplayName = "ParcelaDto: Quando quitada, ValorRestante é zero")]
        public void ParcelaDto_Quitada_ValorRestanteZero()
        {
            var parcela = new ParcelaDto
            {
                ValorTotal    = 150m,
                ValorRecebido = 150m
            };

            parcela.ValorRestante.Should().Be(0m);
        }

        [Fact(DisplayName = "GerarParcelasDto: NumeroParcelas padrão é 1")]
        public void GerarParcelasDto_PadraoUmaParcela()
        {
            var dto = new GerarParcelasDto();
            dto.NumeroParcelas.Should().Be(1);
            dto.IntervalosDias.Should().Be(30);
        }
    }

    // ── Testes de SPED ─────────────────────────────────────────────────────────

    [Trait("Categoria", "Fiscal")]
    public class SpedEfdGeneratorTests
    {
        [Fact(DisplayName = "SPED gerado contém registro 0000")]
        public void GerarBloco0_ContemRegistro0000()
        {
            var gen = new ERP.Infrastructure.Services.SpedEfdGenerator();
            gen.GerarBloco0(new ERP.Infrastructure.Services.SpedConfig
            {
                DataInicio      = new DateTime(2026, 1, 1),
                DataFim         = new DateTime(2026, 1, 31),
                RazaoSocial     = "EMPRESA TESTE LTDA",
                CNPJ            = "11222333000181",
                CodigoMunicipio = "4106902"
            });

            gen.IniciarBlocoC();
            gen.EncerrarBlocoC();
            gen.GerarBlocoH(Array.Empty<ERP.Infrastructure.Services.SpedItemInventario>(), "31012026");

            var conteudo = gen.Encerrar();

            conteudo.Should().Contain("|0000|");
            conteudo.Should().Contain("|9999|");
            conteudo.Should().Contain("EMPRESA TESTE LTDA");
            conteudo.Should().Contain("11222333000181");
        }

        [Fact(DisplayName = "SPED gerado termina com registro 9999")]
        public void Encerrar_UltimoRegistroE9999()
        {
            var gen = new ERP.Infrastructure.Services.SpedEfdGenerator();
            gen.GerarBloco0(new ERP.Infrastructure.Services.SpedConfig
            {
                DataInicio = DateTime.Today, DataFim = DateTime.Today,
                RazaoSocial = "TESTE", CNPJ = "00000000000000"
            });
            gen.IniciarBlocoC();
            gen.EncerrarBlocoC();
            gen.GerarBlocoH(Array.Empty<ERP.Infrastructure.Services.SpedItemInventario>(),
                DateTime.Today.ToString("ddMMyyyy"));

            var linhas = gen.Encerrar().Split("\r\n",
                StringSplitOptions.RemoveEmptyEntries);

            linhas.Last().Should().StartWith("|9999|");
        }
    }
}
