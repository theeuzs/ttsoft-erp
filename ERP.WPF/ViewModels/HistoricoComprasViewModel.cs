// ── ERP.WPF/ViewModels/HistoricoComprasViewModel.cs ───────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Item 1.3 do roadmap: histórico de compras de um produto, organizado por
/// fornecedor. Injeção via construtor desde o início (Parte 0 do roadmap).
/// </summary>
public class HistoricoComprasViewModel : BaseViewModel
{
    private readonly IPedidoCompraService _service;
    private readonly IProductService      _productService;

    public ObservableCollection<ProductDto> ProdutosSugestao { get; } = new();
    public ObservableCollection<HistoricoCompraProdutoDto> Historico { get; } = new();

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
            OnPropertyChanged(nameof(TotalComprado));
            OnPropertyChanged(nameof(MenorPreco));
            OnPropertyChanged(nameof(MelhorFornecedor));
            _ = CarregarHistoricoAsync();
        }
    }

    public bool TemProdutoSelecionado => ProdutoSelecionado != null;

    /// <summary>
    /// Soma de quantidade × preço, só de pedidos já RECEBIDOS — Rascunho e
    /// Cancelado nunca viraram compra de verdade, não deveriam contar aqui.
    /// </summary>
    public decimal TotalComprado => Historico
        .Where(h => h.Status == "Recebido")
        .Sum(h => h.Total);

    /// <summary>Menor preço unitário já pago (só entre pedidos Recebidos).</summary>
    public decimal? MenorPreco => Historico.Any(h => h.Status == "Recebido")
        ? Historico.Where(h => h.Status == "Recebido").Min(h => h.PrecoUnitario)
        : null;

    /// <summary>Fornecedor do menor preço já pago (só entre pedidos Recebidos).</summary>
    public string? MelhorFornecedor => Historico
        .Where(h => h.Status == "Recebido")
        .OrderBy(h => h.PrecoUnitario)
        .FirstOrDefault()?.FornecedorNome;

    public ICommand LimparBuscaCommand { get; }

    public HistoricoComprasViewModel(IPedidoCompraService service, IProductService productService)
    {
        _service        = service;
        _productService = productService;

        LimparBuscaCommand = new RelayCommand(_ =>
        {
            BuscaProduto     = string.Empty;
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

        var itens = await _service.GetHistoricoPorProdutoAsync(ProdutoSelecionado.Id);
        foreach (var item in itens) Historico.Add(item);

        OnPropertyChanged(nameof(TotalComprado));
        OnPropertyChanged(nameof(MenorPreco));
        OnPropertyChanged(nameof(MelhorFornecedor));
    }
}