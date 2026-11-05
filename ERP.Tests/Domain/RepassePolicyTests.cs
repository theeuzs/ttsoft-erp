using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Services;
using FluentAssertions;
using Xunit;

namespace ERP.Tests.Domain;

// S{N} FIX: grupo de preco R (repasse = custo + IPI + 25%).
public class RepassePolicyTests
{
    [Fact]
    public void Ducha_Sintex_do_print_da_80_42()
    {
        // OriginalCost 54,99 + IPI 17% = 64,3383 (Custo Final Calculado da tela) x 1,25
        var p = new Product { OriginalCost = 54.99m, IpiPercent = 17m, SalePrice = 179.90m };

        RepassePolicy.PrecoDoProduto(p).Should().Be(80.42m);
        p.GetPrecoParaGrupo(GrupoPreco.R).Should().Be(80.42m);
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
    public void Sem_custo_nenhum_grupo_R_cai_pro_varejo_nunca_zero()
    {
        var p = new Product { OriginalCost = 0m, CostPrice = 0m, SalePrice = 50m };

        RepassePolicy.PrecoDoProduto(p).Should().Be(0m);
        p.GetPrecoParaGrupo(GrupoPreco.R).Should().Be(50m);
    }

    [Fact]
    public void Produto_filho_sem_custo_usa_custo_do_pai_vezes_fator()
    {
        var pai   = new Product { Id = Guid.NewGuid(), OriginalCost = 10m, IpiPercent = 0m };
        var filho = new Product { ParentProductId = pai.Id, ConversionFactor = 6m, SalePrice = 99m };

        filho.GetPrecoParaGrupo(GrupoPreco.R, pai).Should().Be(75m); // 10 x 6 x 1,25
        filho.GetPrecoParaGrupo(GrupoPreco.R).Should().Be(99m);      // sem o pai: varejo
    }

    [Fact]
    public void Grupo_B_continua_usando_o_preco_B_cadastrado()
    {
        var p = new Product { OriginalCost = 54.99m, IpiPercent = 17m, SalePrice = 179.90m, PrecoBRevendedor = 120m };

        p.GetPrecoParaGrupo(GrupoPreco.B).Should().Be(120m);
        p.GetPrecoParaGrupo(GrupoPreco.R).Should().Be(80.42m);
    }
}
