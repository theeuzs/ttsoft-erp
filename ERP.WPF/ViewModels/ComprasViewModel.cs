using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using ERP.WPF.Helpers;
using ERP.WPF.Reports;
using ERP.WPF.State;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.WPF.ViewModels;

public class ComprasViewModel : BaseViewModel
{
    // ── Lista principal ───────────────────────────────────────────────────
    public ObservableCollection<PedidoCompraDto> Pedidos { get; } = new();
    public ObservableCollection<string> FornecedoresDisponiveis { get; } = new();

    private PedidoCompraDto? _pedidoSelecionado;
    public PedidoCompraDto? PedidoSelecionado
    {
        get => _pedidoSelecionado;
        set { SetProperty(ref _pedidoSelecionado, value); AtualizarComandos(); }
    }

    // ── Formulário novo pedido ────────────────────────────────────────────
    private string _fornecedorNome = string.Empty;
    public string FornecedorNome
    {
        get => _fornecedorNome;
        set { SetProperty(ref _fornecedorNome, value); AtualizarComandos(); }
    }

    private DateTime? _dataPrevista;
    public DateTime? DataPrevista { get => _dataPrevista; set => SetProperty(ref _dataPrevista, value); }

    private string _observacoes = string.Empty;
    public string Observacoes { get => _observacoes; set => SetProperty(ref _observacoes, value); }

