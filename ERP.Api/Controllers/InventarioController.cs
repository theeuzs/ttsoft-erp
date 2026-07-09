using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

/// <summary>
/// Controller de Inventário.
/// Usa IProductService.GetPagedAsync para listagem com busca e paginação direto no banco.
/// IInventarioService fica restrito às operações de contagem e ajuste de estoque.
///
/// SPRINT 1 FIX: GetProdutos não chama mais ObterProdutosAsync() que fazia SELECT *
/// e filtrava em memória — OOM risk eliminado.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventarioController : ControllerBase
{
    private readonly IInventarioService _inventario;
    private readonly IProductService    _products;

    public InventarioController(IInventarioService inventario, IProductService products)
    {
        _inventario = inventario;
        _products   = products;
    }

    /// <summary>
    /// Lista produtos para contagem com busca e paginação via IQueryable (banco).
    /// Antes: ObterProdutosAsync() carregava todos os produtos na memória e filtrava aqui.
    /// Agora: GetPagedAsync() faz WHERE + SKIP + TAKE direto no SQL Server.
    /// </summary>
    [HttpGet("produtos")]
    public async Task<IActionResult> GetProdutos(
        [FromQuery] string? search = null,
        [FromQuery] int     pagina = 1,
        [FromQuery] int     tam    = 50)
    {
        tam = Math.Clamp(tam, 1, 200);

        // IProductService.GetPagedAsync gera WHERE name/SKU LIKE + OFFSET/FETCH no banco
        var resultado = await _products.GetPagedAsync(pagina, tam, search);

        return Ok(new
        {
            Items      = resultado.Items,
            Total      = resultado.TotalItems,
            Pagina     = pagina,
            TotalPages = (int)Math.Ceiling(resultado.TotalItems / (double)tam)
        });
    }

    /// <summary>Busca produto por código de barras (scanner).</summary>
    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetByBarcode(string barcode)
    {
        var produto = await _products.GetByBarcodeAsync(barcode)
                   ?? await _products.GetBySkuAsync(barcode);

        return produto is null
            ? NotFound(new { erro = $"Produto não encontrado: {barcode}" })
            : Ok(new
            {
                produto.Id,
                produto.Name,
                produto.Barcode,
                produto.SKU,
                produto.Unit,
                EstoqueAtual = produto.Stock
            });
    }

    /// <summary>
    /// Registra a contagem de um item e retorna divergência imediata.
    /// Lookup via IProductService — sem acesso direto ao DbContext.
    /// </summary>
    [HasPermission(Permissions.InventarioView)]
    [HttpPost("contar")]
    public async Task<IActionResult> ContarItem([FromBody] ContarItemInputDto dto)
    {
        var produto = await _products.GetByIdAsync(dto.ProdutoId);
        if (produto is null)
            return NotFound(new { erro = $"Produto {dto.ProdutoId} não encontrado." });

        var divergencia = dto.QuantidadeContada - produto.Stock;
        return Ok(new
        {
            ProdutoId            = produto.Id,
            Nome                 = produto.Name,
            EstoqueAtual         = produto.Stock,
            dto.QuantidadeContada,
            Divergencia          = divergencia,
            Status               = divergencia == 0 ? "OK" : divergencia > 0 ? "Sobra" : "Falta"
        });
    }

    /// <summary>Aplica ajustes de inventário em lote via IInventarioService.</summary>
    [HasPermission(Permissions.StockAdjust)]
    [HttpPost("aplicar-ajustes")]
    public async Task<IActionResult> AplicarAjustes([FromBody] AjusteInputDto dto)
    {
        if (dto.Itens?.Any() != true)
            return BadRequest(new { erro = "Nenhum item informado." });

        var ajustes = dto.Itens
            .Select(i => (ProductId: i.ProdutoId, NovoEstoque: i.QuantidadeContada));

        await _inventario.AplicarAjustesAsync(ajustes);

        return Ok(new
        {
            Ajustados  = dto.Itens.Count,
            Mensagem   = $"{dto.Itens.Count} produto(s) ajustado(s).",
            AplicadoEm = DateTime.Now
        });
    }

    /// <summary>
    /// Gera relatório de divergências entre estoque atual e contagem.
    /// PERFORMANCE FIX: usa ObterProdutosPorIdsAsync — antes chamava
    /// ObterProdutosAsync() (catálogo inteiro) e filtrava em memória aqui, apesar
    /// do comentário anterior dizer o contrário. Agora o filtro por ID vai na
    /// própria query SQL.
    /// </summary>
    [HasPermission(Permissions.InventarioView)]
    [HttpPost("relatorio-divergencias")]
    public async Task<IActionResult> RelatorioDivergencias(
        [FromBody] List<ItemContadoInputDto> itens)
    {
        if (itens is null || itens.Count == 0)
            return BadRequest(new { erro = "Nenhum item informado." });

        var ids     = itens.Select(i => i.ProdutoId).Distinct().ToList();
        var todos   = await _inventario.ObterProdutosPorIdsAsync(ids);
        var lookup  = todos.ToDictionary(p => p.ProductId);

        var divergencias = itens
            .Where(i => lookup.ContainsKey(i.ProdutoId))
            .Select(i =>
            {
                var p    = lookup[i.ProdutoId];
                var diff = i.QuantidadeContada - p.EstoqueSistema;
                return new
                {
                    ProdutoId            = p.ProductId,
                    Nome                 = p.Nome,
                    EstoqueAtual         = p.EstoqueSistema,
                    i.QuantidadeContada,
                    Divergencia          = diff,
                    Status               = diff == 0 ? "OK" : diff > 0 ? "Sobra" : "Falta"
                };
            })
            .OrderByDescending(d => Math.Abs(d.Divergencia))
            .ToList();

        return Ok(new
        {
            TotalContados = itens.Count,
            TotalOK       = divergencias.Count(d => d.Status == "OK"),
            TotalSobras   = divergencias.Count(d => d.Status == "Sobra"),
            TotalFaltas   = divergencias.Count(d => d.Status == "Falta"),
            Divergencias  = divergencias,
            GeradoEm      = DateTime.Now
        });
    }
}

public class ContarItemInputDto
{
    public Guid    ProdutoId         { get; set; }
    public decimal QuantidadeContada { get; set; }
    public string? Observacao        { get; set; }
}

public class ItemContadoInputDto
{
    public Guid    ProdutoId         { get; set; }
    public decimal QuantidadeContada { get; set; }
}

public class AjusteInputDto
{
    public List<ItemContadoInputDto>? Itens      { get; set; }
    public string?                    Observacao { get; set; }
}