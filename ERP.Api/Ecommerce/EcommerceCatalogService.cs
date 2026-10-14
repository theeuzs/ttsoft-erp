using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Api.Ecommerce;

// S{N} FIX: Theuzs e-commerce - leitura do catalogo da Vila Verde. SOMENTE LEITURA: este servico nao grava nada no ERP.
//
// Regras:
//  1. Tenant vem de EcommerceOptions.TenantId, nunca da requisicao. IgnoreQueryFilters() + filtro explicito de
//     TenantId e IsDeleted em TODA consulta (mesmo padrao do CatalogoPublicoService; o IgnoreQueryFilters
//     tambem desliga o filtro de soft delete).
//  2. Custo efetivo = CostPrice se > 0, senao OriginalCost (linha Zagonel/Sintex tem CostPrice = 0 na base).
//     Produto filho (ParentProductId) sem custo proprio usa custo efetivo do pai x ConversionFactor.
//     Custo efetivo <= 0 => SKU NAO publicavel (evita vender de graca).
//  3. Piso de preco: preco >= repasse x (1 + MargemMinimaPercentual) por unidade, e
//     (preco - repasse) x quantidade >= MargemMinimaReais por linha (permite caixa com 10 lampadas).
//  4. Produto filho: estoque real fica no pai (ver Product.ParentProductId) => vendavel = estoque do pai / ConversionFactor.
//  5. Custo e repasse nunca saem deste servico.
public sealed class EcommerceCatalogService
{
    private readonly AppDbContext _ctx;
    private readonly EcommerceOptions _opt;
    private readonly ILogger<EcommerceCatalogService> _log;

    public EcommerceCatalogService(AppDbContext ctx, IOptions<EcommerceOptions> opt, ILogger<EcommerceCatalogService> log)
    {
        _ctx = ctx;
        _opt = opt.Value;
        _log = log;
    }

    private sealed record ProdutoLinha(
        string? Sku,
        string? Barcode,
        string Nome,
        string? Descricao,
        string? ImagemUrl,
        decimal Estoque,
        decimal CostPrice,
        decimal OriginalCost,
        bool EhFilho,
        decimal ConversionFactor,
        decimal? EstoquePai,
        decimal? CostPricePai,
        decimal? OriginalCostPai);

    public async Task<List<ItemCatalogoResposta>> ConsultarAsync(IReadOnlyList<ItemConsultaCatalogo> itens, CancellationToken ct)
    {
        var skus = itens
            .Select(i => i.Sku.Trim())
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var linhas = await _ctx.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.TenantId == _opt.TenantId
                        && !p.IsDeleted
                        && p.IsActive
                        && ((p.SKU != null && skus.Contains(p.SKU))
                            || (p.Barcode != null && skus.Contains(p.Barcode))))
            .Select(p => new ProdutoLinha(
                p.SKU,
                p.Barcode,
                p.Name,
                p.DescricaoDetalhada,
                p.ImageUrl,
                p.Stock,
                p.CostPrice,
                p.OriginalCost,
                p.ParentProductId != null && p.ConversionFactor > 0,
                p.ConversionFactor,
                p.ParentProduct != null && !p.ParentProduct.IsDeleted ? (decimal?)p.ParentProduct.Stock : null,
                p.ParentProduct != null ? (decimal?)p.ParentProduct.CostPrice : null,
                p.ParentProduct != null ? (decimal?)p.ParentProduct.OriginalCost : null))
            .ToListAsync(ct);

        // O site pode mandar o SKU Interno OU o codigo de barras (no cadastro da loja muitos produtos so tem um dos dois).
        // SKU tem prioridade. Codigo duplicado dentro do tenant e problema de cadastro: usa o primeiro e avisa no log.
        var porSku = new Dictionary<string, ProdutoLinha>(StringComparer.OrdinalIgnoreCase);
        var porBarcode = new Dictionary<string, ProdutoLinha>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in linhas)
        {
            if (!string.IsNullOrWhiteSpace(l.Sku) && !porSku.TryAdd(l.Sku.Trim(), l))
                _log.LogWarning("Ecommerce: SKU duplicado no tenant da loja: {Sku}", l.Sku);
            if (!string.IsNullOrWhiteSpace(l.Barcode) && !porBarcode.TryAdd(l.Barcode.Trim(), l))
                _log.LogWarning("Ecommerce: codigo de barras duplicado no tenant da loja: {Barcode}", l.Barcode);
        }

        var resposta = new List<ItemCatalogoResposta>(itens.Count);
        foreach (var item in itens)
        {
            var sku = item.Sku.Trim();
            if (!porSku.TryGetValue(sku, out var p) && !porBarcode.TryGetValue(sku, out p))
            {
                resposta.Add(new ItemCatalogoResposta(sku, false, false, "SKU/codigo de barras nao encontrado, inativo ou excluido",
                    null, null, null, false, 0));
                continue;
            }

            var custoEfetivo = CustoEfetivo(p);
            string? motivo = null;

            if (custoEfetivo <= 0)
            {
                motivo = "Custo invalido (CostPrice e OriginalCost zerados)";
            }
            else if (p.EhFilho && p.EstoquePai is null)
            {
                motivo = "Produto filho sem produto pai valido";
            }
            else
            {
                var repasse = Math.Round(custoEfetivo * _opt.FatorRepasse, 2, MidpointRounding.AwayFromZero);
                var sobraUnitaria = item.PrecoVenda - repasse;
                var quantidadeLinha = Math.Max(1, item.Quantidade);

                if (item.PrecoVenda < repasse * (1 + _opt.MargemMinimaPercentual))
                    motivo = "Preco de venda abaixo do piso percentual sobre o repasse";
                else if (sobraUnitaria * quantidadeLinha < _opt.MargemMinimaReais)
                    motivo = "Sobra da linha abaixo da margem minima em reais";
            }

            var publicavel = motivo is null;

            // Estoque vendavel = estoque fisico - reserva do balcao, limitado ao teto por SKU.
            // Produto filho: o estoque vive no pai, em unidades do pai.
            var estoqueEmUnidadesDeVenda = p.EhFilho
                ? (p.EstoquePai ?? 0m) / p.ConversionFactor
                : p.Estoque;
            var vendavel = (int)Math.Floor(estoqueEmUnidadesDeVenda) - _opt.EstoqueReservaLoja;
            var quantidadeMaxima = publicavel ? Math.Clamp(vendavel, 0, _opt.QuantidadeMaximaPorItem) : 0;
            var disponivel = publicavel && quantidadeMaxima >= Math.Max(1, item.Quantidade);

            if (!publicavel)
                _log.LogInformation("Ecommerce: SKU {Sku} bloqueado: {Motivo}", sku, motivo);

            resposta.Add(new ItemCatalogoResposta(
                sku, true, publicavel, motivo,
                p.Nome, p.Descricao, p.ImagemUrl,
                disponivel, quantidadeMaxima));
        }

        return resposta;
    }

    private static decimal CustoEfetivo(ProdutoLinha p)
    {
        var proprio = p.CostPrice > 0 ? p.CostPrice : p.OriginalCost;
        if (proprio > 0 || !p.EhFilho) return proprio;

        var custoPai = (p.CostPricePai ?? 0m) > 0 ? p.CostPricePai!.Value : (p.OriginalCostPai ?? 0m);
        return custoPai * p.ConversionFactor;
    }
}
