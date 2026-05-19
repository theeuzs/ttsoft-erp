using ERP.Domain.Entities;
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> HasAnyAsync();
    Task<IEnumerable<User>> GetAllAsync();
    Task AddAsync(User user);
    Task DeleteAsync(Guid id);
    /// <summary>Persiste tentativas de login falhadas e/ou reset do contador.</summary>
    Task UpdateLoginAttemptAsync(Guid userId, int failedAttempts, DateTime? lockoutEndUtc);
}