// ── ERP.Application/DTOs/PedidoMarketplaceDto.cs ───────────────────────────
using ERP.Domain.Enums;

namespace ERP.Application.DTOs;

/// <summary>Uma linha da tela "Marketplace → Pedidos" — cruza o que o canal
/// diz (ExternalStatus) com o que o ERP fez com isso (InternalStatus/Venda).</summary>
public record PedidoMarketplaceDto(
    Guid Id,
    string ExternalOrderId,
    string CanalNome,
    string? BuyerNickname,
    string ExternalStatus,
    ExternalOrderStatus InternalStatus,
    decimal ValorTotal,
    Guid? VendaId,
    string? VendaNumero,
    string? ShippingMode,
    string? ShippingStatus,
    DateTime DataPedidoExterno,
    DateTime? UltimaSincronizacao)
{
    /// <summary>Já virou venda no ERP.</summary>
    public bool TemVenda => VendaId.HasValue;

    /// <summary>Tem informação de frete — hoje sempre false pra pedido de
    /// teste (ver ExternalOrder.ShippingId), fica pronto pro dia que existir.</summary>
    public bool TemFrete => !string.IsNullOrEmpty(ShippingMode) || !string.IsNullOrEmpty(ShippingStatus);

    /// <summary>Faz sentido oferecer o botão "Reprocessar" — só não faz
    /// sentido pra pedido cancelado (estado final, reprocessar não muda nada).</summary>
    public bool PodeReprocessar => InternalStatus != ExternalOrderStatus.Cancelado;
}