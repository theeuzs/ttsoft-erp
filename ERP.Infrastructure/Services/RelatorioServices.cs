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
    private readonly AppDbContext            _ctx;
    private readonly IContaBancariaService   _contaBancariaService;

    // S17 FIX: migrado de IServiceProvider + CreateScope() (Service Locator a
    // nível de service) pra injeção via construtor — Parte 0 do roadmap,
    // aproveitando que já estou tocando este arquivo pra outra coisa.
    public FluxoCaixaService(AppDbContext ctx, IContaBancariaService contaBancariaService)
    {
        _ctx                  = ctx;
        _contaBancariaService = contaBancariaService;
    }

    public async Task<FluxoCaixaResultadoDto> ObterAsync(DateTime dataInicio, DateTime dataFim, CancellationToken ct = default)
    {
        var dataFimAjustada = dataFim.Date.AddDays(1).AddTicks(-1);

        var entradas = await _ctx.ContasReceber.AsNoTracking()
            .Include(c => c.Customer)
            .Where(c => c.DataVencimento >= dataInicio.Date
                     && c.DataVencimento <= dataFimAjustada
                     && c.Status == "Pendente")
            .OrderBy(c => c.DataVencimento)
            .ToListAsync(ct);

        var saidas = await _ctx.ContasPagar.AsNoTracking()
            .Where(c => c.DataVencimento >= dataInicio.Date
                     && c.DataVencimento <= dataFimAjustada
                     && c.Status == "Pendente")
            .OrderBy(c => c.DataVencimento)
            .ToListAsync(ct);

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

        // S17 FIX: saldo consolidado real (Caixa + Contas Bancárias) como ponto de
        // partida — sem isso, o "saldo projetado" era só entradas-menos-saídas do
        // período, não "quanto dinheiro eu vou ter", que é a pergunta de verdade.
        var posicao = await _contaBancariaService.ObterPosicaoFinanceiraAsync();

        return new FluxoCaixaResultadoDto(
            lancamentos,
            entradas.Sum(e => e.ValorTotal - e.ValorRecebido),
            saidas.Sum(s => s.Valor),
            posicao.SaldoConsolidado);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  HAVER
// ═══════════════════════════════════════════════════════════════════════════════
public class HaverService : IHaverService
{
    private readonly AppDbContext   _db;
    private readonly IRequestTenant _tenant;

    // ── FASE 0 FIX ───────────────────────────────────────────────────────────
    // Antes: HaverService usava IServiceProvider + CreateScope() para criar
    // seu próprio DbContext. O novo scope criava um RequestTenant zerado
    // (TenantId = Guid.Empty), o que destruía o isolamento de tenant.
    // Agora: injeta AppDbContext e IRequestTenant do mesmo scope da requisição.
    // Os HasQueryFilter do DbContext filtram por TenantId automaticamente.
    public HaverService(AppDbContext db, IRequestTenant tenant)
    {
        _db     = db;
        _tenant = tenant;
    }

    public async Task<decimal> ObterSaldoAsync(Guid customerId, CancellationToken ct = default)
    {
        // HasQueryFilter em Customer já filtra por TenantId.
        // Se o cliente não pertence a este tenant, retorna 0 (não expõe dado).
        return await _db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => c.HaverBalance)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<HaverHistoricoDto>> ObterHistoricoAsync(Guid customerId, CancellationToken ct = default)
    {
        // HasQueryFilter em MovimentoHaver já filtra por TenantId após correção.
        return await _db.MovimentosHaver.AsNoTracking()
            .Where(m => m.CustomerId == customerId)
            .OrderBy(m => m.DataMovimento)
            .Select(m => new HaverHistoricoDto(m.DataMovimento, m.Tipo, m.Descricao, m.Valor, m.OperadorNome))
            .ToListAsync(ct);
    }

    public async Task LancarAsync(Guid customerId, decimal valor, string tipo, string descricao, string operadorNome)
    {
        var delta    = tipo == "Entrada" ? valor : -valor;
        var tenantId = _tenant.TenantId;

        // S8 FIX: UPDATE atômico com WHERE condicional — elimina TOCTOU.
        // Padrão anterior: read saldo → check → read customer → check → write (duas transações distintas).
        // Vetor: duas Saídas concorrentes com saldo = 100 → ambas passam no check com saldo 100 → cliente consome 200.
        // Agora: UPDATE só executa se HaverBalance + delta >= 0; segunda transação concorrente retorna rows = 0.
        // Nota: InMemory não executa SQL real → testes que chamam LancarAsync("Saida") precisam de SQLite in-process.
        var rows = await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE Customers
               SET HaverBalance = HaverBalance + {delta},
                   UpdatedAt    = {DateTime.UtcNow}
               WHERE Id       = {customerId}
                 AND TenantId = {tenantId}
                 AND (HaverBalance + {delta}) >= 0");

        if (rows == 0)
            throw new InvalidOperationException(
                tipo == "Saida"
                    ? "Saldo Haver insuficiente para esta operação."
                    : "Cliente não encontrado neste tenant.");

        _db.MovimentosHaver.Add(new MovimentoHaver
        {
            CustomerId    = customerId,
            Valor         = valor,
            Tipo          = tipo,
            Descricao     = descricao,
            DataMovimento = DateTime.Now,
            OperadorNome  = operadorNome,
        });

        await _db.SaveChangesAsync();
    }

    public async Task RegistrarMovimentoVendaAsync(Guid customerId, decimal valor, string tipo, string descricao, string operadorNome)
    {
        _db.MovimentosHaver.Add(new MovimentoHaver
        {
            CustomerId    = customerId,
            Valor         = valor,
            Tipo          = tipo,
            Descricao     = descricao,
            DataMovimento = DateTime.Now,
            OperadorNome  = operadorNome,
        });

        await _db.SaveChangesAsync();
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

    // PERFORMANCE FIX: filtra por ID direto na query (WHERE Id IN (...)), em vez
    // de carregar o catálogo inteiro e filtrar em memória como o antigo uso de
    // ObterProdutosAsync() fazia no relatório de divergências.
    public async Task<IReadOnlyList<InventarioProdutoDto>> ObterProdutosPorIdsAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idsList = ids as ICollection<Guid> ?? ids.ToList();
        if (idsList.Count == 0) return Array.Empty<InventarioProdutoDto>();

        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.Products.AsNoTracking()
            .Where(p => idsList.Contains(p.Id))
            .Include(p => p.Category)
            .OrderBy(p => p.Category != null ? p.Category.Name : "")
            .ThenBy(p => p.Name)
            .Select(p => new InventarioProdutoDto(
                p.Id,
                p.Name,
                p.SKU ?? string.Empty,
                p.Category != null ? p.Category.Name : "Sem categoria",
                p.Stock))
            .ToListAsync(ct);
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