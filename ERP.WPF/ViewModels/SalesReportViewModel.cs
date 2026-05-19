using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using ERP.WPF.Helpers;
using ERP.WPF.Reports;
using QuestPDF.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ERP.WPF.ViewModels;

public class SalesReportViewModel : BaseViewModel
{
    private readonly ISaleService _saleService;

    private DateTime _startDate = DateTime.Today.AddDays(-7);
    public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }

    private DateTime _endDate = DateTime.Today;
    public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }

    private string _selectedSeller = "Todos";
    public string SelectedSeller { get => _selectedSeller; set => SetProperty(ref _selectedSeller, value); }

    public ObservableCollection<string> AvailableSellers { get; } = new() { "Todos" };
    public ObservableCollection<SalesReportItemDto> Sales { get; } = new();

    private decimal _totalRevenue;
    public decimal TotalRevenue { get => _totalRevenue; set => SetProperty(ref _totalRevenue, value); }

    private int _totalSalesCount;
    public int TotalSalesCount { get => _totalSalesCount; set => SetProperty(ref _totalSalesCount, value); }

    public ICommand SearchCommand { get; }
    public ICommand ExportarPdfCommand { get; }   // ← NOVO

    public SalesReportViewModel(ISaleService saleService)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        _saleService = saleService;
        SearchCommand     = new AsyncRelayCommand(_ => LoadReportAsync());
        ExportarPdfCommand = new RelayCommand(_ => ExportarPdf(), _ => Sales.Count > 0);

        _ = LoadVendedoresAsync(); // Carrega o ComboBox primeiro
        _ = LoadReportAsync();
    }

    // ── Carregar Lista de Vendedores do Banco ─────────────────────────────
    private async Task LoadVendedoresAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IUserQueryService>();
            var vendedores = await service.GetAllNamesAsync();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                AvailableSellers.Clear();
                AvailableSellers.Add("Todos");
                foreach (var v in vendedores)
                    AvailableSellers.Add(v);
                SelectedSeller = "Todos";
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Erro ao carregar vendedores: {ex.Message}");
        }
    }

    // ── PDF ───────────────────────────────────────────────────────────────
    private void ExportarPdf()
    {
        var config = ConfiguracaoService.Carregar();
        var doc = new VendasPdfReport(config, StartDate, EndDate, SelectedSeller, Sales);
        PdfReportBase.SalvarEAbrir(doc, "Vendas");
    }

    // ── Carregamento ──────────────────────────────────────────────────────
    private async Task LoadReportAsync()
    {
        IsBusy = true;
        try
        {
            var results = await _saleService.GetSalesReportAsync(StartDate, EndDate, SelectedSeller);
            Sales.Clear();
            foreach (var item in results) Sales.Add(item);

            TotalRevenue    = Sales.Sum(s => s.ValorTotal);
            TotalSalesCount = Sales.Count;

            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Erro ao carregar relatório: {ex.Message}");
        }
        finally { IsBusy = false; }
    }
}