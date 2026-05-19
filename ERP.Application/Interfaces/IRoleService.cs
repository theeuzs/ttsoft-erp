using ERP.Application.DTOs;

namespace ERP.Application.Interfaces;

public interface IRoleService
{
    Task<IEnumerable<RoleDto>>       GetAllAsync();
    Task<IEnumerable<PermissionDto>> GetAllPermissionsAsync();
    Task                             UpdateAsync(UpdateRoleDto dto);
    Task<RoleDto>                    CreateAsync(CreateRoleDto dto);
    /// <summary>
    /// Retorna false se o cargo for protegido (Administrador) ou tiver usuários vinculados.
    /// </summary>
    Task<bool> DeleteAsync(Guid id);
}
