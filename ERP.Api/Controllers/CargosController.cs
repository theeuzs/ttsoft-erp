using ERP.Api.Security;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CargosController : ControllerBase
{
    private readonly IRoleService _service;
    public CargosController(IRoleService service) => _service = service;

    /// <summary>Lista todos os cargos/roles com suas permissões.</summary>
    [HttpGet]
    [HasPermission(Permissions.UsersView)]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.GetAllAsync());

    /// <summary>Lista todas as permissões disponíveis no sistema.</summary>
    [HttpGet("permissoes")]
    [HasPermission(Permissions.UsersView)]
    public async Task<IActionResult> GetPermissoes()
        => Ok(await _service.GetAllPermissionsAsync());

    /// <summary>
    /// Cria um novo cargo.
    /// Requer role.manage — separado de users.view para evitar escalada de privilégio.
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.RoleManage)]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    {
        try
        {
            var role = await _service.CreateAsync(dto);
            return Ok(role);
        }
        catch (Exception ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Atualiza permissões de um cargo. Requer role.manage.</summary>
    [HttpPut]
    [HasPermission(Permissions.RoleManage)]
    public async Task<IActionResult> Update([FromBody] UpdateRoleDto dto)
    {
        try
        {
            await _service.UpdateAsync(dto);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Remove um cargo. Requer role.manage.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.RoleManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok
            ? NoContent()
            : BadRequest(new { erro = "Não é possível excluir — cargo protegido ou possui usuários vinculados." });
    }
}