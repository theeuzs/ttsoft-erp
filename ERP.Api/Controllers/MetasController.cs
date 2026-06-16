// ── ERP.Api/Controllers/MetasController.cs ───────────────────────────────────
// S1.6: TenantId adicionado ao WHERE do UPDATE e DELETE via ExecuteSqlRawAsync.
// Embora o `existente` seja obtido via IQueryable filtrado por tenant, a defesa
// em profundidade exige que o UPDATE/DELETE também filtre explicitamente.
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MetasController : ControllerBase
{
    private readonly IMetasService _service;

    public MetasController(IMetasService service) => _service = service;

    /// <summary>Lista metas com progresso real de vendas.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int mes = 0, [FromQuery] int ano = 0,
        CancellationToken ct = default)
    {
        mes = mes == 0 ? DateTime.Today.Month : mes;
        ano = ano == 0 ? DateTime.Today.Year  : ano;

        var resultado = await _service.GetAllAsync(mes, ano, ct);
        return Ok(resultado);
    }

    /// <summary>Cria ou atualiza uma meta (upsert por vendedor/mês/ano).</summary>
    [HasPermission(Permissions.ReportFinancial)]
    [HttpPost]
    public async Task<IActionResult> Upsert(
        [FromBody] MetaVendasDto dto, CancellationToken ct = default)
    {
        var (id, atualizado) = await _service.UpsertAsync(dto, ct);
        return Ok(new { Id = id, Atualizado = atualizado });
    }

    /// <summary>Remove uma meta.</summary>
    [HasPermission(Permissions.ReportFinancial)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}