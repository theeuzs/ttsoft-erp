using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/contas-pagar")]
[Authorize]
public class ContasPagarController : ControllerBase
{
    private readonly IContaPagarService _service;

    public ContasPagarController(IContaPagarService service) => _service = service;

    /// <summary>Lista contas a pagar pendentes.</summary>
    [HttpGet("pendentes")]
    public async Task<IActionResult> GetPendentes(CancellationToken ct = default)
        => Ok(await _service.GetPendentesAsync(ct));

    /// <summary>Lista contas a pagar vencidas.</summary>
    [HttpGet("vencidas")]
    public async Task<IActionResult> GetVencidas(CancellationToken ct = default)
        => Ok(await _service.GetVencidasAsync(ct));

    /// <summary>Resumo financeiro de contas a pagar.</summary>
    [HttpGet("resumo")]
    public async Task<IActionResult> GetResumo(CancellationToken ct = default)
        => Ok(await _service.GetResumoAsync(ct));

    /// <summary>Cria nova conta a pagar.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateContaPagarDto dto, CancellationToken ct = default)
        => Ok(await _service.CreateAsync(dto, ct));

    /// <summary>Registra pagamento de uma conta.</summary>
    [HttpPost("{id:guid}/pagar")]
    public async Task<IActionResult> Pagar(Guid id, CancellationToken ct = default)
    {
        await _service.PagarAsync(id, ct);
        return Ok(new { mensagem = "Conta paga com sucesso." });
    }

    /// <summary>Cancela uma conta a pagar.</summary>
    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, CancellationToken ct = default)
    {
        await _service.CancelarAsync(id, ct);
        return NoContent();
    }
}