using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService          _service;
    private readonly IProdutoAgregadoService  _agregados;

    public ProductsController(IProductService service, IProdutoAgregadoService agregados)
    {
        _service   = service;
        _agregados = agregados;
    }

    /// <summary>Lista produtos com paginação e busca opcional.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int    page     = 1,
        [FromQuery] int    pageSize = 50,
        [FromQuery] string? search  = null)
    {
        var result = await _service.GetPagedAsync(page, pageSize, search);
        return Ok(result);
    }

    /// <summary>Busca produto por ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var product = await _service.GetByIdAsync(id);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Busca produto por código de barras.</summary>
    [HttpGet("barcode/{barcode}")]
    [ProducesResponseType(typeof(ProductDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByBarcode(string barcode)
    {
        var product = await _service.GetByBarcodeAsync(barcode);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Busca produto por SKU.</summary>
    [HttpGet("sku/{sku}")]
    [ProducesResponseType(typeof(ProductDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBySku(string sku)
    {
        var product = await _service.GetBySkuAsync(sku);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>Lista produtos com estoque abaixo do mínimo.</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), 200)]
    public async Task<IActionResult> GetLowStock()
    {
        var products = await _service.GetLowStockListAsync();
        return Ok(products);
    }

    /// <summary>Cria novo produto.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        try
        {
            var product = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { erros = ex.Errors.Select(e => e.ErrorMessage) });
        }
    }

    /// <summary>Atualiza produto existente.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ProductDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductDto dto)
    {
        if (dto.Id != id) return BadRequest(new { erro = "ID da URL não corresponde ao body." });

        try
        {
            var product = await _service.UpdateAsync(dto);
            return Ok(product);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { erros = ex.Errors.Select(e => e.ErrorMessage) });
        }
    }

    /// <summary>Remove produto (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Ajusta estoque de um produto.</summary>
    [HttpPatch("{id:guid}/stock")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AdjustStock(Guid id, [FromBody] StockAdjustDto dto)
    {
        var product = await _service.GetByIdAsync(id);
        if (product is null) return NotFound();

        // Reutiliza o serviço existente via UpdateProductDto
        var updateDto = new UpdateProductDto
        {
            Id    = id,
            Name  = product.Name,
            Stock = dto.NovoEstoque
        };

        await _service.UpdateAsync(updateDto);
        return NoContent();
    }
    /// <summary>Catálogo público — não requer autenticação.</summary>
    [HttpGet("catalogo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCatalogo(
        [FromQuery] int     page      = 1,
        [FromQuery] int     pageSize  = 24,
        [FromQuery] string? search    = null,
        [FromQuery] string? categoria = null)
    {
        var result = await _service.GetPagedAsync(page, pageSize, search);
        var items  = result.Items
            .Where(p => p.IsActive && p.Stock > 0)
            .Where(p => string.IsNullOrEmpty(categoria) || p.CategoryName == categoria)
            .Select(p => new { p.Id, p.Name, p.CategoryName, p.Barcode,
                               p.Unit, p.SalePrice, p.Stock, ImageUrl = (string?)null });
        return Ok(new { Items = items, TotalItems = items.Count(), Page = page, PageSize = pageSize });
    }
}


public class ProdutosAgregadosController : ControllerBase
{
    private readonly IProdutoAgregadoService _agregados;

    public ProdutosAgregadosController(IProdutoAgregadoService agregados)
        => _agregados = agregados;

    /// <summary>
    /// Retorna os produtos sugeridos para exibir no popup do PDV quando
    /// <paramref name="id"/> é adicionado ao carrinho.
    /// Filtra apenas com estoque > 0. Máx 6 resultados.
    /// </summary>
    [HttpGet("{id:guid}/sugestoes")]
    [ProducesResponseType(typeof(IEnumerable<ProdutoAgregadoDto>), 200)]
    public async Task<IActionResult> GetSugestoes(Guid id)
    {
        var sugestoes = await _agregados.GetSugestoesAsync(id);
        return Ok(sugestoes);
    }

    /// <summary>
    /// Retorna todos os produtos relacionados do produto (incluindo sem estoque).
    /// Usado pelo cadastro de produto para gerenciar a lista.
    /// </summary>
    [HttpGet("{id:guid}/agregados")]
    [ProducesResponseType(typeof(IEnumerable<ProdutoAgregadoDto>), 200)]
    public async Task<IActionResult> GetAgregados(Guid id)
    {
        var agregados = await _agregados.GetAgregadosAsync(id);
        return Ok(agregados);
    }

    /// <summary>
    /// Salva a lista completa de produtos relacionados (replace).
    /// Envia a lista completa — os que foram removidos da lista serão excluídos.
    /// </summary>
    [HttpPut("{id:guid}/agregados")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> SalvarAgregados(Guid id, [FromBody] SalvarAgregadosDto dto)
    {
        try
        {
            await _agregados.SalvarAgregadosAsync(id, dto.Itens);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Remove um produto específico da lista de relacionados.</summary>
    [HttpDelete("{id:guid}/agregados/{relacionadoId:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoverAgregado(Guid id, Guid relacionadoId)
    {
        await _agregados.RemoverAgregadoAsync(id, relacionadoId);
        return NoContent();
    }

}

public record StockAdjustDto(decimal NovoEstoque, string Motivo = "Ajuste via API");
