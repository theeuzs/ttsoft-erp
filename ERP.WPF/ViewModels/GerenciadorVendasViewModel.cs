// ERP.WPF/ViewModels/GerenciadorVendasViewModel.cs
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

public class GerenciadorVendaItem
{
    public string  ProductName       { get; set; } = string.Empty;
    public string  Categoria         { get; set; } = string.Empty;
    public decimal QuantidadeVendida { get; set; }
    public decimal CustoUnitario     { get; set; }
    public decimal ValorVendaTotal   { get; set; }
    public decimal CustoTotal        { get; set; }

    public decimal LucroTotal => ValorVendaTotal - CustoTotal;
    public decimal MargemPercent => ValorVendaTotal > 0 ? LucroTotal / ValorVendaTotal * 100 : 0;
}

/// <summary>
/// Item 2.1 do roadmap Comercial — produto, classe, quantidade, custo e
/// venda numa visão só. Diferente do HistoricoVendasViewModel (que é
/// drill-down de UM produto selecionado), essa tela mostra TODOS os produtos
/// vendidos, agrupados, sem precisar escolher um por vez.
/// </summary>
public class GerenciadorVendasViewModel : BaseViewModel
{
    public ObservableCollection<GerenciadorVendaItem> Itens { get; } = new();

    private decimal _totalVendido;
    public decimal TotalVendido { get => _totalVendido; set => SetProperty(ref _totalVendido, value); }

    private decimal _totalLucro;
    public decimal TotalLucro { get => _totalLucro; set => SetProperty(ref _totalLucro, value); }

    private DateTime? _dataInicio;
    public DateTime? DataInicio
    {
        get => _dataInicio;
        set { SetProperty(ref _dataInicio, value); _ = CarregarAsync(); }
    }

    private DateTime? _dataFim;
    public DateTime? DataFim
    {
        get => _dataFim;
        set { SetProperty(ref _dataFim, value); _ = CarregarAsync(); }
    }

    private string _filtroBusca = string.Empty;
    public string FiltroBusca
    {
        get => _filtroBusca;
        set { SetProperty(ref _filtroBusca, value); AplicarFiltro(); }
    }

    private ObservableCollection<GerenciadorVendaItem> _todosItens = new();
    public ICommand CarregarCommand { get; }

    public GerenciadorVendasViewModel()
    {
        CarregarCommand = new RelayCommand(async _ => await CarregarAsync());
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        IsBusy = true;
        try
        {
            var service = App.Services.GetRequiredService<IGerenciadorVendasService>();
            var itens   = await service.ObterAsync(DataInicio, DataFim);

            _todosItens.Clear();
            foreach (var i in itens)
                _todosItens.Add(new GerenciadorVendaItem
                {
                    ProductName       = i.ProductName,
                    Categoria         = i.Categoria,
                    QuantidadeVendida = i.QuantidadeVendida,
                    CustoUnitario     = i.CustoUnitario,
                    ValorVendaTotal   = i.ValorVendaTotal,
                    CustoTotal        = i.CustoTotal,
                });

            AplicarFiltro();

            TotalVendido = _todosItens.Sum(i => i.ValorVendaTotal);
            TotalLucro   = _todosItens.Sum(i => i.LucroTotal);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar o gerenciador de vendas:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void AplicarFiltro()
    {
        var filtrados = string.IsNullOrWhiteSpace(FiltroBusca)
            ? _todosItens
            : _todosItens.Where(i =>
                i.ProductName.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase) ||
                i.Categoria.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase));

        Itens.Clear();
        foreach (var i in filtrados.OrderByDescending(i => i.ValorVendaTotal)) Itens.Add(i);
    }
}
