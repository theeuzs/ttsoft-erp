// S3.7: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync
// 1.8.9: verificação de ownership por tenant antes dos DELETE/INSERT raw.
//        PermissionRole não tem TenantId — proteção via check no Role pai.
// TENANT FIX: AppDbContext.GetGlobalTenantId() → IRequestTenant.TenantId.
//        Antes: GetGlobalTenantId() retorna Guid.Empty na API (só é setado pelo
//        WPF no login), fazendo CreateAsync gravar cargos com TenantId=Guid.Empty
//        (órfãos, somem da listagem) e UpdatePermissionsAsync falhar o check de
//        ownership sempre (roleExiste nunca é true) — editar permissões pela
//        API/Portal não tinha efeito nenhum, silenciosamente.
//        Mesmo padrão já corrigido em DevolucaoRepository (Fase 1.5).
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _ctx;
    private readonly ERP.Application.Interfaces.IRequestTenant _tenant;

    public RoleRepository(AppDbContext ctx, ERP.Application.Interfaces.IRequestTenant tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

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
        // PROVIDER FIX: troca do DELETE/INSERT raw em PermissionRole pelo grafo
        // de entidade rastreada (Include + Clear/Add), mesmo idioma já usado em
        // CreateAsync. O SQL raw funcionava em SQL Server real, mas lança
        // InvalidOperationException no provider InMemory usado pelos testes de
        // integração ("Relational-specific methods can only be used when the
        // context is using a relational database provider") — o teste de PUT só
        // revelou isso porque o tenant fix abaixo parou de retornar cedo demais.
        // Resolve os dois problemas: funciona em qualquer provider e elimina SQL
        // raw numa tabela de junção simples que o EF já modela nativamente.
        //
        // 1.8.9 (mantido): verifica ownership por tenant ANTES de aplicar a
        // alteração — impede editar permissões de role de outro tenant via IDOR.
        var tenantId = _tenant.TenantId;

        var role = await _ctx.Roles
            .IgnoreQueryFilters()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role is null || role.TenantId != tenantId) return; // role não pertence a este tenant — rejeição silenciosa

        var novasPermissoes = await _ctx.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .ToListAsync();

        role.Permissions.Clear();
        foreach (var perm in novasPermissoes)
            role.Permissions.Add(perm);

        role.MaxDiscountPercentage = maxDiscount;
        role.MaxSangriaValue       = maxSangria;
        role.PercentualComissao    = percentualComissao;
        role.UpdatedAt             = DateTime.UtcNow;

        await _ctx.SaveChangesAsync();
    }

    public async Task<Role> CreateAsync(
        string name, decimal maxDiscount, decimal maxSangria,
        List<Guid> permissionIds)
    {
        var tenantId = _tenant.TenantId;

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