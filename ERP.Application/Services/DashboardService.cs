using AutoMapper;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;

namespace ERP.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public DashboardService(IUnitOfWork uow, IMapper mapper)
    {
        _uow    = uow;
        _mapper = mapper;
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        // Pega o dia de hoje, mas ignorando o horário (00:00)
        var hoje     = DateTime.UtcNow.Date;
        // Vai até 23:59:59.999 de hoje
        var fimHoje  = hoje.AddDays(1).AddTicks(-1); 
        var mesAtual = new DateTime(hoje.Year, hoje.Month, 1);
        var sete     = hoje.AddDays(-6);

        // ── Vendas de hoje ────────────────────────────────────────────────
        var vendasHoje = (await _uow.Sales.GetByDateRangeAsync(hoje, fimHoje))
            .Where(s => s.Status != SaleStatus.Cancelada).ToList();

        decimal revenueToday    = vendasHoje.Sum(s => s.Total);
        int     salesCountToday = vendasHoje.Count;

        var recentSales = vendasHoje.OrderByDescending(x => x.SaleDate).Take(8)
            .Select(s => _mapper.Map<SaleDto>(s)).ToList();

        // ── Totais do mês ─────────────────────────────────────────────────
        var monthTotal  = await _uow.Sales.GetMonthTotalAsync();
        var avgTicket   = await _uow.Sales.GetAverageTicketAsync(mesAtual, fimHoje);
        var topRaw      = await _uow.Sales.GetTopProductsAsync(5, mesAtual, fimHoje);
        var topProducts = topRaw.Select(t => new TopProductDto(t.Name, t.Quantity)).ToList();
        var allSalesMonth = await _uow.Sales.GetByDateRangeAsync(mesAtual, fimHoje);
        int totalOrders = allSalesMonth.Count();

        // ── Estoque baixo ─────────────────────────────────────────────────
        var lowStockList = await _uow.Products.GetLowStockAsync();
        var lowStockProducts = lowStockList.Take(50)
            .Select(p => _mapper.Map<ProductDto>(p)).ToList();

        // ── Despesas do mês ───────────────────────────────────────────────
        var despesas = await _uow.ContasPagar.GetAllAsync();
        decimal expensesMonth = despesas
            .Where(c => c.DataVencimento.Year == hoje.Year && c.DataVencimento.Month == hoje.Month)
            .Sum(c => c.Valor);

        // ── Contas vencendo hoje ──────────────────────────────────────────
        var contasHoje = despesas
            .Where(c => c.DataVencimento.Date == hoje && c.Status == "Pendente").ToList();
        int     contasVencendo = contasHoje.Count;
        decimal valorVencendo  = contasHoje.Sum(c => c.Valor);

        // ── Gráfico: últimos 7 dias ───────────────────────────────────────
        var vendasSemana = (await _uow.Sales.GetByDateRangeAsync(sete, fimHoje))
            .Where(s => s.Status != SaleStatus.Cancelada).ToList();

        var grafico = Enumerable.Range(0, 7).Select(i =>
        {
            var dia = sete.AddDays(i);
            double total = (double)vendasSemana
                .Where(s => s.SaleDate.Date == dia)
                .Sum(s => s.Total);
            return (dia.ToString("dd/MM"), total);
        }).ToList();

        return new DashboardDto(
            TodaySales:         revenueToday,
            MonthSales:         monthTotal,
            AverageTicket:      avgTicket,
            TotalOrders:        salesCountToday,
            TopProducts:        topProducts,
            ExpensesThisMonth:  expensesMonth,
            LowStockCount:      lowStockList.Count(),
            ContasVencendoHoje: contasVencendo,
            ValorVencendoHoje:  valorVencendo,
            RecentSales:        recentSales,
            LowStockProducts:   lowStockProducts,
            VendasSemana:       grafico);
    }

    public async Task<DreDto> GetDreSimplificadoAsync(DateTime inicio, DateTime fim)
    {
        var vendas = await _uow.Sales.GetByDateRangeAsync(inicio, fim);
        var vendasValidas = vendas.Where(v => !v.IsDeleted).ToList();

        decimal faturamento = vendasValidas.Sum(v => v.Total);
        decimal custoTotal  = vendasValidas
            .SelectMany(v => v.Items)
            .Sum(item => item.Quantity * (item.Product?.CostPrice ?? (item.UnitPrice * 0.60m)));

        return new DreDto
        {
            ReceitaBruta      = Math.Round(faturamento, 2),
            CustoMercadorias  = Math.Round(custoTotal,  2)
        };
    }
}