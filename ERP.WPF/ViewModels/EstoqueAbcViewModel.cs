// ERP.WPF/ViewModels/EstoqueAbcViewModel.cs
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class ProdutoAbcItem
{
    public int     Rank                { get; set; }
    public string  Nome               { get; set; } = string.Empty;
    public decimal Quantidade         { get; set; }
    public decimal ValorTotal         { get; set; }
    public decimal PercentualAcumulado{ get; set; }
    public string  Classe             { get; set; } = string.Empty;

    public string CorClasse => Classe == "A" ? "#10B981" : Classe == "B" ? "#F59E0B" : "#EF4444";
}

public class EstoqueAbcViewModel : BaseViewModel
{
    public ObservableCollection<ProdutoAbcItem> ListaAbc { get; } = new();

    private DateTime _dataInicio = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime DataInicio { get => _dataInicio; set => SetProperty(ref _dataInicio, value); }

    private DateTime _dataFim = DateTime.Today;
    public DateTime DataFim { get => _dataFim; set => SetProperty(ref _dataFim, value); }

    private decimal _totalFaturamentoPeriodo;
    public decimal TotalFaturamentoPeriodo
    {
        get => _totalFaturamentoPeriodo;
        set => SetProperty(ref _totalFaturamentoPeriodo, value);
    }

    public ICommand GerarRelatorioCommand { get; }

    public EstoqueAbcViewModel()
    {
        GerarRelatorioCommand = new RelayCommand(async _ => await CalcularCurvaAbcAsync());
        _ = CalcularCurvaAbcAsync();
    }

    private async Task CalcularCurvaAbcAsync()
    {
        IsBusy = true;
        try
        {
            ListaAbc.Clear();

            var service = App.Services.GetRequiredService<IAbcService>();
            var itens   = await service.CalcularAsync(DataInicio, DataFim);

            if (!itens.Any()) return;

            TotalFaturamentoPeriodo = itens.Sum(x => x.TotalFinanceiro);

            decimal valorAcumulado = 0;
            int     rank           = 1;

            foreach (var item in itens)
            {
                valorAcumulado += item.TotalFinanceiro;
                decimal pct     = TotalFaturamentoPeriodo > 0
                    ? (valorAcumulado / TotalFaturamentoPeriodo) * 100
                    : 0;

                string classe = pct <= 80 ? "A" : pct <= 95 ? "B" : "C";

                ListaAbc.Add(new ProdutoAbcItem
                {
                    Rank                = rank++,
                    Nome                = item.Nome,
                    Quantidade          = item.Quantidade,
                    ValorTotal          = item.TotalFinanceiro,
                    PercentualAcumulado = pct,
                    Classe              = classe,
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar Curva ABC:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}
