// ── ERP.Api/Controllers/MarketplaceOrdersController.cs ─────────────────────
using ERP.Api.Security;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>
/// Tela "Marketplace → Pedidos" — a mais usada no dia a dia, segundo a ordem
/// de prioridade combinada: cruza o status do canal com o que o ERP fez com
/// cada pedido, permite reprocessar um pedido preso, e mostra os campos de
/// frete (ShippingId/Mode/Status) mesmo hoje sempre nulos — o ponto de
/// extensão pronto pra quando existir pedido real com Mercado Envios.
/// </summary>
[ApiController]
[Route("api/marketplace/pedidos")]
public class MarketplaceOrdersController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IOrderProcessingService _orderProcessing;

    public MarketplaceOrdersController(IUnitOfWork uow, IOrderProcessingService orderProcessing)
    {
        _uow             = uow;
        _orderProcessing = orderProcessing;
    }

    [HttpGet]
    [HasPermission(Permissions.ConfigView)]
    public async Task<IActionResult> Listar()
    {
        var pedidos = await _uow.OrderSync.ListarPedidosAsync();
        var resultado = pedidos.Select(p => new PedidoMarketplaceDto(
            p.Id, p.ExternalOrderId, p.SalesChannel?.Nome ?? "(canal removido)", p.BuyerNickname,
            p.ExternalStatus, p.InternalStatus, p.ValorTotal,
            p.VendaId, p.Venda?.SaleNumber, p.ShippingMode, p.ShippingStatus,
            p.DataPedidoExterno, p.UpdatedAt));

        return Ok(resultado);
    }

    /// <summary>
    /// Reprocessa um pedido específico — busca de novo no canal e roda pelo
    /// mesmo pipeline do webhook/polling. Útil pra um pedido preso em
    /// ConflitoAberto (ex: SKU não mapeado) depois que o lojista resolveu a
    /// causa (mapeou o produto) — sem precisar esperar um novo evento chegar
    /// sozinho do marketplace.
    /// </summary>
    [HttpPost("{id:guid}/reprocessar")]
    [HasPermission(Permissions.ConfigView)]
    public async Task<IActionResult> Reprocessar(Guid id)
    {
        var pedido = await _uow.OrderSync.GetExternalOrderPorIdAsync(id);
        if (pedido is null) return NotFound("Pedido não encontrado.");

        await _orderProcessing.ProcessarPedidoIndividualAsync(pedido.SalesChannelId, pedido.ExternalOrderId);
        return Ok();
    }
}