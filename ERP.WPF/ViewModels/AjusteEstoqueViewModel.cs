using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks; // 👈 Adicionado para rodar buscas em segundo plano
using System.Windows;
using System.Windows.Input;
using ERP.Domain.Interfaces;
using ERP.WPF.Commands;

namespace ERP.WPF.ViewModels;

// 👇 Nossa classe auxiliar exclusiva para essa tela
public class ProdutoResumoAjuste
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Stock { get; set; }
}

public class AjusteEstoqueViewModel : BaseViewModel
{
    private readonly IUnitOfWork _uow;

    public ObservableCollection<ProdutoResumoAjuste> ListaProdutos { get; set; } = new();

    private ProdutoResumoAjuste? _produtoSelecionado;
    public ProdutoResumoAjuste? ProdutoSelecionado
    {
        get => _produtoSelecionado;
        set
        {
            _produtoSelecionado = value;
            OnPropertyChanged(nameof(ProdutoSelecionado));
            
            // 👇 Só atualiza a tela se o produto não for nulo (evita zerar ao pesquisar)
            if (_produtoSelecionado != null)
            {
                EstoqueAtual = _produtoSelecionado.Stock;
                NovoEstoque = EstoqueAtual; 
                DropDownAberto = false; // Fecha a gavetinha na hora!
            }
        }
    }

    // ====================================================================
    // 🚀 INÍCIO DA MÁGICA DE PERFORMANCE (Filtro sob demanda)
    // ====================================================================
    private string _filtro = string.Empty;
    public string Filtro
    {
        get => _filtro;
        set 
        { 
            _filtro = value; 
            OnPropertyChanged(nameof(Filtro));
            
            // 🛑 A TRAVA MÁGICA: Se o texto for igual ao nome do produto selecionado, corta a pesquisa!
            if (_produtoSelecionado != null && _filtro == _produtoSelecionado.Name)
                return;

            // Só dispara a busca se o usuário digitar pelo menos 3 caracteres!
            if (!string.IsNullOrWhiteSpace(_filtro) && _filtro.Length >= 3)
            {
                _ = CarregarProdutosFiltradosAsync(_filtro);
            }
            else if (string.IsNullOrWhiteSpace(_filtro))
            {
                // Limpa a lista se apagar tudo
                System.Windows.Application.Current.Dispatcher.Invoke(() => ListaProdutos.Clear());
            }
        }
    }
    private bool _dropDownAberto;
public bool DropDownAberto
{
    get => _dropDownAberto;
    set { _dropDownAberto = value; OnPropertyChanged(nameof(DropDownAberto)); }
}

    private async Task CarregarProdutosFiltradosAsync(string busca)
    {
        busca = busca.ToLower();
        
        // Busca todos os produtos ativos (uma forma segura que funciona em qualquer UnitOfWork)
        var todosProdutos = await _uow.Products.GetAllAsync();
        
        // Filtra na memória e TRAVA em 10 itens no máximo para o PC não travar
        var produtosFiltrados = todosProdutos
            .Where(p => p.Name.ToLower().Contains(busca) || 
                       (p.Barcode != null && p.Barcode.Contains(busca)))
            .Take(10)
            .ToList();

        // Atualiza a tela de forma segura usando o Dispatcher
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ListaProdutos.Clear();
            foreach (var p in produtosFiltrados)
            {
                ListaProdutos.Add(new ProdutoResumoAjuste 
                { 
                    Id = p.Id, 
                    Name = p.Name, 
                    Stock = p.Stock 
                });
            }
            DropDownAberto = ListaProdutos.Count > 0;
        });
    }
    // ====================================================================
    // 🚀 FIM DA MÁGICA DE PERFORMANCE
    // ====================================================================

    private decimal _estoqueAtual;
    public decimal EstoqueAtual
    {
        get => _estoqueAtual;
        set { _estoqueAtual = value; OnPropertyChanged(nameof(EstoqueAtual)); AtualizarDivergencia(); }
    }

    private decimal _novoEstoque;
    public decimal NovoEstoque
    {
        get => _novoEstoque;
        set { _novoEstoque = value; OnPropertyChanged(nameof(NovoEstoque)); AtualizarDivergencia(); }
    }

    private decimal _divergencia;
    public decimal Divergencia
    {
        get => _divergencia;
        set { _divergencia = value; OnPropertyChanged(nameof(Divergencia)); }
    }

    public string MotivoSelecionado { get; set; } = "Contagem Inicial / Implantação";

    public ICommand SalvarAjusteCommand { get; }

    public AjusteEstoqueViewModel(IUnitOfWork uow)
    {
        _uow = uow;
        SalvarAjusteCommand = new AsyncRelayCommand(ExecutarSalvarAjuste);
        
        // ⚠️ CarregarProdutos() FOI DELETADO DAQUI! O sistema agora abre em 0.1 segundo.
    }

    private void AtualizarDivergencia()
    {
        Divergencia = NovoEstoque - EstoqueAtual;
    }

    private async Task ExecutarSalvarAjuste(object? obj)
    {
        if (ProdutoSelecionado == null)
        {
            MessageBox.Show("Selecione um produto primeiro!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (NovoEstoque == EstoqueAtual)
        {
            MessageBox.Show("O estoque informado é igual ao atual. Nada a ajustar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var produto = await _uow.Products.GetByIdAsync(ProdutoSelecionado.Id);
            if (produto != null)
            {
                produto.Stock = NovoEstoque;
                _uow.Products.Update(produto);
                await _uow.CommitAsync();

                MessageBox.Show($"Estoque atualizado com sucesso!\nDivergência registrada: {Divergencia} itens.\nMotivo: {MotivoSelecionado}", 
                                "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);

                // Limpa tudo para o próximo ajuste
                ProdutoSelecionado = null;
                EstoqueAtual = 0; NovoEstoque = 0; Divergencia = 0;
                
                // Limpa o texto da caixa e os itens carregados para não ficar lixo na memória
                Filtro = string.Empty;
                System.Windows.Application.Current.Dispatcher.Invoke(() => ListaProdutos.Clear());
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar ajuste: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
