// I.1 — 4 endpoints [AllowAnonymous] corrigidos para [Authorize].
// GET /fidelidade/{id}/saldo e /historico expostos permitiam enumerar saldos
// de qualquer cliente conhecendo apenas o GUID — sem autenticação.
// O AutoAtendimento.razor usa esses endpoints via ApiClient autenticado
// (token injetado via ILocalStorageService após login por CPF).
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FidelidadeController : ControllerBase
{
    private readonly IFidelidadeService _service;
    public FidelidadeController(IFidelidadeService service) => _service = service;

    /// <summary>Saldo de pontos do cliente.</summary>
    [HttpGet("{customerId:guid}/saldo")]
    // I.1: era [AllowAnonymous] — qualquer um com o GUID acessava o saldo
    public async Task<IActionResult> GetSaldo(Guid customerId)
    {
        var saldo = await _service.GetSaldoAsync(customerId);
        return Ok(new { customerId, saldo, valorEquivalente = saldo * 0.01m });
    }

    /// <summary>Histórico de pontos do cliente.</summary>
    [HttpGet("{customerId:guid}/historico")]
    // I.1: era [AllowAnonymous]
    public async Task<IActionResult> GetHistorico(Guid customerId,
        [FromQuery] int pagina = 1, [FromQuery] int pageSize = 20)
    {
        var hist = await _service.GetHistoricoAsync(customerId, pagina, pageSize);
        return Ok(hist);
    }

    /// <summary>Resgata pontos — chamado pelo PDV ao finalizar venda com desconto fidelidade.</summary>
    [HasPermission(Permissions.FidelidadeUse)]
    [HttpPost("{customerId:guid}/resgatar")]
    public async Task<IActionResult> Resgatar(Guid customerId, [FromBody] ResgatarPontosRequest req)
    {
        try
        {
            var desconto = await _service.ResgatarPontosAsync(customerId, req.Pontos, req.Descricao);
            return Ok(new { pontos = req.Pontos, desconto });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }
}

public record ResgatarPontosRequest(int Pontos, string Descricao = "Resgate PDV");