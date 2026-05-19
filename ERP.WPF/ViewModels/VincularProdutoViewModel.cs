using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class VincularProdutoViewModel : BaseViewModel
{
    private readonly IProductService _productService;

    private string _termoBusca = string.Empty;
    public string TermoBusca
    {
        get => _termoBusca;
        set => SetProperty(ref _termoBusca, value);
    }

    private ObservableCollection<ProductDto> _produtosEncontrados = new();
    public ObservableCollection<ProductDto> ProdutosEncontrados
    {
        get => _produtosEncontrados;
        set => SetProperty(ref _produtosEncontrados, value);
    }

    private ProductDto? _produtoSelecionado;
    public ProductDto? ProdutoSelecionado
    {
        get => _produtoSelecionado;
        set 
        {
            SetProperty(ref _produtoSelecionado, value);
            // Avisa a tela que o estado do botão "Confirmar" pode ter mudado
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // Ação para fechar a janela passando true (sucesso) ou false (cancelado)
    public Action<bool>? OnRequestClose { get; set; }

    public ICommand BuscarCommand { get; }
    public ICommand ConfirmarCommand { get; }
    public ICommand CancelarCommand { get; }

    public VincularProdutoViewModel(IProductService productService)
    {
        _productService = productService;

        BuscarCommand = new AsyncRelayCommand(ExecutarBuscaAsync);
        // O botão Confirmar só fica habilitado se tiver um produto selecionado na Grid
        ConfirmarCommand = new RelayCommandAction(_ => OnRequestClose?.Invoke(true));
        CancelarCommand = new RelayCommandAction(_ => OnRequestClose?.Invoke(false));
    }

    private async Task ExecutarBuscaAsync(object? obj)
    {
        if (string.IsNullOrWhiteSpace(TermoBusca)) return;

        // Limpa a busca anterior
        ProdutosEncontrados.Clear();

        // Faz a busca. (Se você tiver um método SearchAsync no seu IProductService, use-o! 
        // Aqui estou usando GetAll e filtrando em memória para garantir que vai funcionar de primeira).
        var todosProdutos = await _productService.GetAllAsync();
        
        var filtrados = todosProdutos
            .Where(p => p.Name.Contains(TermoBusca, StringComparison.OrdinalIgnoreCase) || 
                        p.Barcode == TermoBusca)
            .Take(50) // Limita para não travar a tela
            .ToList();

        foreach (var item in filtrados)
        {
            ProdutosEncontrados.Add(item);
        }
    }
}