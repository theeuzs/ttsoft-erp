using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Ecommerce;

// S{N} FIX: Theuzs e-commerce - endpoints do canal. Leitura (catalogo, busca) e, desde o lancamento de vendas,
// escrita restrita: pedidos/lancar e pedidos/estornar (desligados ate Ecommerce__LancamentoAtivo=true).
// [AllowAnonymous] porque NAO usa o JWT do ERP; a protecao e a chave X-Theuzs-Key (EcommerceApiKeyFilter).
[ApiController]
[Route("api/ecommerce/v1")]
[AllowAnonymous]
[ServiceFilter(typeof(EcommerceApiKeyFilter))]
public sealed class EcommerceController : ControllerBase
{
    private readonly EcommerceCatalogService _catalogo;
    private readonly EcommercePedidoService _pedidos;
    private readonly Microsoft.Extensions.Options.IOptions<EcommerceOptions> _opt;

    public EcommerceController(EcommerceCatalogService catalogo, EcommercePedidoService pedidos, Microsoft.Extensions.Options.IOptions<EcommerceOptions> opt)
    {
        _catalogo = catalogo;
        _pedidos = pedidos;
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

    // ---------------------------------------------------------------------------------------------
    // S{N} FIX: lancamento da venda do site (pedido "Pago" no painel do site). Ver EcommercePedidoService.
    // ---------------------------------------------------------------------------------------------
    private static readonly System.Text.RegularExpressions.Regex IdPedidoValido = new("^[A-Za-z0-9-]{6,60}$");

    /// <summary>Lanca a venda do pedido no ERP (preco de repasse, baixa de estoque, Conta a Receber). Idempotente.</summary>
    [HttpPost("pedidos/lancar")]
    public async Task<IActionResult> LancarPedido([FromBody] LancarPedidoRequest req, CancellationToken ct)
    {
        if (!_opt.Value.LancamentoAtivo)
            return StatusCode(503, new { erro = "Lancamento de vendas do site desligado na API (Ecommerce__LancamentoAtivo)." });

        var pedidoId = (req.PedidoId ?? string.Empty).Trim();
        var numero = (req.Numero ?? string.Empty).Trim();
        var linhas = req.Linhas ?? new List<LinhaPedidoEcommerce>();

        if (!IdPedidoValido.IsMatch(pedidoId)) return BadRequest(new { erro = "PedidoId invalido." });
        if (numero.Length is < 1 or > 30) return BadRequest(new { erro = "Numero do pedido invalido." });
        if (linhas.Count is < 1 or > 60) return BadRequest(new { erro = "Informe de 1 a 60 linhas." });
        if (linhas.Any(l => string.IsNullOrWhiteSpace(l.Sku) || l.Sku.Length > 60))
            return BadRequest(new { erro = "SKU invalido." });
        if (linhas.Any(l => l.Quantidade < 1 || l.Quantidade > 1_000))
            return BadRequest(new { erro = "Quantidade invalida." });

        try
        {
            return Ok(await _pedidos.LancarAsync(pedidoId, numero, linhas, ct));
        }
        catch (EcommercePedidoException e)
        {
            return StatusCode(e.Status, new { erro = e.Message });
        }
    }

    /// <summary>Cancela a venda do pedido: devolve o estoque e cancela a Conta a Receber do repasse (se ainda sem baixa).</summary>
    [HttpPost("pedidos/estornar")]
    public async Task<IActionResult> EstornarPedido([FromBody] EstornarPedidoRequest req, CancellationToken ct)
    {
        if (!_opt.Value.LancamentoAtivo)
            return StatusCode(503, new { erro = "Lancamento de vendas do site desligado na API (Ecommerce__LancamentoAtivo)." });

        var pedidoId = (req.PedidoId ?? string.Empty).Trim();
        if (!IdPedidoValido.IsMatch(pedidoId)) return BadRequest(new { erro = "PedidoId invalido." });
        var motivo = (req.Motivo ?? string.Empty).Trim();
        if (motivo.Length > 200) motivo = motivo[..200];

        try
        {
            return Ok(await _pedidos.EstornarAsync(pedidoId, motivo, ct));
        }
        catch (EcommercePedidoException e)
        {
            return StatusCode(e.Status, new { erro = e.Message });
        }
    }
}
