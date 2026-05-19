// ERP.WPF/ViewModels/MargemViewModel.cs
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

public class MargemItem
{
    public string  Nome          { get; set; } = string.Empty;
    public string  SKU           { get; set; } = string.Empty;
    public string  Categoria     { get; set; } = string.Empty;
    public decimal PrecoVenda    { get; set; }
    public decimal PrecoCusto    { get; set; }
    public decimal Estoque       { get; set; }

    public decimal Margem         => PrecoCusto > 0 ? ((PrecoVenda - PrecoCusto) / PrecoVenda) * 100 : 0;
    public decimal LucroUnit      => PrecoVenda - PrecoCusto;
    public decimal LucroPotencial => LucroUnit * Estoque;

    public string CorMargem   => Margem >= 30 ? "#16A34A" : Margem >= 15 ? "#F59E0B" : "#DC2626";
    public string ClasseMargem=> Margem >= 30 ? "✅ Boa"  : Margem >= 15 ? "⚠️ Ok"   : "🔴 Baixa";
}

public class MargemViewModel : BaseViewModel
{
    public ObservableCollection<MargemItem> Produtos { get; } = new();

    private decimal _margemMedia;
    public decimal MargemMedia { get => _margemMedia; set => SetProperty(ref _margemMedia, value); }

    private decimal _lucroPotencial;
    public decimal LucroPotencial { get => _lucroPotencial; set => SetProperty(ref _lucroPotencial, value); }

    private int _produtosMargemBaixa;
    public int ProdutosMargemBaixa { get => _produtosMargemBaixa; set => SetProperty(ref _produtosMargemBaixa, value); }

    private string _filtroBusca = string.Empty;
    public string FiltroBusca
    {
        get => _filtroBusca;
        set { SetProperty(ref _filtroBusca, value); AplicarFiltro(); }
    }

    private ObservableCollection<MargemItem> _todosProdutos = new();
    public ICommand CarregarCommand { get; }

    public MargemViewModel()
    {
        CarregarCommand = new RelayCommand(async _ => await CarregarAsync());
        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        IsBusy = true;
        try
        {
            var service  = App.Services.GetRequiredService<IMargemService>();
            var produtos = await service.ObterAsync();

            _todosProdutos.Clear();
            foreach (var p in produtos)
                _todosProdutos.Add(new MargemItem
                {
                    Nome       = p.Nome,
                    SKU        = p.SKU,
                    Categoria  = p.Categoria,
                    PrecoVenda = p.PrecoVenda,
                    PrecoCusto = p.PrecoCusto, // <-- Voltou ao normal aqui!
                    Estoque    = p.Estoque,
                });

            AplicarFiltro();

            if (_todosProdutos.Any())
            {
                MargemMedia         = _todosProdutos.Average(p => p.Margem);
                LucroPotencial      = _todosProdutos.Sum(p => p.LucroPotencial);
                ProdutosMargemBaixa = _todosProdutos.Count(p => p.Margem < 15);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar margens:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void AplicarFiltro()
    {
        var filtrados = string.IsNullOrWhiteSpace(FiltroBusca)
            ? _todosProdutos
            : _todosProdutos.Where(p =>
                p.Nome.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase) ||
                p.SKU.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase) ||
                p.Categoria.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase));

        Produtos.Clear();
        foreach (var p in filtrados.OrderBy(p => p.Margem)) Produtos.Add(p);
    }
}