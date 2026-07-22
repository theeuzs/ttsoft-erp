// ── ERP.Domain/Entities/SalesChannelPricingPolicy.cs ──────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Ajuste de preço aplicado só quando o produto é publicado/sincronizado neste
/// canal (ex: markup pra cobrir a comissão do Mercado Livre). Não mexe no
/// SalePrice do Product, que continua sendo o preço de venda local (PDV/Portal).
/// </summary>
public class SalesChannelPricingPolicy : BaseEntity
{
    public Guid         SalesChannelId { get; set; }
    public SalesChannel? SalesChannel  { get; set; }

    public string  Nome                { get; set; } = string.Empty;
    public decimal PercentualAjuste    { get; set; } // ex: 15 = +15% sobre o SalePrice
    public bool    AplicarAutomaticamente { get; set; } = true;
}
