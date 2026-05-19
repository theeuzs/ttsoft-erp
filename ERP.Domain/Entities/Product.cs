using ERP.Domain.Common;
using System;
using System.Collections.Generic;

namespace ERP.Domain.Entities;

public class Product : BaseEntity
{
    // Aba Geral
    public string Name { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string? SKU { get; set; }
    
    // 1. Relacionamento com Categoria
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }
    
    // 2. Relacionamento com Marca (Substituímos o "public string? Brand")
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }
    
    // 3. Relacionamento com Fornecedor (Substituímos o "public string? Supplier")
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string Unit { get; set; } = "UN";
    public bool IsActive { get; set; } = true;
    public bool AllowDiscount { get; set; } = true;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }

    // ── Sprint C: Tabela de preços por grupo de cliente ───────────────────────
    /// <summary>Preço Grupo B (Revendedor/Empreiteiro). 0 = usa SalePrice.</summary>
    public decimal PrecoBRevendedor { get; set; } = 0;
    /// <summary>Preço Grupo C (Atacadista/Grande conta). 0 = usa SalePrice.</summary>
    public decimal PrecoCAtacadista { get; set; } = 0;

    /// <summary>Retorna o preço correto conforme o grupo do cliente selecionado.</summary>
    public decimal GetPrecoParaGrupo(ERP.Domain.Enums.GrupoPreco grupo) => grupo switch
    {
        ERP.Domain.Enums.GrupoPreco.B when PrecoBRevendedor > 0 => PrecoBRevendedor,
        ERP.Domain.Enums.GrupoPreco.C when PrecoCAtacadista > 0 => PrecoCAtacadista,
        _ => SalePrice
    };

    public decimal Stock { get; set; }

    // Aba Impostos e Margem
    public decimal OriginalCost { get; set; }
    public decimal IpiPercent { get; set; }
    public decimal IcmsPercent { get; set; }
    public decimal FinalCost => OriginalCost * (1 + IpiPercent / 100) * (1 + IcmsPercent / 100);
    public decimal DesiredMarginPercent { get; set; }
    public decimal RealMarginPercent => SalePrice > 0 ? (SalePrice - FinalCost) / SalePrice * 100 : 0;
    public decimal UnitProfit => SalePrice - FinalCost;
    public decimal Markup => FinalCost > 0 ? SalePrice / FinalCost : 0;

    // Aba Localização
    public decimal MinStock { get; set; }
    public decimal IdealStock { get; set; }
    public bool AllowNegativeStock { get; set; } = false;
    public string? WarehouseLocation { get; set; }

    // Aba Fiscal
    public string? NCM { get; set; }
    public string? CEST { get; set; }
    public int MercadoriaOrigem { get; set; }

    // ── ICMS-ST (Substituição Tributária) ─────────────────────────────────
    /// <summary>Alíquota interna do estado destino para ST. Ex: 18</summary>
    public decimal? AliquotaInternaUFDest { get; set; }
    /// <summary>Margem de Valor Agregado original. Ex: 40 = 40%</summary>
    public decimal? MVAOriginal           { get; set; }
    /// <summary>MVA ajustado para operações interestaduais.</summary>
    public decimal? MVAAjustado           { get; set; }
    /// <summary>Código CEST para produtos com ST obrigatória.</summary>
    public bool     TemSubstituicaoTrib   { get; set; } = false;
    public string? CFOPPadrao { get; set; }
    public string? CSOSN { get; set; }
    public decimal? WholesaleMinQuantity { get; set; }
    public decimal? WholesalePrice { get; set; }

    // ── Conversão de unidade (Metro Linear, Barras, Folhas, etc.) ──────────
    /// <summary>Unidade em que o estoque é controlado. Ex: MT, KG, L</summary>
    public string? UnidadeEstoque { get; set; }
    /// <summary>Unidade em que é vendido no PDV. Ex: BR (Barra), FO (Folha), RL (Rolo)</summary>
    public string? UnidadeVenda { get; set; }
    /// <summary>Quantas UnidadeEstoque cabem em 1 UnidadeVenda. Ex: 1 Barra = 6 metros</summary>
    public decimal FatorConversao { get; set; } = 1m;
    /// <summary>Label exibido no recibo. Ex: "Barra(s)", "Folha(s)", "Rolo(s)"</summary>
    public string? LabelUnidadeVenda { get; set; }

    /// <summary>True quando o produto usa conversão de unidade</summary>
    public bool UsaConversaoUnidade => FatorConversao != 1m && !string.IsNullOrWhiteSpace(UnidadeVenda);

    // ── Mídia e Campanha ──────────────────────────────────────────────────────
    /// <summary>URL da imagem principal (Azure Blob ou Base64 data URI).</summary>
    public string? ImageUrl         { get; set; }

    /// <summary>Descrição detalhada / especificações técnicas do produto.</summary>
    public string? DescricaoDetalhada { get; set; }

    /// <summary>Flag para exibir no destaque de campanha no PDV.</summary>
    public bool    EmCampanha       { get; set; } = false;

    // ── Rastreamento de alteração de preço ───────────────────────────
    public DateTime? SalePriceChangedAt   { get; set; }
    public string?   SalePriceChangedBy   { get; set; }
    public DateTime? CostPriceChangedAt   { get; set; }
    public string?   CostPriceChangedBy   { get; set; }

    public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

    // ── Produtos Agregados / Auto-Sugestão ────────────────────────────────────
    /// <summary>
    /// Produtos que ESTE produto sugere quando adicionado ao carrinho.
    /// Ex: Cimento → [Areia, Brita, Desempenadeira]
    /// </summary>
    public ICollection<ProdutoAgregado> AgregadosPrincipais   { get; set; } = new List<ProdutoAgregado>();

    /// <summary>
    /// Relacionamentos em que ESTE produto aparece como sugestão de outro.
    /// Ex: Areia → [é sugestão de Cimento, é sugestão de Reboco]
    /// (navegação reversa — raramente consultada diretamente)
    /// </summary>
    public ICollection<ProdutoAgregado> AgregadosRelacionados { get; set; } = new List<ProdutoAgregado>();

    // ── Produto Composto (auto-relacionamento) ────────────────────────────
    /// <summary>
    /// Quando preenchido, este produto é um "atalho de venda" (ex: Barra 6m).
    /// O estoque NÃO é controlado aqui — é controlado no Produto Pai (ex: Tubo PVC MT).
    /// </summary>
    public Guid?    ParentProductId  { get; set; }
    /// <summary>
    /// Quantas unidades do Produto Pai são consumidas ao vender 1 unidade deste produto.
    /// Ex: Barra 6m → ConversionFactor = 6 (consome 6 MT do pai).
    /// </summary>
    public decimal  ConversionFactor { get; set; } = 1m;

    /// <summary>Navegação para o Produto Pai (produto que detém o estoque real).</summary>
    public virtual Product? ParentProduct { get; set; }

    /// <summary>True quando este produto é um atalho de venda com estoque no Pai.</summary>
    public bool IsProdutoFilho => ParentProductId.HasValue && ConversionFactor > 0;
}