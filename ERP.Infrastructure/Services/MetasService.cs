// ── ERP.Infrastructure/Services/MetasService.cs ──────────────────────────────
// S3.7: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync (safety by design)
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class MetasService : IMetasService
{
    private readonly AppDbContext   _ctx;
    private readonly IRequestTenant _tenant;

    public MetasService(AppDbContext ctx, IRequestTenant tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<MetaProgressoDto>> GetAllAsync(
        int mes, int ano, CancellationToken ct = default)
    {
        var tenantId  = _tenant.TenantId;
        var inicioMes = new DateTime(ano, mes, 1);
        var fimMes    = inicioMes.AddMonths(1).AddTicks(-1);

        var metas = await _ctx.MetasVendas
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.Mes == mes && m.Ano == ano)
            .OrderBy(m => m.VendedorNome)
            .ToListAsync(ct);

        var vendas = await _ctx.Sales
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId
                     && s.SaleDate >= inicioMes
                     && s.SaleDate <= fimMes
                     && s.Status   != SaleStatus.Cancelada)
            .GroupBy(s => s.SellerName ?? "Desconhecido")
            .Select(g => new { Vendedor = g.Key, Total = g.Sum(s => s.Total) })
            .ToListAsync(ct);

        var totalVendas = vendas.Sum(v => v.Total);

        return metas.Select(m =>
        {
            var realizado = m.VendedorNome == "Geral"
                ? totalVendas
                : vendas.FirstOrDefault(v => v.Vendedor == m.VendedorNome)?.Total ?? 0;

            var pct = m.ValorMeta > 0 ? realizado / m.ValorMeta * 100 : 0;

            return new MetaProgressoDto
            {
                Id           = m.Id,
                VendedorNome = m.VendedorNome,
                Mes          = m.Mes,
                Ano          = m.Ano,
                ValorMeta    = m.ValorMeta,
                Descricao    = m.Descricao,
                Realizado    = realizado,
                Percentual   = Math.Round(pct, 1),
                Restante     = Math.Max(0, m.ValorMeta - realizado),
                Atingida     = realizado >= m.ValorMeta
            };
        }).ToList();
    }

    public async Task<(Guid Id, bool Atualizado)> UpsertAsync(
        MetaVendasDto dto, CancellationToken ct = default)
    {
        var tenantId = _tenant.TenantId;

        var existente = await _ctx.MetasVendas
            .FirstOrDefaultAsync(m => m.TenantId    == tenantId
                                   && m.Mes          == dto.Mes
                                   && m.Ano          == dto.Ano
                                   && m.VendedorNome == dto.VendedorNome, ct);

        if (existente is not null)
        {
            var descricao  = dto.Descricao ?? "";
            var updatedAt  = DateTime.UtcNow;
            var valorMeta  = dto.ValorMeta;
            var existenteId = existente.Id;
            // Fase 1.5: AND TenantId= adicionado por defesa em profundidade.
            // O existenteId já foi carregado via LINQ com HasQueryFilter (tenant filtrado),
            // mas adicionar TenantId no SQL explícito é mais seguro que confiar só no EF.
            await _ctx.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE MetasVendas SET ValorMeta={valorMeta}, Descricao={descricao}, UpdatedAt={updatedAt} WHERE Id={existenteId} AND TenantId={tenantId}");

            return (existente.Id, Atualizado: true);
        }

        var meta = new MetaVendas
        {
            TenantId     = tenantId,
            VendedorNome = dto.VendedorNome,
            Mes          = dto.Mes,
            Ano          = dto.Ano,
            ValorMeta    = dto.ValorMeta,
            Descricao    = dto.Descricao
        };

        _ctx.MetasVendas.Add(meta);
        await _ctx.SaveChangesAsync(ct);
        return (meta.Id, Atualizado: false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // Fase 1.5 Fix: AND TenantId= impede IDOR de DELETE.
        // Sem isso qualquer usuário autenticado de qualquer tenant que adivinhe
        // o GUID de uma meta consegue deletá-la (mesmo de outra loja).
        var tenantId = _tenant.TenantId;
        await _ctx.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM MetasVendas WHERE Id={id} AND TenantId={tenantId}");
    }
}