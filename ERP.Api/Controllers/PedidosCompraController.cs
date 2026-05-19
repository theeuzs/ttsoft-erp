using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/pedidos-compra")]
[Authorize]
public class PedidosCompraController : ControllerBase
{
    private readonly IPedidoCompraService _service;
    public PedidosCompraController(IPedidoCompraService service) => _service = service;

    /// <summary>Lista todos os pedidos de compra.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>Retorna detalhe de um pedido.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var pedido = await _service.GetByIdAsync(id);
        return pedido is null ? NotFound() : Ok(pedido);
    }

    /// <summary>Confirma o recebimento de um pedido de compra.</summary>
    [HttpPost("{id:guid}/receber")]
    public async Task<IActionResult> Receber(Guid id)
    {
        try { await _service.ReceberAsync(id); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { erro = ex.Message }); }
    }

    /// <summary>Cancela um pedido de compra.</summary>
    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        try { await _service.CancelarAsync(id); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { erro = ex.Message }); }
    }
}