    // ── Itens do pedido em edição ─────────────────────────────────────────
    public ObservableCollection<PedidoCompraItemDto> ItensNovoPedido { get; } = new();

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
        set { SetProperty(ref _produtoSelecionado, value); AtualizarComandos(); }
    }

    public ObservableCollection<ProductDto> ProdutosSugestao { get; } = new();

    private decimal _qtdItem = 1;
    public decimal QtdItem { get => _qtdItem; set => SetProperty(ref _qtdItem, value); }

    private decimal _precoItem;
    public decimal PrecoItem { get => _precoItem; set => SetProperty(ref _precoItem, value); }

    public decimal TotalNovoPedido => ItensNovoPedido.Sum(i => i.Total);

    // ── Comandos ──────────────────────────────────────────────────────────
    public ICommand CarregarCommand       { get; }
    public ICommand SalvarPedidoCommand   { get; }
    public ICommand AdicionarItemCommand  { get; }
    public ICommand RemoverItemCommand    { get; }
    public ICommand EnviarCommand         { get; }
    public ICommand ReceberCommand        { get; }
    public ICommand CancelarPedidoCommand { get; }
    public ICommand LimparFormCommand     { get; }
    public ICommand ExportarPdfCommand    { get; }

    public ComprasViewModel()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        CarregarCommand       = new RelayCommand(async _ => await CarregarAsync());
        SalvarPedidoCommand   = new RelayCommand(async _ => await SalvarPedidoAsync(), _ => PodeCriarPedido());
        AdicionarItemCommand  = new RelayCommand(_ => AdicionarItem(), _ => PodeAdicionarItem());
        RemoverItemCommand    = new RelayCommand(p => RemoverItem(p as PedidoCompraItemDto));
        EnviarCommand         = new RelayCommand(async _ => await EnviarAsync(),
            _ => PedidoSelecionado?.Status == Domain.Enums.StatusPedidoCompra.Rascunho);
        ReceberCommand        = new RelayCommand(async _ => await ReceberAsync(),
            _ => PedidoSelecionado?.Status == Domain.Enums.StatusPedidoCompra.Enviado);
        CancelarPedidoCommand = new RelayCommand(async _ => await CancelarAsync(),
            _ => PedidoSelecionado?.Status is
                Domain.Enums.StatusPedidoCompra.Rascunho or
                Domain.Enums.StatusPedidoCompra.Enviado);
        LimparFormCommand     = new RelayCommand(_ => LimparForm());
        ExportarPdfCommand    = new RelayCommand(_ => ExportarPdf(), _ => Pedidos.Any());

        _ = CarregarAsync();
    }

    // ── PDF ───────────────────────────────────────────────────────────────
    private void ExportarPdf()
    {
        var config = ConfiguracaoService.Carregar();
        var doc = new ComprasPdfReport(
            config,
            DateTime.Today.AddDays(-30),
            DateTime.Today,
            Pedidos);
        PdfReportBase.SalvarEAbrir(doc, "Compras");
    }

    // ── Carregamento ──────────────────────────────────────────────────────
    private async Task CarregarAsync()
{
    IsBusy = true;
    try
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPedidoCompraService>();
        
        // 1. Carrega os Pedidos
        var lista = await service.GetAllAsync();
        Pedidos.Clear();
        foreach (var p in lista) Pedidos.Add(p);

        // 2. BUSCA OS FORNECEDORES CADASTRADOS NO BANCO!
        var supService = scope.ServiceProvider.GetRequiredService<ISupplierService>();
        var fornDtos = await supService.GetAllAsync();
        var fornecedores = fornDtos.Select(f => f.Name).ToList();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            FornecedoresDisponiveis.Clear();
            foreach (var f in fornecedores) FornecedoresDisponiveis.Add(f);
        });

        CommandManager.InvalidateRequerySuggested();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Erro ao carregar pedidos:\n{ex.Message}", "Erro",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
    finally { IsBusy = false; }
}

    // ── Busca de produto ──────────────────────────────────────────────────
    private async Task BuscarProdutosAsync(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Length < 2)
        {
            ProdutosSugestao.Clear();
            return;
        }
        try
        {
            using var scope = App.Services.CreateScope();
            var productService = scope.ServiceProvider.GetRequiredService<IProductService>();
            // ← Tenta barcode primeiro se parecer um código (só números)
        if (termo.All(char.IsDigit) && termo.Length >= 4)
        {
            var porBarcode = await productService.GetByBarcodeAsync(termo);
            if (porBarcode != null)
            {
                ProdutoSelecionado = porBarcode;
                ProdutosSugestao.Clear();
                return;
            }
        }
            var result = await productService.SearchAsync(termo);
            ProdutosSugestao.Clear();
            foreach (var p in result.Take(8)) ProdutosSugestao.Add(p);
        }
        catch { ProdutosSugestao.Clear(); }
    }

    // ── Adicionar item ────────────────────────────────────────────────────
    private void AdicionarItem()
{
    if (ProdutoSelecionado == null) return;

    var existente = ItensNovoPedido.FirstOrDefault(i => i.ProductId == ProdutoSelecionado.Id);
    if (existente != null)
        existente.Quantidade += QtdItem;
    else
        ItensNovoPedido.Add(new PedidoCompraItemDto
        {
            ProductId     = ProdutoSelecionado.Id,
            ProductName   = ProdutoSelecionado.Name,
            Quantidade    = QtdItem,
            PrecoUnitario = PrecoItem > 0 ? PrecoItem : ProdutoSelecionado.SalePrice,
        });

    OnPropertyChanged(nameof(TotalNovoPedido));
    
    // 👇 A MÁGICA TÁ AQUI: Avisa os botões que a lista mudou!
    AtualizarComandos(); 

    BuscaProduto       = string.Empty;
    ProdutoSelecionado = null;
    QtdItem            = 1;
    PrecoItem          = 0;
}

    private void RemoverItem(PedidoCompraItemDto? item)
{
    if (item == null) return;
    ItensNovoPedido.Remove(item);
    OnPropertyChanged(nameof(TotalNovoPedido));
    
    // 👇 A MÁGICA TÁ AQUI TAMBÉM: Se o cara remover tudo, o botão de Salvar apaga!
    AtualizarComandos(); 
}

    // ── Salvar pedido ─────────────────────────────────────────────────────
    private async Task SalvarPedidoAsync()
    {
        IsBusy = true;
        try
        {
            var dto = new CreatePedidoCompraDto
            {
                FornecedorNome = FornecedorNome,
                DataPrevista   = DataPrevista,
                Observacoes    = Observacoes,
                CriadoPor      = AppSession.UserName,
                Itens          = ItensNovoPedido.Select(i => new CreatePedidoCompraItemDto
                {
                    ProductId     = i.ProductId,
                    ProductName   = i.ProductName,
                    Quantidade    = i.Quantidade,
                    PrecoUnitario = i.PrecoUnitario,
                }).ToList()
            };

            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPedidoCompraService>();
            await service.CriarAsync(dto);
            LimparForm();
            await CarregarAsync();
            MessageBox.Show("✅ Pedido de compra criado com sucesso!", "Sucesso",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar pedido:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    // ── Ações de status ───────────────────────────────────────────────────
    private async Task EnviarAsync()
    {
        if (PedidoSelecionado == null) return;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPedidoCompraService>();
            var idAtual = PedidoSelecionado.Id;
            await service.EnviarAsync(idAtual);
            await CarregarAsync();
            // Re-seleciona o pedido para atualizar os botões com o novo status
            PedidoSelecionado = Pedidos.FirstOrDefault(p => p.Id == idAtual);
            MessageBox.Show("Pedido marcado como ENVIADO ao fornecedor.", "Status atualizado",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task ReceberAsync()
    {
        if (PedidoSelecionado == null) return;
        var confirm = MessageBox.Show(
            $"Confirmar recebimento do pedido {PedidoSelecionado.Numero}?\n\n" +
            "O estoque dos produtos será atualizado automaticamente.",
            "Confirmar Recebimento", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPedidoCompraService>();
            var idAtual = PedidoSelecionado.Id;
            await service.ReceberAsync(idAtual);
            await CarregarAsync();
            PedidoSelecionado = Pedidos.FirstOrDefault(p => p.Id == idAtual);
            MessageBox.Show("✅ Mercadoria recebida! Estoque atualizado.", "Recebimento",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsBusy = false; }
    }

    private async Task CancelarAsync()
    {
        if (PedidoSelecionado == null) return;
        var confirm = MessageBox.Show($"Cancelar pedido {PedidoSelecionado.Numero}?",
            "Cancelar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IPedidoCompraService>();
            await service.CancelarAsync(PedidoSelecionado.Id);
            await CarregarAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private bool PodeCriarPedido() =>
        !string.IsNullOrWhiteSpace(FornecedorNome) && ItensNovoPedido.Any();

    private bool PodeAdicionarItem() =>
        ProdutoSelecionado != null && QtdItem > 0;

    private void LimparForm()
    {
        FornecedorNome = string.Empty;
        DataPrevista   = null;
        Observacoes    = string.Empty;
        ItensNovoPedido.Clear();
        OnPropertyChanged(nameof(TotalNovoPedido));
    }

    private void AtualizarComandos() =>
        CommandManager.InvalidateRequerySuggested();
}