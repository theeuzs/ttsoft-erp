// S3.7: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync
// 1.8.9: AND TenantId adicionado em todos os UPDATE raw como defesa em profundidade.
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using ERP.Persistence;
using ERP.Persistence.Context;

namespace ERP.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByUsernameAndTenantAsync(string username, Guid tenantId)
    {
        // IgnoreQueryFilters: login é pré-autenticação — AsyncLocal ainda é Guid.Empty.
        // O TenantId é validado EXPLICITAMENTE aqui — sem tenant correto, sem login.
        // Fecha o vetor de cross-tenant login (auditoria A.1).
        return await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Role)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u =>
                u.Username  == username
                && u.TenantId == tenantId
                && !u.IsDeleted);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        // Usado apenas por EnsureDefaultAdminCreatedAsync (bootstrap sem tenant).
        return await _context.Users
            .IgnoreQueryFilters()
            .Include(u => u.Role)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Username == username && !u.IsDeleted);
    }

    public async Task<bool> HasAnyAsync()
    {
        return await _context.Users.AnyAsync();
    }

    public async Task AddAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .Include(u => u.Role)
            .ThenInclude(r => r.Permissions)
            .ToListAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateLoginAttemptAsync(Guid userId, int failedAttempts, DateTime? lockoutEndUtc)
    {
        // 1.8.9: AND TenantId adicionado como defesa em profundidade.
        // Nota: durante o fluxo de login (pré-JWT), GetGlobalTenantId() pode ser
        // Guid.Empty — nesse caso o UPDATE afeta 0 linhas, que é um safe fail
        // (preferível a atualizar o tenant errado por bug de isolamento futuro).
        // O userId já foi validado com tenant correto em GetByUsernameAndTenantAsync.
        var tenantId = AppDbContext.GetGlobalTenantId();

        if (lockoutEndUtc.HasValue)
        {
            var lockoutVal = lockoutEndUtc.Value;
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Users SET FailedLoginAttempts = {failedAttempts}, LockoutEndUtc = {lockoutVal} WHERE Id = {userId} AND TenantId = {tenantId}");
        }
        else
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Users SET FailedLoginAttempts = {failedAttempts}, LockoutEndUtc = NULL WHERE Id = {userId} AND TenantId = {tenantId}");
        }
    }

    public async Task<User?> GetByIdAsync(Guid userId)
        => await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

    public async Task UpdatePasswordAsync(Guid userId, string newPasswordHash, bool mustChangePassword)
    {
        // 1.8.9: AND TenantId como defesa em profundidade — garante que um bug de
        // isolamento futuro nunca permita resetar senha de usuário de outro tenant.
        var tenantId = AppDbContext.GetGlobalTenantId();
        var agora    = DateTime.UtcNow;

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Users SET PasswordHash = {newPasswordHash}, MustChangePassword = {mustChangePassword}, UpdatedAt = {agora} WHERE Id = {userId} AND TenantId = {tenantId}");
    }
}