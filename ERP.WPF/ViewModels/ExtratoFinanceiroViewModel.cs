// ── ERP.WPF/ViewModels/ExtratoFinanceiroViewModel.cs ──────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Extrato Financeiro unificado — timeline de Caixa + Conta Bancária +
/// Recebíveis de Operadora. Injeção via construtor desde o início.
/// </summary>
public class ExtratoFinanceiroViewModel : BaseViewModel
{
    private readonly IExtratoFinanceiroService _service;

    public ObservableCollection<ExtratoItemDto> Itens { get; } = new();

    private DateTime _dataInicio = DateTime.Today.AddDays(-30);
    public DateTime DataInicio { get => _dataInicio; set => SetProperty(ref _dataInicio, value); }

    private DateTime _dataFim = DateTime.Today;
    public DateTime DataFim { get => _dataFim; set => SetProperty(ref _dataFim, value); }

    public decimal TotalEntradas => Itens.Where(i => i.Tipo == "Entrada").Sum(i => i.Valor);
    public decimal TotalSaidas   => Itens.Where(i => i.Tipo == "Saída").Sum(i => i.Valor);
    public decimal TotalPendente => Itens.Where(i => i.Tipo == "Recebível Pendente").Sum(i => i.Valor);

    public ICommand BuscarCommand { get; }

    public ExtratoFinanceiroViewModel(IExtratoFinanceiroService service)
    {
        _service = service;
        BuscarCommand = new RelayCommand(async _ => await BuscarAsync());

        _ = BuscarAsync();
    }

    private async Task BuscarAsync()
    {
        var itens = await _service.ObterExtratoAsync(DataInicio, DataFim);

        Itens.Clear();
        foreach (var item in itens) Itens.Add(item);

        OnPropertyChanged(nameof(TotalEntradas));
        OnPropertyChanged(nameof(TotalSaidas));
        OnPropertyChanged(nameof(TotalPendente));
    }
}