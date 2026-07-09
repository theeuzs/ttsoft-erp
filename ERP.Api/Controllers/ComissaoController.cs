using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComissaoController : ControllerBase
{
    private readonly IComissaoRelatorioService _comissao;

    public ComissaoController(IComissaoRelatorioService comissao) => _comissao = comissao;

    /// <summary>
    /// Calcula comissões por vendedor no período.
    /// A taxa de comissão vem do campo PercentualComissao do cargo (Role) do usuário.
    /// </summary>
    [HasPermission(Permissions.ReportFinancial)]
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? inicio = null,
        [FromQuery] DateTime? fim    = null,
        CancellationToken ct = default)
    {
        var resultado = await _comissao.CalcularComissoesAsync(inicio, fim, ct);
        return Ok(resultado);
    }
}