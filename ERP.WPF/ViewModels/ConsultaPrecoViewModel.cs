using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class ConsultaPrecoViewModel : BaseViewModel
{
    private readonly IProductService _productService;

    private string _codigoBarras = string.Empty;
    public string CodigoBarras { get => _codigoBarras; set => SetProperty(ref _codigoBarras, value); }

    private ProductDto? _produtoEncontrado;
    public ProductDto? ProdutoEncontrado 
    { 
        get => _produtoEncontrado; 
        set 
        { 
            SetProperty(ref _produtoEncontrado, value); 
            OnPropertyChanged(nameof(NaoEncontradoVisivel)); 
            OnPropertyChanged(nameof(ProdutoVisivel));
            OnPropertyChanged(nameof(MensagemAtacado));
        } 
    }

    // 👇 NOVOS CAMPOS DA CALCULADORA 👇
    private decimal _quantidade = 1m;
    public decimal Quantidade 
    { 
        get => _quantidade; 
        set 
        { 
            SetProperty(ref _quantidade, value); 
            RecalcularTotal(); 
        } 
    }

    private decimal _total;
    public decimal Total { get => _total; set => SetProperty(ref _total, value); }

    private bool _isAtacadoAplicado;
    public bool IsAtacadoAplicado { get => _isAtacadoAplicado; set => SetProperty(ref _isAtacadoAplicado, value); }

    public string MensagemAtacado => ProdutoEncontrado != null && ProdutoEncontrado.WholesaleMinQuantity.HasValue && ProdutoEncontrado.WholesaleMinQuantity.Value > 0
        ? $"📦 ATACADO: {ProdutoEncontrado.WholesalePrice:C} (A partir de {ProdutoEncontrado.WholesaleMinQuantity.Value:N0} {UnidadeLabel})"
        : "";

    public string UnidadeLabel => !string.IsNullOrWhiteSpace(ProdutoEncontrado?.UnidadeVenda)
        ? ProdutoEncontrado.UnidadeVenda
        : (ProdutoEncontrado?.Unit ?? "UN");

    public string InfoConversao
    {
        get
        {
            if (ProdutoEncontrado == null || ProdutoEncontrado.FatorConversao <= 1m) return string.Empty;
            decimal qtdEstoque = Quantidade * ProdutoEncontrado.FatorConversao;
            return $"= {qtdEstoque:N2} {ProdutoEncontrado.UnidadeEstoque ?? ProdutoEncontrado.Unit} em estoque";
        }
    }

    public System.Windows.Visibility NaoEncontradoVisivel => ProdutoEncontrado == null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility ProdutoVisivel => ProdutoEncontrado != null ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public ICommand BuscarCommand { get; }
    public ICommand AdicionarCarrinhoCommand { get; }
    
    public Action? OnFechar { get; set; }
    // Action (Callback) para devolver o Produto pro PDV!
    public Action<ProductDto, decimal, bool>? OnAdicionarAoCarrinho { get; set; }

    public ConsultaPrecoViewModel(IProductService productService)
    {
        _productService = productService;
        BuscarCommand = new AsyncRelayCommand(_ => BuscarProdutoAsync());
        AdicionarCarrinhoCommand = new RelayCommand(_ => AdicionarAoCarrinho(), _ => ProdutoEncontrado != null && Quantidade > 0);
    }

    private async Task BuscarProdutoAsync()
    {
        if (string.IsNullOrWhiteSpace(CodigoBarras)) return;
        ProdutoEncontrado = await _productService.GetByBarcodeAsync(CodigoBarras);
        CodigoBarras = string.Empty; // Limpa para o próximo bip
        
        Quantidade = 1m; // Reseta a quantidade e já aciona o RecalcularTotal
    }

    private void RecalcularTotal()
    {
        if (ProdutoEncontrado == null) return;
        
        IsAtacadoAplicado = false;

        // 👇 Verifica se atingiu a meta do atacado
        if (ProdutoEncontrado.WholesaleMinQuantity.HasValue && 
            ProdutoEncontrado.WholesalePrice.HasValue && 
            Quantidade >= ProdutoEncontrado.WholesaleMinQuantity.Value)
        {
            IsAtacadoAplicado = true;

            // 🧠 CÁLCULO DE PACOTES (Igual ao do Carrinho)
            // Ex: Se digitou 2500, são 2 pacotes de 1000 + 500 de sobra
            decimal qtdPacotes = Math.Floor(Quantidade / ProdutoEncontrado.WholesaleMinQuantity.Value);
            decimal sobraUnidades = Quantidade % ProdutoEncontrado.WholesaleMinQuantity.Value;

            // Soma (Pacotes Fechados * Preço do Pacote) + (Sobra * Preço Normal)
            Total = (qtdPacotes * ProdutoEncontrado.WholesalePrice.Value) + (sobraUnidades * ProdutoEncontrado.SalePrice);
        }
        else
        {
            // Se não bateu o atacado, é preço de varejo normal
            Total = Quantidade * ProdutoEncontrado.SalePrice;
        }
    }

    private void AdicionarAoCarrinho()
    {
        if (ProdutoEncontrado != null && OnAdicionarAoCarrinho != null)
        {
            // Manda o produto de volta pro PDV e fecha a tela!
            OnAdicionarAoCarrinho.Invoke(ProdutoEncontrado, Quantidade, IsAtacadoAplicado);
            OnFechar?.Invoke();
        }
    }
}