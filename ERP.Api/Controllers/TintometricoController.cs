using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TintometricoController : ControllerBase
{
    private readonly ITintometricoService _service;
    public TintometricoController(ITintometricoService service) => _service = service;

    /// <summary>Lista todas as fórmulas do tenant com nome do produto.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? busca = null,
        CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(busca, ct));

    /// <summary>Busca fórmula por produto.</summary>
    [HttpGet("produto/{productId:guid}")]
    public async Task<IActionResult> GetByProduct(Guid productId, CancellationToken ct = default)
    {
        var f = await _service.GetByProductAsync(productId, ct);
        return f is null ? NotFound() : Ok(f);
    }

    /// <summary>Cria ou atualiza fórmula de um produto (upsert).</summary>
    [HasPermission(Permissions.ProductEdit)]
    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] SalvarFormulaDto dto, CancellationToken ct = default)
    {
        try
        {
            await _service.UpsertAsync(dto, ct);
            return Ok(new { mensagem = "Fórmula salva." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { erro = ex.Message });
        }
    }

    /// <summary>Remove fórmula de um produto.</summary>
    [HasPermission(Permissions.ProductEdit)]
    [HttpDelete("produto/{productId:guid}")]
    public async Task<IActionResult> Delete(Guid productId, CancellationToken ct = default)
    {
        var removeu = await _service.DeleteAsync(productId, ct);
        if (!removeu) return NotFound();
        return Ok(new { mensagem = "Fórmula removida." });
    }

    /// <summary>
    /// Calcula quantidade de tinta para uma área, baseado na fórmula cadastrada.
    /// Retorna litros necessários, número de latas e custo estimado.
    /// </summary>
    [HttpGet("calcular/{productId:guid}")]
    public async Task<IActionResult> Calcular(
        Guid    productId,
        [FromQuery] decimal areaM2,
        [FromQuery] int     demaos = 0,
        CancellationToken ct = default)
    {
        try
        {
            var resultado = await _service.CalcularAsync(productId, areaM2, demaos, ct);
            if (resultado is null)
                return NotFound(new { erro = "Produto sem fórmula cadastrada." });

            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }
}