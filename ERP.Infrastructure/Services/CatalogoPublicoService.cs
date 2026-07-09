// ── ERP.Infrastructure/Services/CatalogoPublicoService.cs ────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class CatalogoPublicoService : ICatalogoPublicoService
{
    private readonly AppDbContext _ctx;
    public CatalogoPublicoService(AppDbContext ctx) => _ctx = ctx;

    public async Task<CatalogoResultadoDto?> GetCatalogoAsync(
        int page, int pageSize, string? search, string? categoria, Guid? tenantId,
        CancellationToken ct = default)
    {
        // S10 FIX: filtros aplicados no banco, não em memória após paginação.
        // Antes: GetPagedAsync(24 itens) → filtrar em memória → TotalItems = count(página) ← errado.
        // Agora: filtros no IQueryable → Skip/Take → TotalItems = count(antes do skip) ← correto.

        var query = _ctx.Products.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted);

        // ── S11 FIX: opt-in obrigatório para acesso anônimo a tenant explícito ──
        // Antes: qualquer tenantId (derivável de CNPJ público via SHA-256) retornava
        // catálogo completo com preço e estoque — vazamento de inteligência comercial
        // (concorrente raspa preços/estoque/portfólio de qualquer cliente TTSoft).
        // Agora: tenant precisa habilitar explicitamente CatalogoPublicoHabilitado
        // na filial matriz. Sem isso, null — controller retorna 404, não revela
        // se o tenant existe ou não.
        bool mostrarPreco   = false;
        bool mostrarEstoque = false;

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            var matriz = await _ctx.Branches.AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.TenantId == tenantId.Value && b.IsMatriz, ct);

            if (matriz is null || !matriz.CatalogoPublicoHabilitado)
                return null;

            mostrarPreco   = matriz.CatalogoMostrarPreco;
            mostrarEstoque = matriz.CatalogoMostrarEstoque;

            query = query.IgnoreQueryFilters()
                         .Where(p => p.IsActive && !p.IsDeleted && p.TenantId == tenantId.Value);
        }

        // Filtra com estoque disponível
        query = query.Where(p => p.Stock > 0);

        // Busca por nome ou código de barras
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Barcode.Contains(search));

        // Filtro por categoria
        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(p => p.Category != null && p.Category.Name == categoria);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new CatalogoItemDto(
                p.Id,
                p.Name,
                p.Category != null ? p.Category.Name : "",
                p.Barcode,
                p.Unit,
                // S11 FIX: preço e estoque só aparecem se o tenant optou explicitamente
                // (CatalogoMostrarPreco / CatalogoMostrarEstoque). Tenant autenticado
                // (sem tenantId explícito — chamada interna) sempre vê tudo.
                (!tenantId.HasValue || mostrarPreco) ? (decimal?)p.SalePrice : null,
                (!tenantId.HasValue || mostrarEstoque) ? (decimal?)p.Stock : null,
                (string?)null))
            .ToListAsync(ct);

        return new CatalogoResultadoDto(items, total, page, pageSize);
    }
}
