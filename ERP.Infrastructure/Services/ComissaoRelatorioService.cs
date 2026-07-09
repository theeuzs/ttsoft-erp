// ── ERP.Infrastructure/Services/ComissaoRelatorioService.cs ──────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class ComissaoRelatorioService : IComissaoRelatorioService
{
    private readonly AppDbContext _ctx;

    public ComissaoRelatorioService(AppDbContext ctx) => _ctx = ctx;

    public async Task<ComissaoRelatorioDto> CalcularComissoesAsync(
        DateTime? inicio, DateTime? fim, CancellationToken ct = default)
    {
        var ini = inicio ?? DateTime.Today.AddMonths(-1);
        var end = fim    ?? DateTime.Today.AddDays(1);

        // Agrupa vendas por vendedor no período
        var vendas = await _ctx.Sales.AsNoTracking()
            .Where(s => s.SaleDate >= ini && s.SaleDate < end
                     && s.Status != ERP.Domain.Enums.SaleStatus.Cancelada)
            .GroupBy(s => s.SellerName ?? "Sem vendedor")
            .Select(g => new
            {
                Vendedor     = g.Key,
                QtdVendas    = g.Count(),
                TotalVendido = g.Sum(s => s.Total)
            })
            .ToListAsync(ct);

        // Busca percentuais de comissão por usuário/cargo
        var usuarios = await _ctx.Users.AsNoTracking()
            .Include(u => u.Role)
            .Select(u => new { u.Name, Percentual = u.Role != null ? u.Role.PercentualComissao : 0m })
            .ToListAsync(ct);

        var comissoes = vendas.Select(v =>
        {
            var pct = usuarios
                .FirstOrDefault(u => u.Name == v.Vendedor)?.Percentual ?? 0m;

            return new ComissaoVendedorRelatorioDto(
                Vendedor:           v.Vendedor,
                QtdVendas:          v.QtdVendas,
                TotalVendido:       v.TotalVendido,
                PercentualComissao: pct,
                ValorComissao:      Math.Round(v.TotalVendido * pct / 100m, 2));
        })
        .OrderByDescending(c => c.TotalVendido)
        .ToList();

        return new ComissaoRelatorioDto(
            Inicio:         ini,
            Fim:            end.AddDays(-1),
            Vendedores:     comissoes,
            TotalComissoes: comissoes.Sum(c => c.ValorComissao),
            TotalVendido:   comissoes.Sum(c => c.TotalVendido));
    }
}
