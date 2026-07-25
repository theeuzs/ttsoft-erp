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
    DateTime? UltimaSincronizacao);