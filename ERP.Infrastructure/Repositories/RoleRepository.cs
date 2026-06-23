// S3.7: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync
// 1.8.9: verificação de ownership por tenant antes dos DELETE/INSERT raw.
//        PermissionRole não tem TenantId — proteção via check no Role pai.
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _ctx;

    public RoleRepository(AppDbContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<Role>> GetAllWithPermissionsAsync()
    {
        return await _ctx.Roles
            .Include(r => r.Permissions)
            .Include(r => r.Users)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Permission>> GetAllPermissionsAsync()
    {
        return (await _ctx.Permissions
            .OrderBy(p => p.Description)
            .ToListAsync())
            .DistinctBy(p => p.Code)
            .ToList();
    }

    public async Task<Role?> GetByIdWithPermissionsAsync(Guid id)
    {
        return await _ctx.Roles
            .Include(r => r.Permissions)
            .Include(r => r.Users)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task UpdatePermissionsAsync(
        Guid roleId, List<Guid> permissionIds,
        decimal maxDiscount, decimal maxSangria,
        decimal percentualComissao = 0)
    {
        // 1.8.9: PermissionRole não tem coluna TenantId (tabela de junção pura).
        // Defesa em profundidade: verifica que o roleId pertence ao tenant atual
        // ANTES dos DELETE/INSERT raw — impede que bug futuro de isolamento permita
        // editar permissões de roles de outros tenants via IDOR neste endpoint.
        var tenantId  = AppDbContext.GetGlobalTenantId();
        var roleExiste = await _ctx.Roles
            .IgnoreQueryFilters()
            .AnyAsync(r => r.Id == roleId && r.TenantId == tenantId);

        if (!roleExiste) return; // role não pertence a este tenant — rejeição silenciosa

        var role = await _ctx.Roles
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null) return;

        role.MaxDiscountPercentage = maxDiscount;
        role.MaxSangriaValue       = maxSangria;
        role.PercentualComissao    = percentualComissao;
        role.UpdatedAt             = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();

        // S3.7: ExecuteSqlInterpolatedAsync — safe by design
        await _ctx.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM PermissionRole WHERE RolesId = {roleId}");

        foreach (var permId in permissionIds)
        {
            await _ctx.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO PermissionRole (RolesId, PermissionsId) VALUES ({roleId}, {permId})");
        }
    }

    public async Task<Role> CreateAsync(
        string name, decimal maxDiscount, decimal maxSangria,
        List<Guid> permissionIds)
    {
        var tenantId = AppDbContext.GetGlobalTenantId();

        var permsToAssign = await _ctx.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .ToListAsync();

        var role = new Role
        {
            Id                    = Guid.NewGuid(),
            TenantId              = tenantId,
            Name                  = name,
            MaxDiscountPercentage = maxDiscount,
            MaxSangriaValue       = maxSangria,
            Permissions           = permsToAssign
        };

        _ctx.Roles.Add(role);
        await _ctx.SaveChangesAsync();
        _ctx.ChangeTracker.Clear();
        return role;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var role = await _ctx.Roles
            .Include(r => r.Users)
            .AsTracking()
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return false;
        if (role.Name.Equals("Administrador", StringComparison.OrdinalIgnoreCase)) return false;
        if (role.Users.Any()) return false;

        _ctx.Roles.Remove(role);
        await _ctx.SaveChangesAsync();
        return true;
    }
}