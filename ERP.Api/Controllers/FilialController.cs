using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilialController : ControllerBase
{
    private readonly ITransferenciaService _service;
    public FilialController(ITransferenciaService service) => _service = service;

    /// <summary>Lista todas as filiais ativas.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetFilialAsync());

    /// <summary>Lista transferências de uma filial.</summary>
    [HttpGet("{id:guid}/transferencias")]
    public async Task<IActionResult> GetTransferencias(Guid id)
        => Ok(await _service.GetByFilialAsync(id));

    /// <summary>Cria uma transferência de estoque entre filiais.</summary>
    [HttpPost("transferencias")]
    public async Task<IActionResult> CriarTransferencia([FromBody] CriarTransferenciaDto dto)
    {
        try
        {
            var t = await _service.CriarAsync(dto);
            return Ok(new { t.Id, t.Status, Mensagem = "Transferência criada em rascunho." });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { erro = ex.Message }); }
    }

    /// <summary>Confirma transferência — debita origem e credita destino.</summary>
    [HttpPost("transferencias/{id:guid}/confirmar")]
    public async Task<IActionResult> Confirmar(Guid id, [FromBody] string operador)
    {
        try
        {
            await _service.ConfirmarAsync(id, operador);
            return Ok(new { Mensagem = "Transferência confirmada. Estoques atualizados." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { erro = ex.Message }); }
    }

    /// <summary>Cancela uma transferência.</summary>
    [HttpPost("transferencias/{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] string motivo)
    {
        try
        {
            await _service.CancelarAsync(id, motivo);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { erro = ex.Message }); }
    }
}
