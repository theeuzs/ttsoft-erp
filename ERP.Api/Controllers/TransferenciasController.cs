using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TransferenciasController : ControllerBase
{
    private readonly ITransferenciaService _service;

    public TransferenciasController(ITransferenciaService service) => _service = service;

    /// <summary>Lista transferências de uma filial (origem ou destino).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? filialId = null)
    {
        if (filialId.HasValue)
        {
            var lista = await _service.GetByFilialAsync(filialId.Value);
            return Ok(lista.Select(MapDto));
        }

        // Sem filtro: retorna todas as filiais disponíveis para o selector
        var filiais = await _service.GetFilialAsync();
        return Ok(filiais.Select(f => new { f.Id, f.Name }));
    }

    /// <summary>Lista filiais disponíveis.</summary>
    [HttpGet("filiais")]
    public async Task<IActionResult> GetFiliais()
    {
        var filiais = await _service.GetFilialAsync();
        return Ok(filiais.Select(f => new { f.Id, f.Name }));
    }

    /// <summary>Cria uma nova transferência (status Rascunho).</summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarTransferenciaRequest req)
    {
        try
        {
            var operador = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                        ?? User.FindFirst("name")?.Value ?? "Portal";

            var dto = new CriarTransferenciaDto
            {
                OrigemId     = req.OrigemId,
                DestinoId    = req.DestinoId,
                OperadorNome = operador,
                Observacao   = req.Observacao,
                Itens        = req.Itens.Select(i => (i.ProductId, i.Quantidade)).ToList()
            };

            var result = await _service.CriarAsync(dto);
            return Ok(MapDto(result));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { erro = ex.Message }); }
    }

    /// <summary>Confirma a transferência — debita origem e credita destino.</summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPut("{id:guid}/confirmar")]
    public async Task<IActionResult> Confirmar(Guid id)
    {
        try
        {
            var operador = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Portal";
            var result   = await _service.ConfirmarAsync(id, operador);
            return Ok(MapDto(result));
        }
        catch (KeyNotFoundException)            { return NotFound(); }
        catch (InvalidOperationException ex)    { return BadRequest(new { erro = ex.Message }); }
    }

    /// <summary>Cancela a transferência.</summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPut("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarRequest req)
    {
        try
        {
            await _service.CancelarAsync(id, req.Motivo);
            return NoContent();
        }
        catch (KeyNotFoundException)            { return NotFound(); }
        catch (InvalidOperationException ex)    { return BadRequest(new { erro = ex.Message }); }
    }

    // ── Mapeamento ────────────────────────────────────────────────────────────
    private static object MapDto(ERP.Domain.Entities.TransferenciaEstoque t) => new
    {
        t.Id,
        Origem       = t.Origem?.Name  ?? t.OrigemId.ToString(),
        Destino      = t.Destino?.Name ?? t.DestinoId.ToString(),
        t.OrigemId,
        t.DestinoId,
        t.Status,
        StatusTexto  = t.Status.ToString(),
        t.DataTransferencia,
        t.OperadorNome,
        t.Observacao,
        Itens = t.Itens.Select(i => new
        {
            i.ProductId,
            Produto    = i.Product?.Name ?? i.ProductId.ToString(),
            i.Quantidade
        })
    };
}

public class CriarTransferenciaRequest
{
    public Guid    OrigemId   { get; set; }
    public Guid    DestinoId  { get; set; }
    public string? Observacao  { get; set; }
    public List<TransferenciaItemRequest> Itens { get; set; } = new();
}

public class TransferenciaItemRequest
{
    public Guid    ProductId  { get; set; }
    public decimal Quantidade { get; set; }
}

public class CancelarRequest
{
    public string Motivo { get; set; } = string.Empty;
}
