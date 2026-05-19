using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace ERP.Infrastructure.Services;

public class BIService : IBIService
{
    private readonly IServiceProvider _sp;
    public BIService(IServiceProvider sp) => _sp = sp;

    // ── Sazonalidade: vendas mês a mês ───────────────────────────────────────
    public async Task<IReadOnlyList<SazonalidadeDto>> ObterSazonalidadeAsync(
        int meses = 12, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var inicio = DateTime.Today.AddMonths(-meses + 1).AddDays(-DateTime.Today.Day + 1);

        var vendas = await ctx.Sales.AsNoTracking()
            .Where(s => s.SaleDate >= inicio && s.Status != SaleStatus.Cancelada)
            .Select(s => new { s.SaleDate, s.Total })
            .ToListAsync(ct);

        return vendas
            .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new SazonalidadeDto(
                g.Key.Month,
                CultureInfo.GetCultureInfo("pt-BR")
                    .DateTimeFormat.GetMonthName(g.Key.Month),
                g.Key.Year,
                g.Sum(s => s.Total),
                g.Count(),
                g.Count() > 0 ? g.Sum(s => s.Total) / g.Count() : 0))
            .ToList();
    }

    // ── Curva ABC avançada com margem e classificação ─────────────────────────
    public async Task<IReadOnlyList<AbcAvancadoDto>> ObterAbcAvancadoAsync(
        DateTime inicio, DateTime fim, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fimAjustado = fim.Date.AddDays(1).AddTicks(-1);

        var itens = await ctx.SaleItems.AsNoTracking()
            .Where(i => i.Sale.SaleDate >= inicio.Date
                     && i.Sale.SaleDate <= fimAjustado
                     && i.Sale.Status != SaleStatus.Cancelada)
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new
            {
                Nome       = g.Key.ProductName,
                Quantidade = g.Sum(i => i.Quantity),
                Total      = g.Sum(i => i.TotalItem),
                CustoMedio = g.Average(i => i.Product != null ? i.Product.OriginalCost : 0m),
                SKU        = g.Select(i => i.Product != null ? i.Product.SKU : "").FirstOrDefault() ?? ""
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        var totalGeral = itens.Sum(i => i.Total);
        decimal acumulado = 0;
        int rank = 0;

        return itens.Select(i =>
        {
            rank++;
            acumulado += totalGeral > 0 ? i.Total / totalGeral * 100 : 0;
            var classe = acumulado <= 80 ? "A" : acumulado <= 95 ? "B" : "C";
            var margem = i.Total > 0 && i.CustoMedio > 0
                ? (i.Total - i.CustoMedio * i.Quantidade) / i.Total * 100
                : 0;

            return new AbcAvancadoDto(
                i.Nome, i.SKU, "",
                i.Quantidade, i.Total,
                Math.Round(acumulado, 2), classe,
                Math.Round(margem, 2), rank);
        }).ToList();
    }

    // ── DRE Detalhado com categorização de despesas ───────────────────────────
    public async Task<DreDetalhadoDto> ObterDreDetalhadoAsync(
        DateTime inicio, DateTime fim, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fimAjustado = fim.Date.AddDays(1).AddTicks(-1);

        var vendas = await ctx.Sales.AsNoTracking()
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Where(s => s.SaleDate >= inicio.Date
                     && s.SaleDate <= fimAjustado
                     && s.Status != SaleStatus.Cancelada)
            .ToListAsync(ct);

        var despesas = await ctx.ContasPagar.AsNoTracking()
            .Where(c => c.DataVencimento >= inicio.Date
                     && c.DataVencimento <= fimAjustado)
            .ToListAsync(ct);

        var receitaBruta = vendas.Sum(v => v.Subtotal);
        var descontos    = vendas.Sum(v => v.Subtotal - v.Total);
        var receitaLiq   = receitaBruta - descontos;

        var custo = vendas.SelectMany(v => v.Items)
            .Sum(i => i.Quantity * (i.Product?.OriginalCost > 0
                ? i.Product!.OriginalCost : i.UnitPrice * 0.6m));

        var lucroBruto  = receitaLiq - custo;
        var margemBruta = receitaLiq > 0 ? lucroBruto / receitaLiq * 100 : 0;

        var despFixas   = despesas.Where(d => d.Categoria == "Fixa").Sum(d => d.Valor);
        var despVar     = despesas.Where(d => d.Categoria != "Fixa").Sum(d => d.Valor);
        var totalDesp   = despFixas + despVar;
        var ebitda      = lucroBruto - totalDesp;
        var lucroLiq    = ebitda;
        var margemLiq   = receitaLiq > 0 ? lucroLiq / receitaLiq * 100 : 0;

        var linhas = despesas
            .GroupBy(d => d.Descricao)
            .Select(g => new DreLinhaDto(g.Key, g.Sum(d => d.Valor),
                g.First().Categoria ?? "Variável"))
            .OrderByDescending(l => l.Valor)
            .ToList();

        return new DreDetalhadoDto(
            receitaBruta, descontos, receitaLiq,
            custo, lucroBruto, Math.Round(margemBruta, 2),
            totalDesp, despFixas, despVar,
            Math.Round(ebitda, 2), Math.Round(lucroLiq, 2),
            Math.Round(margemLiq, 2), linhas);
    }

    // ── Ranking de vendedores ─────────────────────────────────────────────────
    public async Task<IReadOnlyList<RankingVendedorDto>> ObterRankingVendedoresAsync(
        DateTime inicio, DateTime fim, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fimAjustado = fim.Date.AddDays(1).AddTicks(-1);

        var grupos = await ctx.Sales.AsNoTracking()
            .Where(s => s.SaleDate >= inicio.Date
                     && s.SaleDate <= fimAjustado
                     && s.Status != SaleStatus.Cancelada)
            .GroupBy(s => s.SellerName)
            .Select(g => new
            {
                Nome      = g.Key ?? "Sem Vendedor",
                QtdVendas = g.Count(),
                Total     = g.Sum(s => s.Total)
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        var totalGeral = grupos.Sum(g => g.Total);
        return grupos.Select((g, idx) => new RankingVendedorDto(
            idx + 1, g.Nome, g.QtdVendas, g.Total,
            g.QtdVendas > 0 ? Math.Round(g.Total / g.QtdVendas, 2) : 0,
            totalGeral > 0 ? Math.Round(g.Total / totalGeral * 100, 2) : 0))
            .ToList();
    }

    // ── Previsão de demanda com sugestão de compra ───────────────────────────
    public async Task<IReadOnlyList<PrevisaoDemandaDto>> ObterPrevisaoDemandaAsync(
        CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tressMesesAtras = DateTime.Today.AddMonths(-3);

        // Vendas dos últimos 3 meses por produto
        var vendas = await ctx.SaleItems.AsNoTracking()
            .Where(i => i.Sale.SaleDate >= tressMesesAtras
                     && i.Sale.Status != SaleStatus.Cancelada)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, TotalVendido = g.Sum(i => i.Quantity) })
            .ToListAsync(ct);

        var prodIds = vendas.Select(v => v.ProductId).ToList();

        var produtos = await ctx.Products.AsNoTracking()
            .Where(p => prodIds.Contains(p.Id) && !p.IsDeleted)
            .Select(p => new { p.Id, p.Name, p.Stock, p.MinStock })
            .ToListAsync(ct);

        return produtos.Select(p =>
        {
            var venda      = vendas.FirstOrDefault(v => v.ProductId == p.Id);
            var mediaMensal = venda != null ? venda.TotalVendido / 3m : 0;
            var diasEstoque = mediaMensal > 0
                ? (int)(p.Stock / (mediaMensal / 30m))
                : 999;
            var sugestao = mediaMensal > 0
                ? Math.Max(0, mediaMensal * 2 - p.Stock)  // 2 meses de cobertura
                : 0;

            return new PrevisaoDemandaDto(
                p.Id, p.Name,
                Math.Round(mediaMensal, 2),
                p.Stock, diasEstoque,
                p.Stock <= p.MinStock,
                Math.Round(sugestao, 2));
        })
        .Where(p => p.DiasEstoque < 60 || p.AbaixoDoMinimo) // Só os críticos
        .OrderBy(p => p.DiasEstoque)
        .ToList();
    }
}
