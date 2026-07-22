// ERP.WPF/ViewModels/FluxoCaixaViewModel.cs
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

public enum FluxoTipo { Entrada, Saida }

public class FluxoItem
{
    public DateTime  Data        { get; set; }
    public string    Descricao   { get; set; } = string.Empty;
    public decimal   Valor       { get; set; }
    public FluxoTipo Tipo        { get; set; }
    public string    Status      { get; set; } = string.Empty;

    public string TipoTexto      => Tipo == FluxoTipo.Entrada ? "↑ Entrada" : "↓ Saída";
    public string ValorFormatado => Tipo == FluxoTipo.Entrada ? $"+ R$ {Valor:N2}" : $"- R$ {Valor:N2}";
    public string CorTipo        => Tipo == FluxoTipo.Entrada ? "#16A34A" : "#DC2626";
}

public class FluxoDia
{
    public DateTime Data          { get; set; }
    public decimal  TotalEntradas { get; set; }
    public decimal  TotalSaidas   { get; set; }
    public decimal  SaldoDia      { get; set; }
    public decimal  SaldoAcumulado{ get; set; }
    public int      QuantidadeItens{ get; set; }

    public string DataFormatada      => Data.ToString("dd/MM (ddd)");
    public string SaldoDiaFormatado  => SaldoDia >= 0 ? $"+ R$ {SaldoDia:N2}" : $"- R$ {Math.Abs(SaldoDia):N2}";
    public string CorSaldo           => SaldoAcumulado >= 0 ? "#16A34A" : "#DC2626";
    public string CorSaldoDia        => SaldoDia >= 0 ? "#16A34A" : "#DC2626";
}

public class FluxoCaixaViewModel : BaseViewModel
{
    private DateTime _dataInicio = DateTime.Today;
    public DateTime DataInicio { get => _dataInicio; set => SetProperty(ref _dataInicio, value); }

    private DateTime _dataFim = DateTime.Today.AddDays(30);
    public DateTime DataFim { get => _dataFim; set => SetProperty(ref _dataFim, value); }

    private decimal _totalEntradas;
    public decimal TotalEntradas { get => _totalEntradas; set => SetProperty(ref _totalEntradas, value); }

    private decimal _totalSaidas;
    public decimal TotalSaidas { get => _totalSaidas; set => SetProperty(ref _totalSaidas, value); }

    private decimal _saldoProjetado;
    public decimal SaldoProjetado { get => _saldoProjetado; set => SetProperty(ref _saldoProjetado, value); }

    // S17 FIX: alerta de saldo negativo — extensão barata do fix de ligar o
    // fluxo ao saldo real (Categoria A, sem construir "simulação" nem nada
    // especulativo, só checar o que já estamos calculando).
    private DateTime? _primeiroDiaNegativo;
    public DateTime? PrimeiroDiaNegativo { get => _primeiroDiaNegativo; set => SetProperty(ref _primeiroDiaNegativo, value); }
    public bool TemAlertaSaldoNegativo => PrimeiroDiaNegativo.HasValue;

    public ObservableCollection<FluxoItem> Lancamentos { get; } = new();
    public ObservableCollection<FluxoDia>  Dias        { get; } = new();

    public ISeries[] GraficoSeries { get; private set; } = Array.Empty<ISeries>();
    public Axis[]    GraficoEixoX  { get; private set; } = Array.Empty<Axis>();
    public Axis[]    GraficoEixoY  { get; private set; } = Array.Empty<Axis>();

    public ICommand CarregarCommand { get; }

    public FluxoCaixaViewModel()
    {
        CarregarCommand = new RelayCommand(async _ => await CarregarAsync());
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        IsBusy = true;
        try
        {
            var service   = App.Services.GetRequiredService<IFluxoCaixaService>();
            var resultado = await service.ObterAsync(DataInicio, DataFim);

            Lancamentos.Clear();
            foreach (var l in resultado.Lancamentos)
                Lancamentos.Add(new FluxoItem
                {
                    Data      = l.Data,
                    Descricao = l.Descricao,
                    Valor     = l.Valor,
                    Tipo      = l.Tipo == "Entrada" ? FluxoTipo.Entrada : FluxoTipo.Saida,
                    Status    = l.Status,
                });

            TotalEntradas  = resultado.TotalEntradas;
            TotalSaidas    = resultado.TotalSaidas;
            // S17 FIX: antes era só TotalEntradas - TotalSaidas — respondia "quanto
            // vou receber menos pagar", não "quanto dinheiro vou ter". Agora parte
            // do saldo consolidado real (Caixa + Contas Bancárias).
            SaldoProjetado = resultado.SaldoInicial + TotalEntradas - TotalSaidas;

            // Consolida por dia
            Dias.Clear();
            decimal saldoAcumulado = resultado.SaldoInicial;
            foreach (var grupo in Lancamentos.GroupBy(l => l.Data.Date).OrderBy(g => g.Key))
            {
                decimal entDia = grupo.Where(l => l.Tipo == FluxoTipo.Entrada).Sum(l => l.Valor);
                decimal saiDia = grupo.Where(l => l.Tipo == FluxoTipo.Saida).Sum(l => l.Valor);
                saldoAcumulado += entDia - saiDia;

                Dias.Add(new FluxoDia
                {
                    Data             = grupo.Key,
                    TotalEntradas    = entDia,
                    TotalSaidas      = saiDia,
                    SaldoDia         = entDia - saiDia,
                    SaldoAcumulado   = saldoAcumulado,
                    QuantidadeItens  = grupo.Count(),
                });
            }

            // S17 FIX: alerta de saldo negativo — primeiro dia, dentro do período
            // consultado, em que o saldo acumulado projetado fica negativo.
            PrimeiroDiaNegativo = Dias.FirstOrDefault(d => d.SaldoAcumulado < 0)?.Data;
            OnPropertyChanged(nameof(TemAlertaSaldoNegativo));

            AtualizarGrafico();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar fluxo de caixa:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void AtualizarGrafico()
    {
        if (!Dias.Any()) { GraficoSeries = Array.Empty<ISeries>(); OnPropertyChanged(nameof(GraficoSeries)); return; }

        var labels = Dias.Select(d => d.Data.ToString("dd/MM")).ToArray();

        GraficoSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name        = "Positivo",
                Values      = Dias.Select(d => d.SaldoDia >= 0 ? (double)d.SaldoDia : 0).ToArray(),
                Fill        = new SolidColorPaint(new SKColor(74, 222, 128)),
                Stroke      = null,
                MaxBarWidth = 40,
            },
            new ColumnSeries<double>
            {
                Name        = "Negativo",
                Values      = Dias.Select(d => d.SaldoDia < 0 ? (double)d.SaldoDia : 0).ToArray(),
                Fill        = new SolidColorPaint(new SKColor(248, 113, 113)),
                Stroke      = null,
                MaxBarWidth = 40,
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