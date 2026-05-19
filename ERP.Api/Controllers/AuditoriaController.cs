using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditoriaController : ControllerBase
{
    private readonly IAuditLogService _service;

    public AuditoriaController(IAuditLogService service) => _service = service;

    /// <summary>
    /// Lista logs de auditoria paginados, com filtro por usuário, ação e período.
    /// Toda a lógica de filtro, ordenação e paginação ocorre no banco via IQueryable.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string?   usuario = null,
        [FromQuery] string?   acao    = null,
        [FromQuery] DateTime? de      = null,
        [FromQuery] DateTime? ate     = null,
        [FromQuery] int       pagina  = 1,
        [FromQuery] int       tam     = 50,
        CancellationToken ct          = default)
    {
        var resultado = await _service.GetPagedAsync(usuario, acao, de, ate, pagina, tam, ct);
        return Ok(resultado);
    }
}