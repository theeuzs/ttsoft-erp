using ERP.Api.Security;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IProductService          _service;
    private readonly IProdutoAgregadoService  _agregados;
    private readonly AppDbContext             _db;

    public ProductsController(IProductService service, IProdutoAgregadoService agregados, AppDbContext db)
    {
        _service   = service;
        _agregados = agregados;
        _db        = db;
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
    [HasPermission(Permissions.ProductEdit)]
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
    [HasPermission(Permissions.ProductEdit)]
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
    [HasPermission(Permissions.ProductEdit)]
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
    [HasPermission(Permissions.StockAdjust)]
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
        [FromQuery] string? categoria = null,
        [FromQuery] Guid?   tenantId  = null)
    {
        // S10 FIX: filtros aplicados no banco, não em memória após paginação.
        // Antes: GetPagedAsync(24 itens) → filtrar em memória → TotalItems = count(página) ← errado.
        // Agora: filtros no IQueryable → Skip/Take → TotalItems = count(antes do skip) ← correto.

        // TenantId: se JWT presente, vem do HasQueryFilter automaticamente.
        // Se AllowAnonymous sem JWT, aceita tenantId como query param (catálogo público de loja específica).
        var query = _db.Products.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted);

        // Filtro de tenant explícito para acesso anônimo
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            query = query.IgnoreQueryFilters()
                         .Where(p => p.IsActive && !p.IsDeleted && p.TenantId == tenantId.Value);

        // Filtra com estoque disponível
        query = query.Where(p => p.Stock > 0);

        // Busca por nome ou código de barras
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Barcode.Contains(search));

        // Filtro por categoria
        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(p => p.Category != null && p.Category.Name == categoria);

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Name,
                CategoryName = p.Category != null ? p.Category.Name : "",
                p.Barcode,
                p.Unit,
                SalePrice = p.SalePrice,
                Stock     = p.Stock,
                ImageUrl  = (string?)null
            })
            .ToListAsync();

        return Ok(new { Items = items, TotalItems = total, Page = page, PageSize = pageSize });
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
    [HasPermission(Permissions.ProductEdit)]
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
    [HasPermission(Permissions.ProductEdit)]
    [HttpDelete("{id:guid}/agregados/{relacionadoId:guid}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> RemoverAgregado(Guid id, Guid relacionadoId)
    {
        await _agregados.RemoverAgregadoAsync(id, relacionadoId);
        return NoContent();
    }

}

public record StockAdjustDto(decimal NovoEstoque, string Motivo = "Ajuste via API");