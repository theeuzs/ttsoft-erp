using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly IInventarioService _inventario;
    private readonly IProductService    _products;

    public StockController(IInventarioService inventario, IProductService products)
    {
        _inventario = inventario;
        _products   = products;
    }

    /// <summary>
    /// Retorna snapshot completo do estoque atual.
    /// Ideal para sincronizar com e-commerce ou sistemas externos.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InventarioProdutoDto>), 200)]
    public async Task<IActionResult> GetSnapshot()
    {
        var items = await _inventario.ObterProdutosAsync();
        return Ok(items);
    }

    /// <summary>Produtos com estoque abaixo do mínimo.</summary>
    [HttpGet("low")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), 200)]
    public async Task<IActionResult> GetLowStock()
    {
        var items = await _products.GetLowStockListAsync();
        return Ok(items);
    }

    /// <summary>
    /// Aplica ajustes de estoque em lote.
    /// Útil para importação de inventário físico.
    /// </summary>
    [HasPermission(Permissions.StockAdjust)]
    [HttpPost("adjust")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> AdjustBatch([FromBody] List<StockAdjustItemDto> ajustes)
    {
        if (ajustes is null || ajustes.Count == 0)
            return BadRequest(new { erro = "Lista de ajustes não pode ser vazia." });

        var items = ajustes.Select(a => (a.ProductId, a.NovoEstoque));
        await _inventario.AplicarAjustesAsync(items);
        return NoContent();
    }

    /// <summary>
    /// Retorna posição de estoque de um produto específico.
    /// </summary>
    [HttpGet("{productId:guid}")]
    [ProducesResponseType(typeof(StockPositionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPosition(Guid productId)
    {
        var product = await _products.GetByIdAsync(productId);
        if (product is null) return NotFound();

        return Ok(new StockPositionDto(
            product.Id,
            product.Name,
            product.Stock,
            product.MinStock,
            product.Stock <= product.MinStock));
    }
}

public record StockAdjustItemDto(Guid ProductId, decimal NovoEstoque, string Motivo = "Ajuste via API");
public record StockPositionDto(Guid ProductId, string Name, decimal Stock, decimal MinStock, bool AbaixoDoMinimo);
