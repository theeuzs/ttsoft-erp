using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IConfigLojaService _config;

    public ConfigController(IConfigLojaService config) => _config = config;

    /// <summary>
    /// Config pública da loja — usada pela CalculadoraPublica e CatalogoPublico.
    /// AllowAnonymous: não expõe dados sensíveis, só nome fantasia e telefone.
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic(
        [FromQuery] Guid? tenantId = null, CancellationToken ct = default)
    {
        var (nomeFantasia, telefone) = await _config.GetPublicAsync(tenantId, ct);
        return Ok(new { NomeFantasia = nomeFantasia, Telefone = telefone });
    }

    /// <summary>Retorna as configurações completas da loja (filial matriz).</summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get(CancellationToken ct = default)
        => Ok(await _config.GetAsync(ct));

    /// <summary>Atualiza as configurações da loja.</summary>
    [HasPermission(Permissions.ConfigView)]
    [HttpPut]
    [Authorize]
    public async Task<IActionResult> Put([FromBody] ConfigLojaDto dto, CancellationToken ct = default)
    {
        await _config.PutAsync(dto, ct);
        return Ok(new { mensagem = "Configurações salvas." });
    }
}