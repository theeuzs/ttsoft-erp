using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;

namespace ERP.Application.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _repo;

    public RoleService(IRoleRepository repo) => _repo = repo;

    public async Task<IEnumerable<RoleDto>> GetAllAsync()
    {
        var roles = await _repo.GetAllWithPermissionsAsync();
        return roles.Select(r => new RoleDto
        {
            Id                   = r.Id,
            Name                 = r.Name,
            MaxDiscountPercentage = r.MaxDiscountPercentage,
            MaxSangriaValue      = r.MaxSangriaValue,
            PermissionCodes      = r.Permissions.Select(p => p.Code).ToList(),
            PermissionIds        = r.Permissions.Select(p => p.Id).ToList(),
            UserCount            = r.Users.Count,
            IsProtected          = r.Name.Equals("Administrador", StringComparison.OrdinalIgnoreCase),
            PercentualComissao   = r.PercentualComissao
        });
    }

    public async Task<IEnumerable<PermissionDto>> GetAllPermissionsAsync()
    {
        var perms = await _repo.GetAllPermissionsAsync();
        return perms.Select(p => new PermissionDto
        {
            Id          = p.Id,
            Code        = p.Code,
            Description = p.Description,
            Group       = ResolverGrupo(p.Code)
        }).OrderBy(p => p.Group).ThenBy(p => p.Description);
    }

    public async Task UpdateAsync(UpdateRoleDto dto)
        => await _repo.UpdatePermissionsAsync(
            dto.Id, dto.PermissionIds,
            dto.MaxDiscountPercentage, dto.MaxSangriaValue,
            dto.PercentualComissao);

    public async Task<RoleDto> CreateAsync(CreateRoleDto dto)
    {
        var role = await _repo.CreateAsync(
            dto.Name, dto.MaxDiscountPercentage,
            dto.MaxSangriaValue, dto.PermissionIds);

        return new RoleDto
        {
            Id                   = role.Id,
            Name                 = role.Name,
            MaxDiscountPercentage = role.MaxDiscountPercentage,
            MaxSangriaValue      = role.MaxSangriaValue,
            PermissionCodes      = new(),
            PermissionIds        = new(),
            UserCount            = 0,
            IsProtected          = false
        };
    }

    public async Task<bool> DeleteAsync(Guid id) => await _repo.DeleteAsync(id);

    // ── Agrupamento de permissões por módulo (para exibição na UI) ──────────
    private static string ResolverGrupo(string code) => code switch
    {
        _ when code.StartsWith("sale.")       => "PDV / Vendas",
        _ when code.StartsWith("cash.")       => "Caixa",
        _ when code.StartsWith("product.")    => "Produtos",
        _ when code.StartsWith("stock.")      => "Estoque",
        _ when code.StartsWith("haver.")      => "Clientes",
        _ when code.StartsWith("report.")     => "Financeiro",
        _ when code.StartsWith("financeiro.") => "Financeiro",
        _ when code.StartsWith("despesas.")   => "Financeiro",
        _ when code.StartsWith("fluxocaixa.") => "Financeiro",
        _ when code.StartsWith("margem.")     => "Financeiro",
        _ when code.StartsWith("audit.")      => "Relatórios",
        _ when code.StartsWith("compras.")    => "Compras",
        _ when code.StartsWith("inventario.") => "Estoque",
        _ when code.StartsWith("notasfiscais.") => "Fiscal",
        _ when code.StartsWith("users.")      => "Administração",
        _ when code.StartsWith("config.")     => "Administração",
        _                                     => "Outros"
    };
}
