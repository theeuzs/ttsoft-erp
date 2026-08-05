// ERP.WPF/ViewModels/VendasXCustoViewModel.cs
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class VendasXCustoItem
{
    public string  Periodo                 { get; set; } = string.Empty;
    public decimal TotalVendas              { get; set; }
    public decimal TotalCusto               { get; set; }
    public decimal ContasAReceberPendente   { get; set; }
    public decimal LucroBruto => TotalVendas - TotalCusto;
    public decimal MargemPercent => TotalVendas > 0 ? LucroBruto / TotalVendas * 100 : 0;
}

/// <summary>
/// Item 2.4 do roadmap Comercial — vendas, custo e contas a receber
/// pendentes cruzados por mês, com gráfico.
/// </summary>
public class VendasXCustoViewModel : BaseViewModel
{
    private DateTime _dataInicio = DateTime.Today.AddMonths(-5).AddDays(1 - DateTime.Today.Day);
    public DateTime DataInicio { get => _dataInicio; set { SetProperty(ref _dataInicio, value); _ = CarregarAsync(); } }

    private DateTime _dataFim = DateTime.Today;
    public DateTime DataFim { get => _dataFim; set { SetProperty(ref _dataFim, value); _ = CarregarAsync(); } }

    private decimal _totalVendas;
    public decimal TotalVendas { get => _totalVendas; set => SetProperty(ref _totalVendas, value); }

    private decimal _totalCusto;
    public decimal TotalCusto { get => _totalCusto; set => SetProperty(ref _totalCusto, value); }

    private decimal _totalLucro;
    public decimal TotalLucro { get => _totalLucro; set => SetProperty(ref _totalLucro, value); }

    private decimal _totalContasAReceber;
    public decimal TotalContasAReceber { get => _totalContasAReceber; set => SetProperty(ref _totalContasAReceber, value); }

    public ObservableCollection<VendasXCustoItem> Periodos { get; } = new();

    public ISeries[] GraficoSeries { get; private set; } = Array.Empty<ISeries>();
    public Axis[]    GraficoEixoX  { get; private set; } = Array.Empty<Axis>();
    public Axis[]    GraficoEixoY  { get; private set; } = Array.Empty<Axis>();

    public ICommand CarregarCommand { get; }

    public VendasXCustoViewModel()
    {
        CarregarCommand = new RelayCommand(async _ => await CarregarAsync());
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        IsBusy = true;
        try
        {
            var service   = App.Services.GetRequiredService<IVendasXCustoService>();
            var resultado = await service.ObterAsync(DataInicio, DataFim);

            Periodos.Clear();
            foreach (var p in resultado)
                Periodos.Add(new VendasXCustoItem
                {
                    Periodo               = p.Periodo,
                    TotalVendas           = p.TotalVendas,
                    TotalCusto            = p.TotalCusto,
                    ContasAReceberPendente= p.ContasAReceberPendente,
                });

            TotalVendas         = Periodos.Sum(p => p.TotalVendas);
            TotalCusto           = Periodos.Sum(p => p.TotalCusto);
            TotalLucro            = TotalVendas - TotalCusto;
            // Contas a receber pendente não soma entre meses (seria contar a
            // mesma dívida várias vezes) — usa o último período carregado,
            // que reflete o pendente mais recente dentro do intervalo.
            TotalContasAReceber   = Periodos.LastOrDefault()?.ContasAReceberPendente ?? 0;

            AtualizarGrafico();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar vendas x custo:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void AtualizarGrafico()
    {
        if (!Periodos.Any()) { GraficoSeries = Array.Empty<ISeries>(); OnPropertyChanged(nameof(GraficoSeries)); return; }

        var labels = Periodos.Select(p => p.Periodo).ToArray();

        GraficoSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name        = "Vendas",
                Values      = Periodos.Select(p => (double)p.TotalVendas).ToArray(),
                Fill        = new SolidColorPaint(new SKColor(74, 222, 128)),
                Stroke      = null,
                MaxBarWidth = 40,
            },
            new ColumnSeries<double>
            {
                Name        = "Custo",
                Values      = Periodos.Select(p => (double)p.TotalCusto).ToArray(),
                Fill        = new SolidColorPaint(new SKColor(248, 113, 113)),
                Stroke      = null,
                MaxBarWidth = 40,
            },
            new LineSeries<double>
            {
                Name   = "Contas a Receber",
                Values = Periodos.Select(p => (double)p.ContasAReceberPendente).ToArray(),
                Fill   = null,
                Stroke = new SolidColorPaint(new SKColor(59, 130, 246), 3),
                GeometrySize = 8,
                GeometryFill   = new SolidColorPaint(new SKColor(59, 130, 246)),
                GeometryStroke = new SolidColorPaint(new SKColor(59, 130, 246)),
            },
        };

        GraficoEixoX = new[] { new Axis { Labels = labels, TextSize = 11,
            LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)), SeparatorsPaint = null } };

        GraficoEixoY = new[] { new Axis { TextSize = 11,
            LabelsPaint = new SolidColorPaint(new SKColor(100, 116, 139)),
            Labeler = v => $"R$ {v:N0}" } };

        OnPropertyChanged(nameof(GraficoSeries));
        OnPropertyChanged(nameof(GraficoEixoX));
        OnPropertyChanged(nameof(GraficoEixoY));
    }
}
