// ── ERP.WPF/ViewModels/HistoricoVendasViewModel.cs ────────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Histórico de vendas de um produto — resolve a necessidade real de "lembro o
/// produto, não lembro a data, quero achar a venda". Injeção via construtor
/// desde o início (Parte 0 do roadmap).
/// </summary>
public class HistoricoVendasViewModel : BaseViewModel
{
    private readonly IUnitOfWork      _uow;
    private readonly IProductService  _productService;

    public ObservableCollection<ProductDto> ProdutosSugestao { get; } = new();
    public ObservableCollection<HistoricoVendaProdutoItem> Historico { get; } = new();

    private string _buscaProduto = string.Empty;
    public string BuscaProduto
    {
        get => _buscaProduto;
        set { SetProperty(ref _buscaProduto, value); _ = BuscarProdutosAsync(value); }
    }

    private ProductDto? _produtoSelecionado;
    public ProductDto? ProdutoSelecionado
    {
        get => _produtoSelecionado;
        set
        {
            SetProperty(ref _produtoSelecionado, value);
            OnPropertyChanged(nameof(TemProdutoSelecionado));
            _ = CarregarHistoricoAsync();
        }
    }

    public bool TemProdutoSelecionado => ProdutoSelecionado != null;

    /// <summary>Quantidade total já vendida deste produto (só vendas não canceladas).</summary>
    public decimal QuantidadeTotalVendida => Historico
        .Where(h => h.Status != "Cancelada")
        .Sum(h => h.Quantidade);

    /// <summary>Faturamento total já gerado por este produto (só vendas não canceladas).</summary>
    public decimal TotalVendido => Historico
        .Where(h => h.Status != "Cancelada")
        .Sum(h => h.Total);

    public ICommand LimparBuscaCommand { get; }

    public HistoricoVendasViewModel(IUnitOfWork uow, IProductService productService)
    {
        _uow            = uow;
        _productService = productService;

        LimparBuscaCommand = new RelayCommand(_ =>
        {
            BuscaProduto       = string.Empty;
            ProdutoSelecionado = null;
            Historico.Clear();
            ProdutosSugestao.Clear();
        });
    }

    private async Task BuscarProdutosAsync(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Length < 2)
        {
            ProdutosSugestao.Clear();
            return;
        }

        try
        {
            if (termo.All(char.IsDigit) && termo.Length >= 4)
            {
                var porBarcode = await _productService.GetByBarcodeAsync(termo);
                if (porBarcode != null)
                {
                    ProdutoSelecionado = porBarcode;
                    ProdutosSugestao.Clear();
                    return;
                }
            }

            var result = await _productService.SearchAsync(termo);
            ProdutosSugestao.Clear();
            foreach (var p in result.Take(8)) ProdutosSugestao.Add(p);
        }
        catch { ProdutosSugestao.Clear(); }
    }

    private async Task CarregarHistoricoAsync()
    {
        Historico.Clear();
        if (ProdutoSelecionado is null) return;

        var itens = await _uow.Sales.GetHistoricoVendasPorProdutoAsync(ProdutoSelecionado.Id);
        foreach (var item in itens)
        {
            Historico.Add(new HistoricoVendaProdutoItem
            {
                DataVenda      = item.Sale.SaleDate,
                NumeroVenda    = item.Sale.SaleNumber,
                ClienteNome    = item.Sale.Customer?.Name ?? "Consumidor final",
                Quantidade     = item.Quantity,
                PrecoUnitario  = item.UnitPrice,
                Total          = item.TotalPrice,
                Status         = item.Sale.Status.ToString()
            });
        }

        OnPropertyChanged(nameof(QuantidadeTotalVendida));
        OnPropertyChanged(nameof(TotalVendido));
    }
}

/// <summary>Uma linha do histórico de vendas de um produto.</summary>
public class HistoricoVendaProdutoItem
{
    public DateTime DataVenda     { get; set; }
    public string   NumeroVenda   { get; set; } = string.Empty;
    public string   ClienteNome   { get; set; } = string.Empty;
    public decimal  Quantidade    { get; set; }
    public decimal  PrecoUnitario { get; set; }
    public decimal  Total         { get; set; }
    public string   Status        { get; set; } = string.Empty;
}
