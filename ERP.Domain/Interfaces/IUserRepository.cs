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
    Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId);

    /// <summary>S12: Busca usuario pelo token de confirmacao de cadastro (cross-check e-mail RFB).</summary>
    Task<User?> GetByConfirmacaoTokenAsync(string token);
    Task UpdateLoginAttemptAsync(Guid userId, Guid tenantId, int failedAttempts, DateTime? lockoutEndUtc);
    Task<User?> GetByIdAsync(Guid userId);
    Task UpdatePasswordAsync(Guid userId, Guid tenantId, string newPasswordHash, bool mustChangePassword);

    /// <summary>Controle de versão de sessão (16/09) — incrementa TokenVersion,
    /// invalidando todo token já emitido pra esse usuário (logout, troca de
    /// senha, desativação de conta). Global por usuário: derruba todos os
    /// aparelhos/sessões da pessoa de uma vez, de propósito.</summary>
    Task RevokeSessionsAsync(Guid userId);
}