// ── ERP.Infrastructure/Services/ConfigLojaService.cs ─────────────────────────
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class ConfigLojaService : IConfigLojaService
{
    private readonly AppDbContext   _ctx;
    private readonly IRequestTenant _tenant;

    public ConfigLojaService(AppDbContext ctx, IRequestTenant tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<(string NomeFantasia, string Telefone)> GetPublicAsync(
        Guid? tenantId, CancellationToken ct = default)
    {
        // Quando chamado sem JWT, o tenantId vem como query param (calculadora pública)
        var tid = tenantId ?? _tenant.TenantId;
        if (tid == Guid.Empty) return ("Loja", "");

        // S15 FIX: bug real encontrado durante a refatoração — TenantMiddleware só
        // popula IRequestTenant/AsyncLocal quando a requisição é autenticada
        // (context.User.Identity.IsAuthenticated == true). Numa chamada anônima
        // (é exatamente o caso deste endpoint, [AllowAnonymous]), o filtro global
        // HasQueryFilter de Branch (TenantId == tenantFilter.Value) fica travado
        // em Guid.Empty. Como o filtro global e o .Where(tid) explícito se combinam
        // com AND, a query nunca encontrava a filial de verdade — SEMPRE caía no
        // fallback "Loja", não importa qual tenantId fosse passado na query string.
        // Isso quebrava Calculadora Pública e Catálogo Público na prática.
        // IgnoreQueryFilters() + filtro explícito por tid resolve — aqui é
        // deliberado (o tid já vem validado pelo caller), não um bypass acidental.
        var branch = await _ctx.Branches
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => b.TenantId == tid && b.IsMatriz)
            .Select(b => new { b.Name, b.Telefone })
            .FirstOrDefaultAsync(ct);

        return (branch?.Name ?? "Loja", branch?.Telefone ?? "");
    }

    public async Task<ConfigLojaDto> GetAsync(CancellationToken ct = default)
    {
        var branch = await _ctx.Branches
            .AsNoTracking()
            .Where(b => b.IsMatriz)
            .FirstOrDefaultAsync(ct);

        if (branch is null) return new ConfigLojaDto();

        return new ConfigLojaDto
        {
            Id       = branch.Id,
            Nome     = branch.Name,
            CNPJ     = branch.CNPJ ?? "",
            Endereco = branch.Endereco ?? "",
            Telefone = branch.Telefone ?? ""
        };
    }

    public async Task PutAsync(ConfigLojaDto dto, CancellationToken ct = default)
    {
        var branch = await _ctx.Branches
            .Where(b => b.IsMatriz)
            .FirstOrDefaultAsync(ct);

        if (branch is null)
        {
            // Cria a filial matriz se não existir
            branch = new Branch
            {
                Id       = Guid.NewGuid(),
                IsMatriz = true,
                IsActive = true
            };
            _ctx.Branches.Add(branch);
        }

        branch.Name     = dto.Nome;
        branch.CNPJ     = dto.CNPJ;
        branch.Endereco = dto.Endereco;
        branch.Telefone = dto.Telefone;

        await _ctx.SaveChangesAsync(ct);
    }
}
