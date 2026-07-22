// ── ERP.Domain/Entities/ShadowStockReservation.cs ──────────────────────────────
using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

/// <summary>
/// Reserva de estoque enquanto um ExternalOrder está em processamento — existe
/// pra evitar overselling quando o mesmo produto pode ser vendido por mais de
/// um canal (ou pelo PDV local) ao mesmo tempo (ver Roadmap, item 3.3).
///
/// É livro-razão, não um saldo mutável: cada reserva é uma linha própria,
/// somada em tempo de leitura (SUM(Quantidade) WHERE Status = Reservada) pra
/// saber quanto está comprometido agora — mesmo princípio de
/// CaixaMovimento/MovimentoContaBancaria. Fica separada de SkuMapping porque
/// responde uma pergunta diferente: não "quem é esse produto", e sim "quanto
/// está reservado, por qual pedido, agora".
/// </summary>
public class ShadowStockReservation : BaseEntity
{
    public Guid           ExternalOrderId { get; set; }
    public ExternalOrder?  ExternalOrder   { get; set; }
    public Guid           CorrelationId   { get; set; }

    public Guid     ProductId { get; set; }
    public Product?  Product  { get; set; }

    public decimal Quantidade { get; set; }

    public StatusReservaEstoque Status { get; set; } = StatusReservaEstoque.Reservada;

    /// <summary>Quando a reserva foi encerrada (confirmada em venda, liberada ou expirada).</summary>
    public DateTime? LiberadaEm { get; set; }
}
