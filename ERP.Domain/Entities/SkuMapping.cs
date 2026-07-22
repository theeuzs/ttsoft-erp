// ── ERP.Domain/Entities/SkuMapping.cs ──────────────────────────────────────────
using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Liga o SKU usado num SalesChannel ao Product interno. Único por
/// (SalesChannelId, SkuExterno) — configurar isso no EF Core na migração.
///
/// Entidade puramente de configuração/roteamento (De/Para estático) — não
/// guarda nenhum dado transacional. Reserva de estoque (quanto está
/// comprometido agora, por qual pedido) vive em ShadowStockReservation:
/// responsabilidade diferente (fato operacional que muda a cada pedido, não
/// uma regra de mapeamento), e precisa de rastro auditável por reserva, não
/// de um valor corrente que se sobrescreve a cada sync — mesmo motivo pelo
/// qual CaixaMovimento/MovimentoContaBancaria são livro-razão, não um campo
/// Saldo mutável.
/// </summary>
public class SkuMapping : BaseEntity
{
    public Guid          SalesChannelId { get; set; }
    public SalesChannel? SalesChannel   { get; set; }

    public string  SkuExterno { get; set; } = string.Empty;

    public Guid     ProductId { get; set; }
    public Product?  Product  { get; set; }

    /// <summary>Quanto subtrair do estoque real antes de reportar ao canal (regra de configuração).</summary>
    public decimal BufferSeguranca { get; set; } = 0;
}
