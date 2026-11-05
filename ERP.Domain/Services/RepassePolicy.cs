namespace ERP.Domain.Services;

/// <summary>
/// S{N} FIX: preco de repasse (acordo da loja com o revendedor do canal proprio:
/// custo + IPI + 25%). FONTE UNICA da conta: usada pelo grupo de preco R
/// (Product.GetPrecoParaGrupo, PDV e SaleService) e pelo catalogo/pedidos do
/// site Theuzs (EcommerceCatalogService). Nao existe campo gravado: o preco e
/// sempre calculado do custo atual, entao nunca fica desatualizado.
/// </summary>
public static class RepassePolicy
{
    /// <summary>Fator do acordo (custo + 25%).</summary>
    public const decimal FatorPadrao = 1.25m;

    /// <summary>
    /// Igual ao "Custo Final Calculado" da tela de produto: OriginalCost x (1 + IPI%).
    /// CostPrice so entra quando OriginalCost esta zerado - a tela e a importacao por
    /// planilha atualizam so o OriginalCost; o CostPrice so muda em NF-e/pedido de compra
    /// e fica desatualizado (ex.: alicate Irwin, OriginalCost 44,38 x CostPrice antigo 31,97).
    /// </summary>
    public static decimal CustoDoCadastro(decimal costPrice, decimal originalCost, decimal ipiPercent) =>
        originalCost > 0 ? Math.Round(originalCost * (1 + ipiPercent / 100m), 4) : costPrice;

    /// <summary>
    /// Custo do proprio produto; para produto filho (atalho de venda, ex.: Barra 6m)
    /// sem custo proprio, usa o custo do pai x fator de conversao.
    /// </summary>
    public static decimal CustoEfetivo(decimal costPrice, decimal originalCost, decimal ipiPercent,
        bool ehFilho, decimal conversionFactor,
        decimal? costPricePai, decimal? originalCostPai, decimal? ipiPercentPai)
    {
        var proprio = CustoDoCadastro(costPrice, originalCost, ipiPercent);
        if (proprio > 0 || !ehFilho) return proprio;

        var custoPai = CustoDoCadastro(costPricePai ?? 0m, originalCostPai ?? 0m, ipiPercentPai ?? 0m);
        return custoPai * conversionFactor;
    }

    /// <summary>Repasse = custo efetivo x fator, 2 casas (meio pra cima). 0 quando nao ha custo.</summary>
    public static decimal Preco(decimal custoEfetivo, decimal fator = FatorPadrao) =>
        custoEfetivo > 0 ? Math.Round(custoEfetivo * fator, 2, MidpointRounding.AwayFromZero) : 0m;

    /// <summary>
    /// Repasse de um produto. <paramref name="pai"/> so e necessario para produto filho
    /// sem custo proprio; sem ele, esse caso devolve 0.
    /// </summary>
    public static decimal PrecoDoProduto(ERP.Domain.Entities.Product p,
        ERP.Domain.Entities.Product? pai = null, decimal fator = FatorPadrao) =>
        Preco(CustoEfetivo(p.CostPrice, p.OriginalCost, p.IpiPercent, p.IsProdutoFilho, p.ConversionFactor,
            pai?.CostPrice, pai?.OriginalCost, pai?.IpiPercent), fator);
}
