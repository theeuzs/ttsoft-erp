using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ERP.Tests.Domain;

// S{N} FIX: Preco B automatico = Custo Final Calculado (custo original + IPI) + 25%.
public class RepassePolicyTests
{
    [Fact]
    public void Assento_Tigre_do_print_da_78_43()
    {
        // 54,56 + IPI 15% = 62,744 (Custo Final Calculado da tela) x 1,25 = 78,43
        var p = new Product { OriginalCost = 54.56m, IpiPercent = 15m, IcmsPercent = 8.03m, SalePrice = 112.90m };

        RepassePolicy.PrecoDoProduto(p).Should().Be(78.43m);
        p.GetPrecoParaGrupo(GrupoPreco.B).Should().Be(78.43m);
    }

    [Fact]
    public void Preco_B_manual_maior_que_zero_vence_o_automatico()
    {
        var p = new Product { OriginalCost = 54.56m, IpiPercent = 15m, SalePrice = 112.90m, PrecoBRevendedor = 90m };

        p.GetPrecoParaGrupo(GrupoPreco.B).Should().Be(90m);
    }

    [Fact]
    public void OriginalCost_tem_prioridade_sobre_CostPrice_desatualizado()
    {
        var p = new Product { OriginalCost = 44.38m, CostPrice = 31.97m, IpiPercent = 0m };

        RepassePolicy.PrecoDoProduto(p).Should().Be(55.48m); // 44,38 x 1,25 = 55,475 -> 55,48
    }

    [Fact]
    public void Sem_OriginalCost_usa_CostPrice_sem_somar_IPI_de_novo()
    {
        var p = new Product { OriginalCost = 0m, CostPrice = 100m, IpiPercent = 10m };

        RepassePolicy.PrecoDoProduto(p).Should().Be(125m);
    }

    [Fact]
    public void Sem_custo_nenhum_Preco_B_cai_pro_varejo_nunca_zero()
    {
        var p = new Product { OriginalCost = 0m, CostPrice = 0m, SalePrice = 50m };

        p.GetPrecoParaGrupo(GrupoPreco.B).Should().Be(50m);
    }

    [Fact]
    public void Produto_filho_sem_custo_usa_custo_do_pai_vezes_fator()
    {
        var pai   = new Product { Id = Guid.NewGuid(), OriginalCost = 10m, IpiPercent = 0m };
        var filho = new Product { ParentProductId = pai.Id, ConversionFactor = 6m, SalePrice = 99m };

        filho.GetPrecoParaGrupo(GrupoPreco.B, pai).Should().Be(75m); // 10 x 6 x 1,25
        filho.GetPrecoParaGrupo(GrupoPreco.B).Should().Be(99m);      // sem o pai: varejo
    }

    [Fact]
    public void Varejo_e_grupo_C_nao_mudam()
    {
        var p = new Product { OriginalCost = 54.56m, IpiPercent = 15m, SalePrice = 112.90m, PrecoCAtacadista = 70m };

        p.GetPrecoParaGrupo(GrupoPreco.A).Should().Be(112.90m);
        p.GetPrecoParaGrupo(GrupoPreco.C).Should().Be(70m);
    }
}

// S{N} FIX: o DTO que vai pro PDV e pra tela de produto leva o Preco B EFETIVO.
public class PrecoBAutomaticoMappingTests
{
    // Mesmo registro do Program.cs da API.
    private static AutoMapper.IMapper CriarMapper() =>
        new ServiceCollection()
            .AddLogging()
            .AddAutoMapper(typeof(ERP.Application.Mappings.MappingProfile))
            .BuildServiceProvider()
            .GetRequiredService<AutoMapper.IMapper>();

    [Fact]
    public void Preco_B_zerado_chega_no_DTO_ja_calculado()
    {
        var p = new Product { Name = "Assento", OriginalCost = 54.56m, IpiPercent = 15m, SalePrice = 112.90m };

        CriarMapper().Map<ERP.Application.DTOs.ProductDto>(p).PrecoBRevendedor.Should().Be(78.43m);
    }

    [Fact]
    public void Preco_B_manual_chega_no_DTO_sem_mudar()
    {
        var p = new Product { Name = "Assento", OriginalCost = 54.56m, IpiPercent = 15m, SalePrice = 112.90m, PrecoBRevendedor = 90m };

        CriarMapper().Map<ERP.Application.DTOs.ProductDto>(p).PrecoBRevendedor.Should().Be(90m);
    }
}
