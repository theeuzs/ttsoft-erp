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
//  5. Custo e repasse nunca saem deste servico. A busca do painel devolve so o PISO de venda (BuscarAsync).
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

    private static decimal CustoEfetivo(ProdutoLinha p) =>
        CustoEfetivo(p.CostPrice, p.OriginalCost, p.EhFilho, p.ConversionFactor, p.CostPricePai, p.OriginalCostPai);

    private static decimal CustoEfetivo(decimal costPrice, decimal originalCost, bool ehFilho, decimal conversionFactor,
        decimal? costPricePai, decimal? originalCostPai)
    {
        var proprio = costPrice > 0 ? costPrice : originalCost;
        if (proprio > 0 || !ehFilho) return proprio;

        var custoPai = (costPricePai ?? 0m) > 0 ? costPricePai!.Value : (originalCostPai ?? 0m);
        return custoPai * conversionFactor;
    }

    // -------------------------------------------------------------------------------------------------
    // S{N} FIX: busca do painel do site (adicionar produto). Somente leitura, mesmo tenant fixo da configuracao.
    // -------------------------------------------------------------------------------------------------
    private sealed record ProdutoBuscaLinha(
        string? Sku,
        string? Barcode,
        string Nome,
        string? Descricao,
        string? ImagemUrl,
        string? Categoria,
        string? Marca,
        string Unidade,
        decimal SalePrice,
        decimal Estoque,
        decimal CostPrice,
        decimal OriginalCost,
        bool EhFilho,
        decimal ConversionFactor,
        decimal? EstoquePai,
        decimal? CostPricePai,
        decimal? OriginalCostPai);

    public async Task<List<ProdutoBuscaResposta>> BuscarAsync(string termo, int limite, CancellationToken ct)
    {
        termo = termo.Trim();
        var ativos = _ctx.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.TenantId == _opt.TenantId && !p.IsDeleted && p.IsActive);

        // 1) Codigo exato: SKU interno ou codigo de barras.
        var exatos = await Projetar(ativos.Where(p => p.SKU == termo || p.Barcode == termo))
            .Take(limite)
            .ToListAsync(ct);

        // 2) Nome contendo TODAS as palavras digitadas ("alicate irwin").
        var porNome = ativos;
        foreach (var palavra in termo.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(6))
        {
            var w = palavra;
            porNome = porNome.Where(p => p.Name.Contains(w));
        }
        var nomes = await Projetar(porNome.OrderBy(p => p.Name))
            .Take(limite)
            .ToListAsync(ct);

        return exatos
            .Concat(nomes)
            .Where(l => !string.IsNullOrWhiteSpace(l.Sku) || !string.IsNullOrWhiteSpace(l.Barcode)) // sem codigo o site nao consegue consultar
            .DistinctBy(l => (l.Sku, l.Barcode, l.Nome))
            .Take(limite)
            .Select(ParaResposta)
            .ToList();
    }

    private static IQueryable<ProdutoBuscaLinha> Projetar(IQueryable<ERP.Domain.Entities.Product> q) =>
        q.Select(p => new ProdutoBuscaLinha(
            p.SKU,
            p.Barcode,
            p.Name,
            p.DescricaoDetalhada,
            p.ImageUrl,
            p.Category != null ? p.Category.Name : null,
            p.Brand != null ? p.Brand.Name : null,
            p.Unit,
            p.SalePrice,
            p.Stock,
            p.CostPrice,
            p.OriginalCost,
            p.ParentProductId != null && p.ConversionFactor > 0,
            p.ConversionFactor,
            p.ParentProduct != null && !p.ParentProduct.IsDeleted ? (decimal?)p.ParentProduct.Stock : null,
            p.ParentProduct != null ? (decimal?)p.ParentProduct.CostPrice : null,
            p.ParentProduct != null ? (decimal?)p.ParentProduct.OriginalCost : null));

    private ProdutoBuscaResposta ParaResposta(ProdutoBuscaLinha l)
    {
        var custo = CustoEfetivo(l.CostPrice, l.OriginalCost, l.EhFilho, l.ConversionFactor, l.CostPricePai, l.OriginalCostPai);
        decimal? piso = null;
        string? motivo = null;

        if (custo <= 0)
            motivo = "Custo invalido (CostPrice e OriginalCost zerados)";
        else if (l.EhFilho && l.EstoquePai is null)
            motivo = "Produto filho sem produto pai valido";
        else
        {
            // Mesmo piso do ConsultarAsync, para uma unidade: repasse x (1 + %) e repasse + R$ minimo.
            var repasse = Math.Round(custo * _opt.FatorRepasse, 2, MidpointRounding.AwayFromZero);
            var minimo = Math.Max(repasse * (1 + _opt.MargemMinimaPercentual), repasse + _opt.MargemMinimaReais);
            piso = Math.Ceiling(minimo * 100m) / 100m;
        }

        var estoque = l.EhFilho ? (l.EstoquePai ?? 0m) / l.ConversionFactor : l.Estoque;
        var vendavel = Math.Max(0, (int)Math.Floor(estoque) - _opt.EstoqueReservaLoja);
        var codigo = !string.IsNullOrWhiteSpace(l.Sku) ? l.Sku.Trim() : l.Barcode!.Trim();

        return new ProdutoBuscaResposta(
            codigo, l.Sku, l.Barcode, l.Nome, l.Descricao, l.ImagemUrl, l.Categoria, l.Marca, l.Unidade,
            l.SalePrice, piso, motivo, vendavel);
    }
}
