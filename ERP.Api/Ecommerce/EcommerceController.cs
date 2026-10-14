using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Ecommerce;

// S{N} FIX: Theuzs e-commerce - endpoints do canal. Somente leitura nesta versao (v1).
// [AllowAnonymous] porque NAO usa o JWT do ERP; a protecao e a chave X-Theuzs-Key (EcommerceApiKeyFilter).
[ApiController]
[Route("api/ecommerce/v1")]
[AllowAnonymous]
[ServiceFilter(typeof(EcommerceApiKeyFilter))]
public sealed class EcommerceController : ControllerBase
{
    private readonly EcommerceCatalogService _catalogo;
    private readonly Microsoft.Extensions.Options.IOptions<EcommerceOptions> _opt;

    public EcommerceController(EcommerceCatalogService catalogo, Microsoft.Extensions.Options.IOptions<EcommerceOptions> opt)
    {
        _catalogo = catalogo;
        _opt = opt;
    }

    /// <summary>Teste de conexao do site com a API (valida a chave). Nao expoe segredo nem tenant.</summary>
    [HttpGet("saude")]
    public IActionResult Saude() => Ok(new { ok = true, servidor = DateTimeOffset.UtcNow });

    /// <summary>
    /// Consulta disponibilidade e validade de preco de uma lista de SKUs.
    /// Usado pelo site (a) para montar o catalogo, com cache, e (b) no checkout, para revalidar o carrinho.
    /// </summary>
    [HttpPost("catalogo/consultar")]
    public async Task<ActionResult<ConsultaCatalogoResponse>> Consultar([FromBody] ConsultaCatalogoRequest req, CancellationToken ct)
    {
        var itens = req.Itens ?? new List<ItemConsultaCatalogo>();

        if (itens.Count == 0)
            return BadRequest("Informe ao menos um item.");
        if (itens.Count > _opt.Value.MaxItensPorConsulta)
            return BadRequest($"Maximo de {_opt.Value.MaxItensPorConsulta} itens por consulta.");
        if (itens.Any(i => string.IsNullOrWhiteSpace(i.Sku) || i.Sku.Length > 60))
            return BadRequest("SKU invalido.");
        if (itens.Any(i => i.PrecoVenda <= 0 || i.PrecoVenda > 100_000m))
            return BadRequest("PrecoVenda invalido.");
        if (itens.Any(i => i.Quantidade < 1 || i.Quantidade > 1_000))
            return BadRequest("Quantidade invalida.");

        var resultado = await _catalogo.ConsultarAsync(itens, ct);
        return Ok(new ConsultaCatalogoResponse(DateTimeOffset.UtcNow, resultado));
    }

    /// <summary>
    /// S{N} FIX: busca para o painel do site adicionar produto: codigo exato (SKU ou codigo de barras) ou palavras do nome.
    /// Ex.: GET /api/ecommerce/v1/produtos/buscar?q=5706915058772  ou  ?q=alicate irwin
    /// </summary>
    [HttpGet("produtos/buscar")]
    public async Task<ActionResult<BuscaProdutosResponse>> Buscar([FromQuery] string? q, CancellationToken ct)
    {
        var termo = (q ?? string.Empty).Trim();
        if (termo.Length < 2 || termo.Length > 80)
            return BadRequest("Informe de 2 a 80 caracteres.");

        var itens = await _catalogo.BuscarAsync(termo, 20, ct);
        return Ok(new BuscaProdutosResponse(DateTimeOffset.UtcNow, itens));
    }
}
