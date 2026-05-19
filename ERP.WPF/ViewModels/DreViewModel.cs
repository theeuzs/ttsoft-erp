// ERP.WPF/ViewModels/DreViewModel.cs
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using ERP.WPF.Helpers;
using ERP.WPF.Reports;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class DreViewModel : BaseViewModel
{
    // ── Filtros ───────────────────────────────────────────────────────────
    private DateTime _dataInicio = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime DataInicio { get => _dataInicio; set => SetProperty(ref _dataInicio, value); }

    private DateTime _dataFim = DateTime.Today;
    public DateTime DataFim { get => _dataFim; set => SetProperty(ref _dataFim, value); }

    // ── Resultados ────────────────────────────────────────────────────────
    private decimal _receitaBruta;
    public decimal ReceitaBruta { get => _receitaBruta; set => SetProperty(ref _receitaBruta, value); }

    private decimal _custoMercadorias;
    public decimal CustoMercadorias { get => _custoMercadorias; set => SetProperty(ref _custoMercadorias, value); }

    private decimal _lucroBruto;
    public decimal LucroBruto { get => _lucroBruto; set => SetProperty(ref _lucroBruto, value); }

    private decimal _despesasOperacionais;
    public decimal DespesasOperacionais { get => _despesasOperacionais; set => SetProperty(ref _despesasOperacionais, value); }

    private decimal _lucroLiquido;
    public decimal LucroLiquido { get => _lucroLiquido; set => SetProperty(ref _lucroLiquido, value); }

    private decimal _margemLucratividade;
    public decimal MargemLucratividade { get => _margemLucratividade; set => SetProperty(ref _margemLucratividade, value); }

    // ── Comandos ──────────────────────────────────────────────────────────
    public ICommand GerarDreCommand    { get; }
    public ICommand ExportarPdfCommand { get; }

    public DreViewModel()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        GerarDreCommand    = new RelayCommand(async _ => await CalcularDreAsync());
        ExportarPdfCommand = new RelayCommand(_ => ExportarPdf(),
            _ => ReceitaBruta > 0 || DespesasOperacionais > 0);

        _ = CalcularDreAsync();
    }

    private async Task CalcularDreAsync()
    {
        IsBusy = true;
        try
        {
            // ← Usa serviço da Application layer; zero acesso direto ao DbContext
            var service = App.Services.GetRequiredService<IDreService>();
            var resultado = await service.CalcularAsync(DataInicio, DataFim);

            ReceitaBruta          = resultado.ReceitaBruta;
            CustoMercadorias      = resultado.CustoMercadorias;
            LucroBruto            = resultado.LucroBruto;
            DespesasOperacionais  = resultado.DespesasOperacionais;
            LucroLiquido          = resultado.LucroLiquido;
            MargemLucratividade   = resultado.MargemLucratividade;

            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao calcular DRE:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void ExportarPdf()
    {
        var config = ConfiguracaoService.Carregar();
        var doc = new DrePdfReport(config, DataInicio, DataFim,
            ReceitaBruta, CustoMercadorias,
            LucroBruto, DespesasOperacionais,
            LucroLiquido, MargemLucratividade);

        PdfReportBase.SalvarEAbrir(doc, "DRE");
    }
}
