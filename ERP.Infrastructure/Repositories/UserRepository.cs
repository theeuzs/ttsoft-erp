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

    public async Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId)
        => await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u =>
                u.Email == email &&
                u.TenantId == tenantId &&
                u.IsActive &&
                !u.IsDeleted);

    // S12: busca por token de confirmação (cross-check e-mail RFB)
    public async Task<User?> GetByConfirmacaoTokenAsync(string token)
        => await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.ConfirmacaoToken == token && !u.IsDeleted);

    public async Task UpdateLoginAttemptAsync(Guid userId, Guid tenantId, int failedAttempts, DateTime? lockoutEndUtc)
    {
        // S10 FIX: tenantId agora vem explícito do AuthService.
        // Refatorado de ExecuteSqlInterpolatedAsync para EF Core load+save —
        // permite testes com InMemory DB (raw SQL não funciona com InMemory).
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException(
                $"UpdateLoginAttemptAsync chamado sem tenantId (userId={userId}). " +
                "Brute-force lockout não pode ser registrado sem tenant definido.");

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted);

        if (user == null)
            throw new InvalidOperationException(
                $"UpdateLoginAttemptAsync: usuário não encontrado (userId={userId}, tenantId={tenantId}).");

        user.FailedLoginAttempts = failedAttempts;
        user.LockoutEndUtc       = lockoutEndUtc;
        await _context.SaveChangesAsync();
    }

    public async Task UpdatePasswordAsync(Guid userId, Guid tenantId, string newPasswordHash, bool mustChangePassword)
    {
        // S12 FIX: tenantId agora vem explícito do caller.
        // Antes: _context.GetTenantId() -> Guid.Empty em fluxo anônimo (reset-password)
        // -> InvalidOperationException -> 500 em produção.
        // Mesmo padrão do UpdateLoginAttemptAsync (S10 N1).
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException(
                $"UpdatePasswordAsync chamado sem tenantId (userId={userId}). " +
                "Senha não pode ser atualizada sem tenant definido.");

        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId && !u.IsDeleted);

        if (user == null)
            throw new InvalidOperationException(
                $"UpdatePasswordAsync: usuário não encontrado (userId={userId}, tenantId={tenantId}).");

        user.PasswordHash        = newPasswordHash;
        user.MustChangePassword  = mustChangePassword;
        user.UpdatedAt           = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<User?> GetByIdAsync(Guid userId)
        => await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
}