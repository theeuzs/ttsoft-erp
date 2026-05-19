using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ERP.WPF.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    // ── Cards ─────────────────────────────────────────────────────────────
    private decimal _revenueToday;
    public decimal RevenueToday
    { get => _revenueToday; set { _revenueToday = value; OnPropertyChanged(nameof(RevenueToday)); } }

    private int _salesCountToday;
    public int SalesCountToday
    { get => _salesCountToday; set { _salesCountToday = value; OnPropertyChanged(nameof(SalesCountToday)); } }

    private decimal _expensesThisMonth;
    public decimal ExpensesThisMonth
    { get => _expensesThisMonth; set { _expensesThisMonth = value; OnPropertyChanged(nameof(ExpensesThisMonth)); } }

    private int _lowStockCount;
    public int LowStockCount
    { get => _lowStockCount; set { _lowStockCount = value; OnPropertyChanged(nameof(LowStockCount)); } }

    private int _contasVencendoHoje;
    public int ContasVencendoHoje
    { get => _contasVencendoHoje; set { _contasVencendoHoje = value; OnPropertyChanged(nameof(ContasVencendoHoje)); } }

    private decimal _valorVencendoHoje;
    public decimal ValorVencendoHoje
    { get => _valorVencendoHoje; set { _valorVencendoHoje = value; OnPropertyChanged(nameof(ValorVencendoHoje)); } }

    // ── Gráfico ───────────────────────────────────────────────────────────
    public ISeries[]  GraficoVendas      { get; private set; } = Array.Empty<ISeries>();
    public Axis[]     GraficoVendasEixoX { get; private set; } = Array.Empty<Axis>();
    public Axis[]     GraficoVendasEixoY { get; private set; } = Array.Empty<Axis>();

    // ── Listas ────────────────────────────────────────────────────────────
    public ObservableCollection<SaleDto>    RecentSales      { get; } = new();
    public ObservableCollection<ProductDto> LowStockProducts { get; } = new();

    // ── Comandos ──────────────────────────────────────────────────────────
    public ICommand RefreshCommand         { get; }
    public ICommand OpenSalesReportCommand { get; }
    public ICommand OpenDreCommand         { get; }
    public ICommand OpenAbcCommand         { get; }
    public ICommand OpenComissaoCommand    { get; }

    public DashboardViewModel()
    {
        RefreshCommand         = new AsyncRelayCommand(async _ => await LoadDashboardDataAsync());
        OpenDreCommand         = new RelayCommand(_ => AbrirDre());
        OpenAbcCommand         = new RelayCommand(_ => AbrirCurvaAbc());
        OpenComissaoCommand    = new RelayCommand(_ => new Views.ComissaoView { DataContext = new ComissaoViewModel() }.ShowDialog());
        OpenSalesReportCommand = new RelayCommand(_ => AbrirRelatorioVendas());

        _ = LoadDashboardDataAsync();
    }

    private void AbrirRelatorioVendas()
    {
        try
        {
            var saleService = App.Services.GetRequiredService<ISaleService>();
            var viewModel   = new SalesReportViewModel(saleService);
            new Views.SalesReportView(viewModel).ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao abrir relatório: {ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AbrirDre()
    {
        try
        {
            var viewModel = new DreViewModel();
            new Views.DreView { DataContext = viewModel }.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao abrir DRE: {ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AbrirCurvaAbc()
        => new Views.EstoqueAbcView { DataContext = new EstoqueAbcViewModel() }.ShowDialog();

    private async Task LoadDashboardDataAsync()
    {
        IsBusy = true;
        try
        {
            using var scope   = App.Services.CreateScope();
            var dashService   = scope.ServiceProvider.GetRequiredService<IDashboardService>();
            var dto           = await dashService.GetDashboardAsync();

            RevenueToday       = dto.TodaySales;
            SalesCountToday    = dto.TotalOrders;
            ExpensesThisMonth  = dto.ExpensesThisMonth;
            LowStockCount      = dto.LowStockCount;
            ContasVencendoHoje = dto.ContasVencendoHoje;
            ValorVencendoHoje  = dto.ValorVencendoHoje;

            RecentSales.Clear();
            foreach (var s in dto.RecentSales)      RecentSales.Add(s);

            LowStockProducts.Clear();
            foreach (var p in dto.LowStockProducts) LowStockProducts.Add(p);

            // ── Gráfico ───────────────────────────────────────────────────
            var labels  = dto.VendasSemana.Select(v => v.Label).ToArray();
            var valores = dto.VendasSemana.Select(v => v.Valor).ToArray();

            GraficoVendas = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Name        = "Vendas",
                    Values      = valores,
                    Fill        = new SolidColorPaint(new SKColor(59, 130, 246)),
                    Stroke      = null,
                    MaxBarWidth = 40,
                }
            };

            GraficoVendasEixoX = new Axis[]
            {
                new Axis
                {
                    Labels          = labels,
                    TextSize        = 11,
                    LabelsPaint     = new SolidColorPaint(new SKColor(100, 116, 139)),
                    SeparatorsPaint = null,
                }
            };

            GraficoVendasEixoY = new Axis[]
            {
                new Axis
                {
                    TextSize    = 11,
                    LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)),
                    Labeler     = v => $"R$ {v:N0}",
                }
            };

            OnPropertyChanged(nameof(GraficoVendas));
            OnPropertyChanged(nameof(GraficoVendasEixoX));
            OnPropertyChanged(nameof(GraficoVendasEixoY));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar o dashboard:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}
