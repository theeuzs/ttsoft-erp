// ── ERP.Infrastructure/Services/RelatorioServices.cs ─────────────────────────
// S3.7: ExecuteSqlRawAsync → ExecuteSqlInterpolatedAsync em HaverService.LancarAsync
// Todas as outras partes são idênticas ao original.
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace ERP.Infrastructure.Services;

// ═══════════════════════════════════════════════════════════════════════════════
//  DRE
// ═══════════════════════════════════════════════════════════════════════════════
public class DreService : IDreService
{
    private readonly IServiceProvider _sp;
    public DreService(IServiceProvider sp) => _sp = sp;

    public async Task<DreResultadoDto> CalcularAsync(DateTime dataInicio, DateTime dataFim, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dataFimAjustada = dataFim.Date.AddDays(1).AddTicks(-1);

        var vendas = await ctx.Sales.AsNoTracking()
            .Include(s => s.Items).ThenInclude(i => i.Product)
            .Where(s => s.SaleDate >= dataInicio.Date
                     && s.SaleDate <= dataFimAjustada
                     && s.Status != SaleStatus.Cancelada
                     && s.IsDeleted == false)
            .ToListAsync();

        var receitaBruta     = vendas.Sum(v => v.Total);
        var custoMercadorias = vendas.SelectMany(v => v.Items)
            .Sum(i => i.Quantity * (i.Product?.CostPrice > 0 ? i.Product!.CostPrice : i.UnitPrice * 0.60m));
        var lucroBruto       = receitaBruta - custoMercadorias;

        var despesas = await ctx.ContasPagar.AsNoTracking()
            .Where(c => c.DataVencimento >= dataInicio.Date
                     && c.DataVencimento <= dataFimAjustada
                     && c.IsDeleted == false)
            .SumAsync(c => (decimal?)c.Valor) ?? 0;

        var lucroLiquido = lucroBruto - despesas;
        var margem       = receitaBruta > 0 ? lucroLiquido / receitaBruta * 100 : 0;

        return new DreResultadoDto(receitaBruta, custoMercadorias, lucroBruto, despesas, lucroLiquido, margem);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CURVA ABC
// ═══════════════════════════════════════════════════════════════════════════════
public class AbcService : IAbcService
{
    private readonly IServiceProvider _sp;
    public AbcService(IServiceProvider sp) => _sp = sp;

    public async Task<IReadOnlyList<AbcItemDto>> CalcularAsync(DateTime dataInicio, DateTime dataFim, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dataFimAjustada = dataFim.Date.AddDays(1).AddTicks(-1);

        var agrupamentoBruto = await ctx.SaleItems.AsNoTracking()
            .Where(i => i.Sale.SaleDate >= dataInicio.Date
                     && i.Sale.SaleDate <= dataFimAjustada
                     && i.Sale.Status != SaleStatus.Cancelada
                     && i.Sale.IsDeleted == false)
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new
            {
                NomeProduto  = g.Key.ProductName,
                QtdTotal     = g.Sum(i => i.Quantity),
                TotalVendido = g.Sum(i => i.Quantity * i.UnitPrice)
            })
            .OrderByDescending(x => x.TotalVendido)
            .ToListAsync();

        return agrupamentoBruto
            .Select(x => new AbcItemDto(x.NomeProduto, x.QtdTotal, x.TotalVendido))
            .ToList();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  COMISSÃO
// ═══════════════════════════════════════════════════════════════════════════════
public class ComissaoService : IComissaoService
{
    private readonly IServiceProvider _sp;
    public ComissaoService(IServiceProvider sp) => _sp = sp;

    public async Task<ComissaoResultadoDto> CalcularAsync(DateTime dataInicio, DateTime dataFim, decimal percentual, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dataFimAjustada = dataFim.Date.AddDays(1).AddTicks(-1);

        var grupos = await ctx.Sales.AsNoTracking()
            .Where(s => s.SaleDate >= dataInicio.Date
                     && s.SaleDate <= dataFimAjustada
                     && s.Status != SaleStatus.Cancelada
                     && s.IsDeleted == false)
            .GroupBy(s => s.SellerName)
            .Select(g => new
            {
                Vendedor     = g.Key,
                QtdVendas    = g.Count(),
                TotalVendido = g.Sum(s => s.Total)
            })
            .OrderByDescending(x => x.TotalVendido)
            .ToListAsync();

        var vendedores = grupos
            .Select(g => new ComissaoVendedorDto(
                string.IsNullOrEmpty(g.Vendedor) ? "Vendedor Padrão" : g.Vendedor,
                g.QtdVendas,
                g.TotalVendido,
                g.TotalVendido * (percentual / 100)))
            .ToList();

        return new ComissaoResultadoDto(vendedores, vendedores.Sum(v => v.TotalVendido), vendedores.Sum(v => v.ValorComissao));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  MARGEM
// ═══════════════════════════════════════════════════════════════════════════════
public class MargemService : IMargemService
{
    private readonly IServiceProvider _sp;
    public MargemService(IServiceProvider sp) => _sp = sp;

    public async Task<IReadOnlyList<MargemProdutoDto>> ObterAsync(CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.SalePrice > 0)
            .OrderBy(p => p.Category != null ? p.Category.Name : "")
            .ThenBy(p => p.Name)
            .Select(p => new MargemProdutoDto(
                p.Name,
                p.SKU ?? string.Empty,
                p.Category != null ? p.Category.Name : "Sem categoria",
                p.SalePrice,
                p.OriginalCost,
                p.Stock))
            .ToListAsync();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  FLUXO DE CAIXA
// ═══════════════════════════════════════════════════════════════════════════════
public class FluxoCaixaService : IFluxoCaixaService
{
    private readonly IServiceProvider _sp;
    public FluxoCaixaService(IServiceProvider sp) => _sp = sp;

    public async Task<FluxoCaixaResultadoDto> ObterAsync(DateTime dataInicio, DateTime dataFim, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dataFimAjustada = dataFim.Date.AddDays(1).AddTicks(-1);

        var entradas = await ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.DataVencimento >= dataInicio.Date
                     && c.DataVencimento <= dataFimAjustada
                     && c.Status == "Pendente")
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();

        var saidas = await ctx.ContasPagar.AsNoTracking()
            .Where(c => c.DataVencimento >= dataInicio.Date
                     && c.DataVencimento <= dataFimAjustada
                     && c.Status == "Pendente")
            .OrderBy(c => c.DataVencimento)
            .ToListAsync();

        var lancamentos = new List<FluxoLancamentoDto>();

        foreach (var e in entradas)
            lancamentos.Add(new FluxoLancamentoDto(
                e.DataVencimento,
                e.Descricao ?? e.Customer?.Name ?? "A Receber",
                e.ValorTotal - e.ValorRecebido,
                "Entrada",
                e.Status));

        foreach (var s in saidas)
            lancamentos.Add(new FluxoLancamentoDto(s.DataVencimento, s.Descricao, s.Valor, "Saida", s.Status));

        lancamentos.Sort((a, b) => a.Data.CompareTo(b.Data));

        return new FluxoCaixaResultadoDto(lancamentos, entradas.Sum(e => e.ValorTotal - e.ValorRecebido), saidas.Sum(s => s.Valor));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  HAVER
// ═══════════════════════════════════════════════════════════════════════════════
public class HaverService : IHaverService
{
    private readonly IServiceProvider _sp;
    public HaverService(IServiceProvider sp) => _sp = sp;

    public async Task<decimal> ObterSaldoAsync(Guid customerId, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.Customers.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => c.HaverBalance)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<HaverHistoricoDto>> ObterHistoricoAsync(Guid customerId, CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.MovimentosHaver.AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .OrderBy(m => m.DataMovimento)
            .Select(m => new HaverHistoricoDto(m.DataMovimento, m.Tipo, m.Descricao, m.Valor, m.OperadorNome))
            .ToListAsync();
    }

    public async Task LancarAsync(Guid customerId, decimal valor, string tipo, string descricao, string operadorNome)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (tipo == "Saida")
        {
            var saldoAtual = await ctx.Customers.AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => c.HaverBalance)
                .FirstOrDefaultAsync();

            if (valor > saldoAtual)
                throw new InvalidOperationException("Saldo Haver insuficiente.");
        }

        decimal delta = tipo == "Entrada" ? valor : -valor;

        // S3.7: ExecuteSqlInterpolatedAsync — safe by design
        int rows = await ctx.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Customers SET HaverBalance = HaverBalance + {delta} WHERE Id = {customerId}");

        if (rows == 0)
            throw new KeyNotFoundException($"Cliente {customerId} não encontrado.");

        ctx.MovimentosHaver.Add(new MovimentoHaver
        {
            CustomerId    = customerId,
            Valor         = valor,
            Tipo          = tipo,
            Descricao     = descricao,
            DataMovimento = DateTime.Now,
            OperadorNome  = operadorNome,
        });

        await ctx.SaveChangesAsync();
    }

    public async Task RegistrarMovimentoVendaAsync(Guid customerId, decimal valor, string tipo, string descricao, string operadorNome)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        ctx.MovimentosHaver.Add(new MovimentoHaver
        {
            CustomerId    = customerId,
            Valor         = valor,
            Tipo          = tipo,
            Descricao     = descricao,
            DataMovimento = DateTime.Now,
            OperadorNome  = operadorNome,
        });

        await ctx.SaveChangesAsync();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  INVENTÁRIO
// ═══════════════════════════════════════════════════════════════════════════════
public class InventarioService : IInventarioService
{
    private readonly IServiceProvider _sp;
    public InventarioService(IServiceProvider sp) => _sp = sp;

    public async Task<IReadOnlyList<InventarioProdutoDto>> ObterProdutosAsync(CancellationToken ct = default)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.Products.AsNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.Category != null ? p.Category.Name : "")
            .ThenBy(p => p.Name)
            .Select(p => new InventarioProdutoDto(
                p.Id,
                p.Name,
                p.SKU ?? string.Empty,
                p.Category != null ? p.Category.Name : "Sem categoria",
                p.Stock))
            .ToListAsync();
    }

    public async Task AplicarAjustesAsync(IEnumerable<(Guid ProductId, decimal NovoEstoque)> ajustes)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ids     = ajustes.Select(a => a.ProductId).ToList();
        var produtos = await ctx.Products.Where(p => ids.Contains(p.Id)).ToListAsync();

        foreach (var (productId, novoEstoque) in ajustes)
        {
            var produto = produtos.FirstOrDefault(p => p.Id == productId);
            if (produto is null) continue;
            produto.Stock = novoEstoque;
        }

        await ctx.SaveChangesAsync();
    }
}