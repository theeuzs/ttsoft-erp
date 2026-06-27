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

    public async Task UpdateLoginAttemptAsync(Guid userId, Guid tenantId, int failedAttempts, DateTime? lockoutEndUtc)
    {
        // S10 FIX: tenantId agora vem explícito do AuthService (que o recebe do X-Tenant-CNPJ header).
        // Antes usava GetGlobalTenantId() que retorna Guid.Empty na API → UPDATE batia em 0 linhas
        // silenciosamente → brute-force lockout desligado em produção.
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException(
                $"UpdateLoginAttemptAsync chamado sem tenantId (userId={userId}). " +
                "Brute-force lockout não pode ser registrado sem tenant definido.");

        int rows;
        if (lockoutEndUtc.HasValue)
        {
            var lockoutVal = lockoutEndUtc.Value;
            rows = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Users SET FailedLoginAttempts = {failedAttempts}, LockoutEndUtc = {lockoutVal} WHERE Id = {userId} AND TenantId = {tenantId}");
        }
        else
        {
            rows = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Users SET FailedLoginAttempts = {failedAttempts}, LockoutEndUtc = NULL WHERE Id = {userId} AND TenantId = {tenantId}");
        }

        if (rows == 0)
            throw new InvalidOperationException(
                $"UpdateLoginAttemptAsync afetou 0 linhas (userId={userId}, tenantId={tenantId}). " +
                "Usuário não encontrado ou tenant inválido — investigar.");
    }

    public async Task<User?> GetByIdAsync(Guid userId)
        => await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);

    public async Task UpdatePasswordAsync(Guid userId, string newPasswordHash, bool mustChangePassword)
    {
        // S10 FIX: GetTenantId() (instância) faz cascata correta:
        //   1. _requestTenant.TenantId  (scoped HTTP request na API)
        //   2. _asyncTenantId.Value     (AsyncLocal — flui para CreateScope filhos)
        //   3. _globalTenantId          (estático WPF)
        // Antes usava GetGlobalTenantId() (static) que sempre retorna Guid.Empty na API
        // → troca de senha retornava 200 OK mas não persistia no banco → loop MustChangePassword.
        var tenantId = _context.GetTenantId();

        if (tenantId == Guid.Empty)
            throw new InvalidOperationException(
                "UpdatePasswordAsync chamado sem tenant — chamada fora de fluxo autenticado. " +
                "TenantMiddleware deve ter populado o AsyncLocal antes desta chamada.");

        var agora = DateTime.UtcNow;
        var rows  = await _context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Users SET PasswordHash = {newPasswordHash}, MustChangePassword = {mustChangePassword}, UpdatedAt = {agora} WHERE Id = {userId} AND TenantId = {tenantId}");

        if (rows == 0)
            throw new InvalidOperationException(
                $"UpdatePasswordAsync afetou 0 linhas (userId={userId}, tenantId={tenantId}). " +
                "Usuário não encontrado ou tenant inválido.");
    }
}