using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ERP.WPF.Helpers;
using System.Windows.Input;
using System.Windows.Media;

namespace ERP.WPF.ViewModels;

public class CatalogoViewModel : BaseViewModel
{
    private readonly IProductService _productService;

    // Ação chamada quando o usuário adiciona produto ao carrinho do PDV
    public Action<ProductDto>? OnAdicionarAoCarrinho { get; set; }

    public ObservableCollection<CatalogoProdutoItem> Produtos { get; } = new();

    private string _filtroBusca = string.Empty;
    public string FiltroBusca
    {
        get => _filtroBusca;
        set => SetProperty(ref _filtroBusca, value);
    }

    private string _categoriaFiltro = "Todos";
    public string CategoriaFiltro
    {
        get => _categoriaFiltro;
        set { SetProperty(ref _categoriaFiltro, value); _ = BuscarAsync(); }
    }

    private string _ordenacao = "Nome A-Z";
    public string Ordenacao
    {
        get => _ordenacao;
        set { SetProperty(ref _ordenacao, value); Reordenar(); }
    }

    private int _totalProdutos;
    public int TotalProdutos
    {
        get => _totalProdutos;
        set => SetProperty(ref _totalProdutos, value);
    }

    public ICommand BuscarCommand         { get; }
    public ICommand AdicionarAoCarrinhoCommand { get; }

    public CatalogoViewModel(IProductService productService)
    {
        _productService            = productService;
        BuscarCommand              = new AsyncRelayCommand(_ => BuscarAsync());
        AdicionarAoCarrinhoCommand = new RelayCommand(p =>
        {
            if (p is CatalogoProdutoItem item)
                OnAdicionarAoCarrinho?.Invoke(item.Original);
        });

        BuscarAsync().SafeFireAndForgetSilentAsync("Catalogo");
    }

    private async Task BuscarAsync()
    {
        IsBusy = true;
        var todos = await _productService.GetAllAsync();

        var filtrado = todos.Where(p => p.IsActive);

        // Filtro de categoria (pisos, revestimentos e variantes)
        var categoriasPiso = new[] { "Piso", "Revestimento", "Porcelanato", "Cerâmica", "Mosaico", "Pisos", "Revestimentos" };

        if (CategoriaFiltro == "Todos")
            filtrado = filtrado.Where(p => categoriasPiso.Any(cat =>
                (p.CategoryName ?? "").Contains(cat, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Piso", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Revestimento", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Porcelanato", StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains("Cerâmica", StringComparison.OrdinalIgnoreCase)));
        else
            filtrado = filtrado.Where(p =>
                (p.CategoryName ?? "").Contains(CategoriaFiltro, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Contains(CategoriaFiltro, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(FiltroBusca))
            filtrado = filtrado.Where(p =>
                p.Name.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase) ||
                (p.Barcode ?? "").Contains(FiltroBusca));

        Produtos.Clear();
        foreach (var p in filtrado.Select(p => new CatalogoProdutoItem(p)))
            Produtos.Add(p);

        TotalProdutos = Produtos.Count;
        Reordenar();
        IsBusy = false;
    }

    private void Reordenar()
    {
        var ordenado = Ordenacao switch
        {
            "Menor Preço" => Produtos.OrderBy(p => p.SalePrice).ToList(),
            "Maior Preço" => Produtos.OrderByDescending(p => p.SalePrice).ToList(),
            "Estoque"     => Produtos.OrderByDescending(p => p.Stock).ToList(),
            _             => Produtos.OrderBy(p => p.Name).ToList()
        };
        Produtos.Clear();
        foreach (var p in ordenado) Produtos.Add(p);
    }
}

/// <summary>Wrapper do ProductDto com propriedades extras para o catálogo visual.</summary>
public class CatalogoProdutoItem : BaseViewModel
{
    public ProductDto Original { get; }

    public Guid    Id          => Original.Id;
    public string  Name        => Original.Name;
    public decimal SalePrice   => Original.SalePrice;
    public decimal Stock       => Original.Stock;
    public string  Unit        => Original.Unit ?? "m²";
    public bool    EmCampanha  => Original.EmCampanha;
    public string? ImageUrl    => Original.ImageUrl;
    public bool    TemImagem   => !string.IsNullOrWhiteSpace(Original.ImageUrl);
    public bool    SemImagem   => string.IsNullOrWhiteSpace(Original.ImageUrl);
    public string? CategoryName => Original.CategoryName;

    public string CorEstoque => Stock > 10 ? "#27AE60" : Stock > 0 ? "#F59E0B" : "#EF4444";

    /// <summary>Extrai dimensões da descrição detalhada (ex: "60x60cm", "45x90cm").</summary>
    public string DimensoesExibicao
    {
        get
        {
            var desc = Original.DescricaoDetalhada ?? "";
            var m = System.Text.RegularExpressions.Regex.Match(desc, @"\d+x\d+\s*cm");
            return m.Success ? m.Value : (Original.CategoryName ?? "Piso/Revestimento");
        }
    }

    public CatalogoProdutoItem(ProductDto p) => Original = p;
}
