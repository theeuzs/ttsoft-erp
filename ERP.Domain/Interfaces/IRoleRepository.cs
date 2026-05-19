using ERP.Domain.Entities;

namespace ERP.Domain.Interfaces;

public interface IRoleRepository
{
    Task<IEnumerable<Role>>       GetAllWithPermissionsAsync();
    Task<IEnumerable<Permission>> GetAllPermissionsAsync();
    Task<Role?>                   GetByIdWithPermissionsAsync(Guid id);
    Task UpdatePermissionsAsync(Guid roleId, List<Guid> permissionIds, decimal maxDiscount, decimal maxSangria, decimal percentualComissao = 0);
    Task<Role>  CreateAsync(string name, decimal maxDiscount, decimal maxSangria, List<Guid> permissionIds);
    Task<bool>  DeleteAsync(Guid id); // false se protegido ou com usuários vinculados
}
