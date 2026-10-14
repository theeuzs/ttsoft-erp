namespace ERP.Api.Ecommerce;

// S{N} FIX: Theuzs e-commerce - configuracao do canal (secao "Ecommerce" do appsettings / App Settings do Azure).
// No Azure: Ecommerce__TenantId, Ecommerce__ApiKey, etc.
public sealed class EcommerceOptions
{
    public const string Secao = "Ecommerce";

    /// <summary>Tenant da Vila Verde. Fixo na configuracao, NUNCA vem da requisicao.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Chave compartilhada com o servidor do site (header X-Theuzs-Key). Minimo 32 caracteres.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Repasse para a loja = custo efetivo x FatorRepasse (acordo custo + 25%).</summary>
    public decimal FatorRepasse { get; set; } = 1.25m;

    /// <summary>
    /// Sobra minima em reais POR LINHA: (preco de venda - repasse) x quantidade.
    /// Padrao 0: so bloqueia venda abaixo do repasse (prejuizo). Frete e cobrado a parte do cliente.
    /// </summary>
    public decimal MargemMinimaReais { get; set; } = 0m;

    /// <summary>Sobra minima percentual sobre o repasse, POR UNIDADE (0,10 = preco >= repasse x 1,10). Padrao 0.</summary>
    public decimal MargemMinimaPercentual { get; set; } = 0m;

    /// <summary>Unidades que ficam sempre reservadas para o balcao da loja (nunca oferecidas no site).</summary>
    public int EstoqueReservaLoja { get; set; } = 2;

    /// <summary>Teto de unidades do SKU num pedido do site (caixa com 10 lampadas consome 10).</summary>
    public int QuantidadeMaximaPorItem { get; set; } = 50;

    /// <summary>Limite de SKUs por consulta (protecao contra abuso).</summary>
    public int MaxItensPorConsulta { get; set; } = 300;

    public bool EstaConfigurado => TenantId != Guid.Empty && ApiKey.Length >= 32;
}
