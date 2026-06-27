using ERP.Domain.Entities;
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public interface IUserRepository
{
    /// <summary>
    /// Busca usuário filtrando por username E tenant.
    /// Usar apenas em login — bypassa HasQueryFilter (que depende de JWT ainda inexistente)
    /// mas garante isolamento explícito via TenantId.
    /// </summary>
    Task<User?> GetByUsernameAndTenantAsync(string username, Guid tenantId);

    /// <summary>Mantido para compatibilidade com EnsureDefaultAdminCreatedAsync.</summary>
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> HasAnyAsync();
    Task<IEnumerable<User>> GetAllAsync();
    Task AddAsync(User user);
    Task DeleteAsync(Guid id);
    /// <summary>Persiste tentativas de login falhadas e/ou reset do contador.</summary>
    Task UpdateLoginAttemptAsync(Guid userId, Guid tenantId, int failedAttempts, DateTime? lockoutEndUtc);
    Task<User?> GetByIdAsync(Guid userId);
    Task UpdatePasswordAsync(Guid userId, string newPasswordHash, bool mustChangePassword);
}