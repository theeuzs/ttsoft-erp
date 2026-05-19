using ERP.WPF.Helpers;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using ERP.WPF.State; 
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace ERP.WPF.ViewModels;

public class CartItem : BaseViewModel
{
    // 👇 ADICIONADO: Propriedade Product para o Motor Fiscal ler os percentuais de IPI/ICMS 👇
    public ERP.Domain.Entities.Product ProdutoOriginal { get; set; } = new();

    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;

    // 👇 O GATILHO DA LÂMPADA 👇
    private bool _hasSuggestions;
    public bool HasSuggestions
    {
        get => _hasSuggestions;
        set { SetProperty(ref _hasSuggestions, value); }
    }

    // 👇 ADICIONADO: O campo para gravar a observação individual do item no carrinho 👇
    private string _observacao = string.Empty;
    public string Observacao
    {
        get => _observacao;
        set { SetProperty(ref _observacao, value); }
    }

    // ==========================================
    // 🧠 O CÉREBRO DO ATACADO ENTRA AQUI 🧠
    // ==========================================
    public decimal NormalUnitPrice { get; set; } // Guarda o preço de varejo padrão
    public decimal? WholesaleMinQuantity { get; set; }
    public decimal? WholesalePrice       { get; set; }
    // Sprint C: preços por grupo de cliente
    public decimal  PrecoBRevendedor     { get; set; } = 0;
    public decimal  PrecoCAtacadista     { get; set; } = 0;
    public bool IsWholesaleActive => WholesaleMinQuantity.HasValue && WholesalePrice.HasValue && Quantity >= WholesaleMinQuantity.Value;

    private void UpdatePriceBasedOnQuantity()
    {
        if (WholesaleMinQuantity.HasValue && WholesalePrice.HasValue && _quantity >= WholesaleMinQuantity.Value)
        {
            UnitPrice = Math.Round(Total / Quantity, 2); 
        }
        else
        {
            UnitPrice = NormalUnitPrice;
        }
    }

    private decimal _quantity = 1;
    public decimal Quantity
    {
        get => _quantity;
        set 
        { 
            SetProperty(ref _quantity, value); 
            UpdatePriceBasedOnQuantity(); 
            OnPropertyChanged(nameof(Total)); 
            OnPropertyChanged(nameof(IsWholesaleActive));
            OnPropertyChanged(nameof(QuantidadeTexto));
        }
    }

    /// <summary>Campo de texto para o campo QTD do carrinho — aceita vírgula e ponto.</summary>
    public string QuantidadeTexto
    {
        get => _quantity.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string normalizado = value.Trim().Replace(",", ".");
            // Ignora enquanto o usuário ainda está digitando (ex: "5." ou "5,")
            if (normalizado.EndsWith(".") || normalizado.EndsWith(",")) return;
            if (decimal.TryParse(normalizado, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal resultado) && resultado > 0)
            {
                Quantity = resultado;
            }
        }
    }

    private decimal _unitPrice;
    public decimal UnitPrice
    {
        get => _unitPrice;
        set { SetProperty(ref _unitPrice, value); OnPropertyChanged(nameof(Total)); }
    }

    private decimal _discountPercent;
    public decimal DiscountPercent
    {
        get => _discountPercent;
        set { SetProperty(ref _discountPercent, value); OnPropertyChanged(nameof(Total)); }
    }

    // 👇 A MÁGICA DA BARRA CRAVADA (EX: 19,90) 👇
    public decimal Total 
    {
        get 
        {
            // Se tem um total fixo salvo (reimpressão do histórico), usa ele direto
            if (TotalSalvo > 0) return TotalSalvo;

            // Se bateu a quantidade de atacado, usamos o preço "travado" para o lote
            if (WholesaleMinQuantity.HasValue && WholesalePrice.HasValue && Quantity >= WholesaleMinQuantity.Value)
            {
                // Calcula quantas barras inteiras tem (ex: 6 unidades = 1 barra)
                decimal qtdPacotes = Math.Floor(Quantity / WholesaleMinQuantity.Value);
                decimal sobraUnidades = Quantity % WholesaleMinQuantity.Value;

                // Preço total = (Barras Fechadas * Preço da Barra) + (Sobra * Preço Normal)
                decimal totalAtacado = (qtdPacotes * WholesalePrice.Value) + (sobraUnidades * NormalUnitPrice);
                return totalAtacado * (1 - DiscountPercent / 100);
            }

            // Se não é atacado, faz a conta normal de varejo
            return Quantity * NormalUnitPrice * (1 - DiscountPercent / 100);
        }
    }

    public string Ncm { get; set; } = string.Empty;
    public string Cest { get; set; } = string.Empty;
    public string Cfop { get; set; } = "5102";
    public string Csosn { get; set; } = "102";
    public string IcmsOrigem { get; set; } = "0";

    // ── Total fixo (para reimpressão do histórico) ──────────────────────
    /// <summary>
    /// Quando definido (> 0), Total retorna este valor em vez de recalcular.
    /// Usado na reimpressão do histórico para evitar diferença de centavos.
    /// </summary>
    public decimal TotalSalvo { get; set; }

    // ── Conversão de unidade ─────────────────────────────────────────
    /// <summary>Ex: 6 (1 barra = 6 metros). Default 1 = sem conversão.</summary>
    public decimal FatorConversao { get; set; } = 1m;
    /// <summary>Label exibido no recibo. Ex: "Barra(s)"</summary>
    public string? LabelUnidadeVenda { get; set; }
    public string UnidadeEstoque { get; set; } = string.Empty;

    /// <summary>Quantidade em UnidadeEstoque que vai baixar do estoque.</summary>
    public decimal QuantidadeEstoque => Quantity * FatorConversao;

    /// <summary>Label amigável para o recibo: "2 Barra(s)" ou "5 UN"</summary>
    public string QuantidadeLabel => string.IsNullOrWhiteSpace(LabelUnidadeVenda)
        ? $"{Quantity:N2} {UnidadeEstoque}"
        : $"{Quantity:N2} {LabelUnidadeVenda}";
}


public class PdvViewModel : BaseViewModel
{
    private readonly IProductService _productService;
    private readonly ISaleService _saleService;
    private readonly ICustomerService _customerService; 
    private readonly ICaixaService _caixaService;
    private readonly IOrcamentoService _orcamentoService;
    // 👇 INJEÇÃO DO MOTOR FISCAL 👇
    private readonly IMotorFiscalService _motorFiscal;
    private readonly IProdutoAgregadoService _produtoAgregadoService;

    // ==========================================
    // 🧠 MEMÓRIA GLOBAL DO CARRINHO (NÃO APAGA AO TROCAR DE TELA)
    // ==========================================
    private static readonly List<CartItem> _carrinhoMemoria = new();
    private static Guid? _clienteIdMemoria = null;
    private static string _clienteNomeMemoria = "Consumidor Final";
    private static decimal _descontoMemoria = 0;

    // 👇 PASSO 1: A variável que lembra a venda EXCLUSIVA deste terminal
    private Guid? _idUltimaVendaDesteCaixa = null;

    private void SalvarEstadoCarrinho()
    {
        _carrinhoMemoria.Clear();
        _carrinhoMemoria.AddRange(CartItems);
        _clienteIdMemoria = _selectedCustomerId;
        _clienteNomeMemoria = SelectedCustomerName;
        _descontoMemoria = DiscountAmount;
    }

    private void RestaurarEstadoCarrinho()
    {
        if (_carrinhoMemoria.Any() || _clienteIdMemoria.HasValue)
        {
            CartItems.Clear();
            foreach (var item in _carrinhoMemoria)
            {
                CartItems.Add(item);
            }

            _selectedCustomerId = _clienteIdMemoria;
            SelectedCustomerName = _clienteNomeMemoria;
            DiscountAmount = _descontoMemoria;
        }
    }

    // ==========================================
    // 1. LÓGICA DO CAIXA
    // ==========================================
    
    public static Action? NotificacaoCaixaAlterado;

    private bool _isCaixaAberto = false;
    public static ERP.Domain.Entities.Orcamento? OrcamentoPendente = null;
    private decimal _valorAtualCaixa = 0;
    private bool _isGridView = false;
    public bool IsGridView { get => _isGridView; set => SetProperty(ref _isGridView, value); }
    public ICommand ToggleViewCommand { get; }
    public ICommand AbrirConsultaRapidaCommand { get; }
    // Comando para abrir as sugestões ao clicar na lâmpada
    public ICommand AbrirSugestoesItemCommand { get; }

    public bool IsCaixaAberto
    {
        get => _isCaixaAberto;
        set 
        {
            if (_isCaixaAberto != value)
            {
                _isCaixaAberto = value;
                OnPropertyChanged(nameof(IsCaixaAberto));
                OnPropertyChanged(nameof(CaixaFechadoVisivel));
                OnPropertyChanged(nameof(CaixaAbertoVisivel));
            }
        }
    }

    public decimal ValorAtualCaixa
    {
        get => _valorAtualCaixa;
        set 
        {
            if (_valorAtualCaixa != value)
            {
                _valorAtualCaixa = value;
                OnPropertyChanged(nameof(ValorAtualCaixa));
            }
        }
    }

    public System.Windows.Visibility CaixaFechadoVisivel => !IsCaixaAberto ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility CaixaAbertoVisivel => IsCaixaAberto ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    private Visibility _modoContingenciaVisivel = Visibility.Collapsed;
    public Visibility ModoContingenciaVisivel
    {
        get => _modoContingenciaVisivel;
        set => SetProperty(ref _modoContingenciaVisivel, value);
    }
    public ICommand MostrarTelaAbrirCaixaCommand => new RelayCommand(_ => AbrirTelaCaixa());
    public ICommand MostrarResumoCaixaCommand => new RelayCommand(_ => AbrirResumoCaixa());
    public ICommand AbrirResumoCaixaCommand { get; }
    public ICommand ReimprimirUltimoReciboCommand { get; }

    private void AbrirTelaCaixa()
    {
        var vm = new AbrirCaixaViewModel(_caixaService, AppSession.UserId, AppSession.UserName);
        var view = new Views.AbrirCaixaView(vm);

        vm.OnCaixaAberto = (valorAberturaDigitado) =>
        {
            IsCaixaAberto = true;
            ValorAtualCaixa = valorAberturaDigitado; 
            NotificacaoCaixaAlterado?.Invoke();
        };

        view.ShowDialog(); 
    }

    private void AbrirResumoCaixa()
    {
        var vm = new ResumoCaixaViewModel();
        var view = new Views.ResumoCaixaView();
        
        view.DataContext = vm;
        vm.OnFechar = view.Close;
        
        vm.OnEncerrarCaixa = async () =>
        {
            try
            {
                await _caixaService.FecharCaixaAsync(AppSession.UserId);
                IsCaixaAberto = false;
                ValorAtualCaixa = 0;
                NotificacaoCaixaAlterado?.Invoke();
                MessageBox.Show("Caixa encerrado com sucesso!\nO sistema está bloqueado para novas vendas.", "TTSoft");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao fechar o caixa no banco: " + ex.Message);
            }
        };

        view.ShowDialog();
    }

    // ==========================================
    // --- CONSTRUTOR --- 
    // ==========================================
    public PdvViewModel(IProductService productService, ISaleService saleService, ICustomerService customerService, ICaixaService caixaService, IOrcamentoService orcamentoService, IMotorFiscalService motorFiscal, IProdutoAgregadoService produtoAgregadoService)
    {
        _productService = productService;
        _saleService = saleService;
        _customerService = customerService; 
        _caixaService = caixaService;
        _orcamentoService = orcamentoService;
        _motorFiscal             = motorFiscal;
        _produtoAgregadoService  = produtoAgregadoService;

        SearchProductCommand = new AsyncRelayCommand(_ => SearchProductAsync());
        CarregarProdutosCampanhaAsync().SafeFireAndForgetSilentAsync("PDV-Campanha");
        AddToCartCommand = new RelayCommand(p => AddToCart(p as ProductDto), p => p is ProductDto);
        RemoveFromCartCommand = new RelayCommand(p => RemoveFromCart(p as CartItem), p => p is CartItem);
        FinalizeSaleCommand = new AsyncRelayCommand(async _ => await FinalizeSaleAsync(), _ => CartItems.Any());
        ClearCartCommand      = new RelayCommand(_ => ClearCart(),        _ => CartItems.Any());
        AplicarMarkup5Command    = new RelayCommand(_ => AplicarMarkup5(),      _ => CartItems.Any());
        SuspenderVendaCommand    = new RelayCommand(_ => SuspenderVenda(),      _ => CartItems.Any());
        RetormarVendaCommand     = new RelayCommand(p  => RetormarVenda(p as VendaSuspensa), _ => VendasSuspensas.Any());
        SearchCustomerCommand = new AsyncRelayCommand(_ => SearchCustomerAsync());
        SalvarOrcamentoCommand = new AsyncRelayCommand(async _ => await SalvarOrcamentoAsync(), _ => CartItems.Any());
        ToggleViewCommand = new RelayCommand(_ => IsGridView = !IsGridView);
        AbrirConsultaRapidaCommand = new RelayCommand(_ => AbrirConsultaRapida());
        SelectCustomerCommand = new RelayCommand(c => SelectCustomer(c as CustomerDto));
        LimparClienteCommand = new RelayCommand(_ => LimparCliente(), _ => _selectedCustomerId.HasValue);
        IncreaseQtyCommand = new RelayCommand(c => IncreaseQty(c as CartItem));
        DecreaseQtyCommand = new RelayCommand(c => DecreaseQty(c as CartItem));
        AbrirResumoCaixaCommand = new ERP.WPF.Commands.RelayCommand(AbrirResumoCaixa);
        
        // Sprint 1: Produtos Agregados
        AdicionarSugestaoCommand = new RelayCommand(
            p => AdicionarSugestaoAoCarrinho(p as ProdutoAgregadoDto),
            p => p is ProdutoAgregadoDto);
        FecharSugestoesCommand = new RelayCommand(_ => FecharSugestoes());
        
        // Gatilho Manual da Lâmpada
        AbrirSugestoesItemCommand = new ERP.WPF.Commands.RelayCommand(async p => {
            if (p is Guid id) await CarregarSugestoesAsync(id);
        });

        ReimprimirUltimoReciboCommand = new ERP.WPF.Commands.RelayCommand(async (_) => await ReimprimirUltimoReciboAsync());
        
        NotificacaoCaixaAlterado -= EscutarRadio;
        NotificacaoCaixaAlterado += EscutarRadio;

        _ = VerificarCaixaAbertoAsync(); 
        
        RestaurarEstadoCarrinho();
        
        VerificarOrcamentoPendente();
        _ = IniciarRadarSefazAsync();

        // Sprint 5: carrega meta do dia e vendas em background
        _ = CarregarMetaEVendasAsync();
        AtualizarClientesFrequentes();
    }

    private void EscutarRadio()
    {
        _ = AtualizarBotaoVerdeAsync();
    }

    private string _customerSearchTerm = string.Empty;
    public string CustomerSearchTerm 
    { 
        get => _customerSearchTerm; 
        set 
        { 
            if (SetProperty(ref _customerSearchTerm, value))
            {
                _ = SearchCustomerListAsync();
            }
        }
    }

    private string _selectedCustomerName = "Consumidor Final";
    public string SelectedCustomerName 
    { 
        get => _selectedCustomerName; 
        set { _selectedCustomerName = value; OnPropertyChanged(nameof(SelectedCustomerName)); } 
    }

    private Guid? _selectedCustomerId;
    private Guid? _orcamentoCarregadoId = null;

    private string _searchTerm = string.Empty;
    public string SearchTerm
    {
        get => _searchTerm;
        set 
        { 
            if (SetProperty(ref _searchTerm, value))
            {
                _ = SearchProductAsync();
            }
        }
    }

    public ObservableCollection<ProductDto> SearchResults { get; } = new();
    public ObservableCollection<CustomerDto> CustomerSearchResults { get; } = new();

    public ICommand SelectCustomerCommand { get; }
    public ICommand LimparClienteCommand { get; }
    public ICommand IncreaseQtyCommand { get; }
    public ICommand DecreaseQtyCommand { get; }

    // Controla visibilidade do botão 'X' de remover cliente
    public bool TemClienteSelecionado => _selectedCustomerId.HasValue;

    public ObservableCollection<CartItem>   CartItems       { get; } = new();
    public ObservableCollection<ProductDto>    ProdutosCampanha  { get; } = new();
    // Static: persiste entre navegações mesmo se o ViewModel for recriado
    private static readonly ObservableCollection<VendaSuspensa> _vendasSuspensasGlobal = new();
    public ObservableCollection<VendaSuspensa> VendasSuspensas => _vendasSuspensasGlobal;

    private bool _temVendasSuspensas;
    public bool TemVendasSuspensas
    {
        get => _vendasSuspensasGlobal.Any();
        set => SetProperty(ref _temVendasSuspensas, value);
    }

    private decimal _discountAmount;
    public decimal DiscountAmount { get => _discountAmount; set { SetProperty(ref _discountAmount, value); OnPropertyChanged(nameof(Total)); } }
    
    public decimal Subtotal => CartItems.Sum(i => i.Total);
    public decimal Total => Subtotal - DiscountAmount;

    // ── Sprint 5: Indicador de Meta ───────────────────────────────────────────
    private decimal _metaDia;
    private decimal _vendidoHoje;

    public decimal MetaDia
    {
        get => _metaDia;
        set { SetProperty(ref _metaDia, value); OnPropertyChanged(nameof(PercentualMeta)); OnPropertyChanged(nameof(TextoMeta)); OnPropertyChanged(nameof(TemMeta)); }
    }
    public decimal VendidoHoje
    {
        get => _vendidoHoje;
        set { SetProperty(ref _vendidoHoje, value); OnPropertyChanged(nameof(PercentualMeta)); OnPropertyChanged(nameof(TextoMeta)); }
    }
    public double  PercentualMeta => MetaDia > 0 ? Math.Min(100, (double)(VendidoHoje / MetaDia * 100)) : 0;
    public string  TextoMeta      => MetaDia > 0 ? $"Meta do dia: {MetaDia:C0} | Vendido: {VendidoHoje:C0} ({PercentualMeta:F0}%)" : "";
    public bool    TemMeta        => MetaDia > 0;

    // ── Sprint 5: Destaque último item adicionado ─────────────────────────────
    private Guid? _ultimoItemId;
    public  Guid? UltimoItemId { get => _ultimoItemId; set => SetProperty(ref _ultimoItemId, value); }

    // ── Sprint C: Grupo de preço do cliente selecionado ──────────────────────────
    private ERP.Domain.Enums.GrupoPreco _grupoPrecoCliente = ERP.Domain.Enums.GrupoPreco.A;
    public  ERP.Domain.Enums.GrupoPreco GrupoPrecoCliente
    {
        get => _grupoPrecoCliente;
        private set { _grupoPrecoCliente = value; AplicarPrecosGrupoAoCarrinho(); }
    }

    private string _labelGrupoPreco = "";
    public string LabelGrupoPreco { get => _labelGrupoPreco; private set => SetProperty(ref _labelGrupoPreco, value); }

    private void AplicarPrecosGrupoAoCarrinho()
    {
        // Re-aplica o preço de grupo em todos os itens do carrinho que ainda têm preço padrão
        foreach (var item in CartItems)
        {
            var precoGrupo = item.PrecoBRevendedor > 0 || item.PrecoCAtacadista > 0
                ? _grupoPrecoCliente switch
                {
                    ERP.Domain.Enums.GrupoPreco.B when item.PrecoBRevendedor > 0 => item.PrecoBRevendedor,
                    ERP.Domain.Enums.GrupoPreco.C when item.PrecoCAtacadista > 0 => item.PrecoCAtacadista,
                    _ => item.NormalUnitPrice
                }
                : item.NormalUnitPrice;

            if (precoGrupo != item.UnitPrice)
                item.UnitPrice = precoGrupo;
        }
        RefreshTotals();

        LabelGrupoPreco = _grupoPrecoCliente switch
        {
            ERP.Domain.Enums.GrupoPreco.B => "💼 Preço Revendedor",
            ERP.Domain.Enums.GrupoPreco.C => "🏭 Preço Atacadista",
            _                              => ""
        };
    }

    // ── Sprint C: Clientes frequentes (últimos 5 atendidos na sessão) ─────────
    public ObservableCollection<CustomerDto> ClientesFrequentes { get; } = new();
    private static readonly List<CustomerDto> _historicoCli = new();

    public bool HasClientesFrequentes => ClientesFrequentes.Count > 0;

    private void RegistrarClienteFrequente(CustomerDto c)
    {
        _historicoCli.RemoveAll(x => x.Id == c.Id);
        _historicoCli.Insert(0, c);
        if (_historicoCli.Count > 5) _historicoCli.RemoveAt(5);
        AtualizarClientesFrequentes();
    }
    private void AtualizarClientesFrequentes()
    {
        ClientesFrequentes.Clear();
        foreach (var c in _historicoCli) ClientesFrequentes.Add(c);
        OnPropertyChanged(nameof(HasClientesFrequentes));
    }

    // ── Sprint 1: Produtos Agregados / Auto-Sugestão ──────────────────────────

    public Action<IEnumerable<ProdutoAgregadoDto>>? OnSugestoesCarregadas { get; set; }

    public ObservableCollection<ProdutoAgregadoDto> Sugestoes { get; } = new();

    private bool _mostrarSugestoes;
    public bool MostrarSugestoes
    {
        get => _mostrarSugestoes;
        set { _mostrarSugestoes = value; OnPropertyChanged(); }
    }

    public ICommand AdicionarSugestaoCommand { get; }
    public ICommand FecharSugestoesCommand   { get; }

    // 👇 O MÉTODO ATUALIZADO PARA EXIBIR AVISO SE TUDO JÁ ESTIVER NO CARRINHO 👇
    private async Task CarregarSugestoesAsync(Guid productId)
    {
        try
        {
            var sugestoes = (await _produtoAgregadoService.GetSugestoesAsync(productId)).ToList();
            if (!sugestoes.Any()) return;

            var noCarrinho = CartItems.Select(c => c.ProductId).ToHashSet();
            var visiveis   = sugestoes.Where(s => !noCarrinho.Contains(s.ProdutoRelacionadoId)).ToList();
            
            if (!visiveis.Any()) 
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() => 
                    MessageBox.Show("Todas as sugestões para este item já foram adicionadas ao carrinho!", "Upsell", MessageBoxButton.OK, MessageBoxImage.Information));
                return;
            }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Sugestoes.Clear();
                foreach (var s in visiveis) Sugestoes.Add(s);
                OnSugestoesCarregadas?.Invoke(visiveis);
                MostrarSugestoes = true; // Abre o popup!
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Falha ao carregar sugestões para produto {Id}", productId);
        }
    }

    // 👇 O MÉTODO SILENCIOSO (SÓ ACENDE A LÂMPADA) 👇
    private async Task VerificarSugestoesSilenciosoAsync(CartItem item)
    {
        try
        {
            var sugestoes = await _produtoAgregadoService.GetSugestoesAsync(item.ProductId);
            if (sugestoes != null && sugestoes.Any())
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() => item.HasSuggestions = true);
            }
        }
        catch { }
    }

    private void AdicionarSugestaoAoCarrinho(ProdutoAgregadoDto? sugestao)
    {
        if (sugestao is null) return;

        var dto = new ProductDto(
            Id:         sugestao.ProdutoRelacionadoId,
            Name:       sugestao.Nome,
            Barcode:    sugestao.Barcode,
            SKU:        null,
            CategoryName: null,
            Brand:      null,
            Unit:       sugestao.Unit,
            SalePrice:  sugestao.Preco,
            Stock:      sugestao.Estoque,
            MinStock:   0,
            IsActive:   true,
            ImageUrl:   sugestao.ImageUrl);

        AddToCart(dto);
        Sugestoes.Remove(sugestao);
        if (!Sugestoes.Any()) FecharSugestoes();
    }

    private void FecharSugestoes()
    {
        MostrarSugestoes = false;
        Sugestoes.Clear();
    }

    // 👇 A VARIÁVEL QUE VAI PRA TELA MOSTRAR OS IMPOSTOS 👇
    private decimal _totalTributos;
    public decimal TotalTributos { get => _totalTributos; set { SetProperty(ref _totalTributos, value); } }

    private PaymentMethod _selectedPayment = PaymentMethod.Dinheiro;
    public PaymentMethod SelectedPayment { get => _selectedPayment; set => SetProperty(ref _selectedPayment, value); }
    public IEnumerable<PaymentMethod> PaymentMethods => Enum.GetValues<PaymentMethod>();

    public ICommand SearchProductCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand RemoveFromCartCommand { get; }
    public ICommand FinalizeSaleCommand { get; }
    public ICommand ClearCartCommand { get; }
    public ICommand AplicarMarkup5Command  { get; }
    public ICommand SuspenderVendaCommand  { get; }
    public ICommand RetormarVendaCommand   { get; }
    public ICommand SearchCustomerCommand { get; }
    public ICommand SalvarOrcamentoCommand { get; }

    private async Task VerificarCaixaAbertoAsync()
    {
        try
        {
            // Criamos um escopo zerado para não dar conflito no Entity Framework
            using (var scope = ERP.WPF.App.Services.CreateScope())
            {
                var uow = scope.ServiceProvider.GetRequiredService<ERP.Domain.Interfaces.IUnitOfWork>();
                
                // Busca direto no banco se esse usuário já tem caixa aberto hoje
                var caixaAberto = await uow.Caixas.GetCaixaAbertoByUsuarioAsync(AppSession.UserId);
                
                if (caixaAberto != null)
                {
                    // Guarda o Id do caixa na sessão para o resto do sistema saber!
                    ERP.WPF.State.AppSession.CaixaId = caixaAberto.Id;

                    // Como estamos em background, pedimos pra thread principal da UI atualizar a tela
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsCaixaAberto = true;
                        ValorAtualCaixa = caixaAberto.ValorAbertura; 
                    });

                    await AtualizarBotaoVerdeAsync(); 
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IsCaixaAberto = false;
                        ValorAtualCaixa = 0;
                        ERP.WPF.State.AppSession.CaixaId = Guid.Empty;
                    });
                }
            }
        }
        catch (Exception ex) 
        { 
            // Agora se der erro, ele avisa no log em vez de engolir!
            Log.Warning(ex, "Erro ao verificar caixa aberto na inicialização do PDV");
        }
    }

    private async Task SearchCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomerSearchTerm)) return;

        try 
        {
            var customers = await _customerService.SearchAsync(CustomerSearchTerm);
            var customer = customers.FirstOrDefault();

            if (customer != null)
            {
                _selectedCustomerId = customer.Id;
                SelectedCustomerName = customer.Name;
                CustomerSearchTerm = string.Empty; 
                
                // 👇 GUARDA O CLIENTE NO BOLSO 👇
                SalvarEstadoCarrinho();
            }
            else
            {
                MessageBox.Show("Cliente não encontrado!", "TTSoft PDV", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex) { MessageBox.Show($"Erro ao buscar cliente: {ex.Message}"); }
    }

    private async Task SearchProductAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm)) 
        {
            SearchResults.Clear();
            return;
        }

        try 
        {
            // ====================================================================
            // 👇 ESTRATÉGIA 1 NA PRÁTICA: O "BIP INTELIGENTE" COM ESCOPO NOVO 👇
            // ====================================================================
            using (var scope = ERP.WPF.App.Services.CreateScope())
            {
                var freshProductService = scope.ServiceProvider.GetRequiredService<IProductService>();

                var byBarcode = await freshProductService.GetByBarcodeAsync(SearchTerm);
                
                if (byBarcode != null)
                {
                    AddToCart(byBarcode);
                    SearchTerm = string.Empty;
                    return;
                }

                var results = await freshProductService.SearchAsync(SearchTerm);
                
                SearchResults.Clear();
                if (results != null)
                {
                    foreach (var p in results.Take(30)) 
                    {
                        SearchResults.Add(p);
                    }
                }
            }
        }
        catch (Exception) { }
    }

    private void AddToCart(ProductDto? product)
    {
        if (product == null) return;
        
        decimal quantidadeParaAdicionar = product.QuantidadeGrade > 0 ? product.QuantidadeGrade : 1;
        
        var existing = CartItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null) 
        { 
            existing.Quantity += quantidadeParaAdicionar;
            // Sprint 5: destaca o item que acabou de ser incrementado
            UltimoItemId = existing.ProductId;
        }
        else
        {
            var newItem = new CartItem 
            { 
                ProductId = product.Id, 
                ProductName = product.Name, 
                NormalUnitPrice = product.SalePrice,
                WholesaleMinQuantity = product.WholesaleMinQuantity,
                WholesalePrice = product.WholesalePrice,
                PrecoBRevendedor = product.PrecoBRevendedor,
                PrecoCAtacadista = product.PrecoCAtacadista,
                UnitPrice = _grupoPrecoCliente switch
                {
                    ERP.Domain.Enums.GrupoPreco.B when product.PrecoBRevendedor > 0 => product.PrecoBRevendedor,
                    ERP.Domain.Enums.GrupoPreco.C when product.PrecoCAtacadista > 0 => product.PrecoCAtacadista,
                    _ => product.SalePrice
                },
                Ncm = product.NCM ?? "00000000",
                Cest = product.CEST ?? string.Empty,
                Cfop = product.CFOPPadrao ?? "5102",
                Csosn = string.IsNullOrWhiteSpace(product.CSOSN) ? "102" : product.CSOSN,
                IcmsOrigem = "0",
                FatorConversao    = product.FatorConversao > 0 ? product.FatorConversao : 1m,
                LabelUnidadeVenda = product.LabelUnidadeVenda,
                UnidadeEstoque    = product.UnidadeEstoque ?? product.Unit,
                ProdutoOriginal = new ERP.Domain.Entities.Product 
                {
                    IpiPercent  = product.IpiPercent,
                    IcmsPercent = product.IcmsPercent
                }
            };

            newItem.Quantity = quantidadeParaAdicionar;

            newItem.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(CartItem.Total))
                    RefreshTotals();
            };
            
            CartItems.Add(newItem);
            UltimoItemId = newItem.ProductId;
            
            // 👇 CHAMA A BUSCA SILENCIOSA AQUI (SÓ ACENDE A LÂMPADA) 👇
            _ = VerificarSugestoesSilenciosoAsync(newItem);
        }
        
        RefreshTotals();
    }

    private void RemoveFromCart(CartItem? item) { if (item != null) { CartItems.Remove(item); RefreshTotals(); } }

    private void AplicarMarkup5()
    {
        foreach (var item in CartItems)
        {
            var novoPreco = item.NormalUnitPrice * 1.05m;
            item.NormalUnitPrice = ArredondarParaMoeda(novoPreco);

            var qtd = item.Quantity;
            item.Quantity = qtd;
        }

        RefreshTotals();

        MessageBox.Show(
            "Markup de 5% aplicado e preços arredondados para R$0,50 ou R$1,00.",
            "Markup Aplicado",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static decimal ArredondarParaMoeda(decimal valor)
    {
        return Math.Ceiling(valor / 0.50m) * 0.50m;
    }

    private void SuspenderVenda()
    {
        if (!CartItems.Any()) return;

        var suspensa = new VendaSuspensa
        {
            Id              = Guid.NewGuid(),
            HoraSuspensao   = DateTime.Now,
            ClienteNome     = SelectedCustomerName ?? "Sem cliente",
            TotalAproximado = CartItems.Sum(i => i.Total),
            Itens           = CartItems.Select(i => new CartItem
            {
                ProductId         = i.ProductId,
                ProductName       = i.ProductName,
                Quantity          = i.Quantity,
                NormalUnitPrice   = i.NormalUnitPrice,
                UnitPrice         = i.UnitPrice,
                Observacao        = i.Observacao,
                FatorConversao    = i.FatorConversao,
                UnidadeEstoque    = i.UnidadeEstoque,
                LabelUnidadeVenda = i.LabelUnidadeVenda,
                WholesalePrice       = i.WholesalePrice,
                WholesaleMinQuantity = i.WholesaleMinQuantity
            }).ToList()
        };

        VendasSuspensas.Add(suspensa);
        TemVendasSuspensas = true;
        ClearCart();

        MessageBox.Show(
            $"Venda suspensa!\nTotal: {suspensa.TotalAproximado:C}",
            "Venda em Espera", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RetormarVenda(VendaSuspensa? suspensa)
    {
        if (suspensa == null) return;

        if (CartItems.Any())
        {
            var resposta = MessageBox.Show(
                "Há itens no carrinho. Deseja suspender a venda atual antes de retomar?",
                "Atenção", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (resposta == MessageBoxResult.Cancel) return;
            if (resposta == MessageBoxResult.Yes)    SuspenderVenda();
            if (resposta == MessageBoxResult.No)     ClearCart();
        }

        foreach (var item in suspensa.Itens)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CartItem.Total)) RefreshTotals();
            };
            CartItems.Add(item);
        }

        VendasSuspensas.Remove(suspensa);
        TemVendasSuspensas = VendasSuspensas.Any();
        RefreshTotals();
    }

    private async Task CarregarProdutosCampanhaAsync()
    {
        try
        {
            var todos = await _productService.GetAllAsync();
            var campanha = todos
                .Where(p => p.IsActive && (p.EmCampanha))
                .OrderBy(p => p.Name)
                .Take(5)
                .ToList();

            ProdutosCampanha.Clear();
            foreach (var p in campanha)
                ProdutosCampanha.Add(p);
        }
        catch { /* não crítico — PDV funciona sem campanha */ }
    }

    private void ClearCart()
    {
        CartItems.Clear();
        SearchResults.Clear();
        CustomerSearchResults.Clear();
        DiscountAmount = 0;
        _selectedCustomerId = null;
        SelectedCustomerName = "Consumidor Final";
        _orcamentoCarregadoId = null;
        RefreshTotals(); 
    }

    private async Task FinalizeSaleAsync()
    {
        if (!CartItems.Any()) return;

        string enderecoCompleto = "";

        if (_selectedCustomerId.HasValue)
        {
            try
            {
                var task = Task.Run(async () => await _customerService.GetByIdAsync(_selectedCustomerId.Value));
                task.Wait(TimeSpan.FromSeconds(2)); 
                var cliente = task.IsCompletedSuccessfully ? task.Result : null;

                if (cliente != null)
                {
                    string rua = cliente.Street ?? "";
                    string numero = !string.IsNullOrWhiteSpace(cliente.Number) ? $", {cliente.Number}" : "";
                    string bairro = !string.IsNullOrWhiteSpace(cliente.Neighborhood) ? $" - {cliente.Neighborhood}" : "";

                    enderecoCompleto = $"{rua}{numero}{bairro}";
                }
            }
            catch (Exception) { }
        }

        var finalizarVm = new FinalizarVendaViewModel(
            _saleService, 
            _customerService,
            CartItems, 
            Total, 
            _selectedCustomerId, 
            SelectedCustomerName,
            enderecoCompleto, 
           async (vendaGeradaId) => 
            {
                if (vendaGeradaId != Guid.Empty)
                {
                    _idUltimaVendaDesteCaixa = vendaGeradaId;
                }

                if (_orcamentoCarregadoId.HasValue)
                {
                    try { await _orcamentoService.MarcarComoVendidoAsync(_orcamentoCarregadoId.Value); }
                    catch { } 
                }
                ClearCart();
                _ = CarregarMetaEVendasAsync();
            }); 

        var telaFinalizar = new Views.FinalizarVendaView(finalizarVm);
        telaFinalizar.ShowDialog();

        await Task.Delay(300); 
        await AtualizarBotaoVerdeAsync();
    }

    private void RefreshTotals() 
    { 
        OnPropertyChanged(nameof(Subtotal)); 
        OnPropertyChanged(nameof(Total)); 
        
        decimal impostos = 0;
        foreach(var item in CartItems)
        {
            decimal rateioDesconto = DiscountAmount > 0 && Subtotal > 0 ? (item.Total / Subtotal) * DiscountAmount : 0;
            var tribs = _motorFiscal.CalcularTributosVenda(item.ProdutoOriginal, item.Quantity, item.UnitPrice, rateioDesconto);
            impostos += tribs.ValorIcms + tribs.ValorIpi + tribs.ValorIcmsSt;
        }
        TotalTributos = impostos;

        SalvarEstadoCarrinho();
    }

    private async Task AtualizarBotaoVerdeAsync()
    {
        try
        {
            using (var scope = ERP.WPF.App.Services.CreateScope())
            {
                var uow = scope.ServiceProvider.GetRequiredService<ERP.Domain.Interfaces.IUnitOfWork>();
                var caixa = await uow.Caixas.GetCaixaAbertoByUsuarioAsync(AppSession.UserId);
                
                if (caixa != null && caixa.Movimentos != null)
                {
                    decimal totalGaveta = 0;
                    foreach (var mov in caixa.Movimentos)
                    {
                        if (mov.Tipo == ERP.Domain.Enums.TipoMovimentoCaixa.Abertura || mov.Tipo == ERP.Domain.Enums.TipoMovimentoCaixa.Suprimento)
                            totalGaveta += mov.Valor;
                        else if (mov.Tipo == ERP.Domain.Enums.TipoMovimentoCaixa.Sangria && mov.FormaPagamento == ERP.Domain.Enums.PaymentMethod.Dinheiro)
                            totalGaveta -= mov.Valor;
                        else if ((mov.Tipo == ERP.Domain.Enums.TipoMovimentoCaixa.Venda || mov.Tipo.ToString() == "RecebimentoConta") && mov.FormaPagamento == ERP.Domain.Enums.PaymentMethod.Dinheiro)
                            totalGaveta += mov.Valor;
                    }
                    
                    System.Windows.Application.Current.Dispatcher.Invoke(() => {
                        ValorAtualCaixa = totalGaveta;
                    });
                }
            }
        }
        catch { }
    }

    private async Task SalvarOrcamentoAsync()
    {
        if (!CartItems.Any()) return;

        try
        {
            var dto = new ERP.Application.DTOs.CreateOrcamentoDto
            {
                CustomerId = _selectedCustomerId,
                CustomerName = SelectedCustomerName,
                SellerName = AppSession.UserName,
                UsuarioId = AppSession.UserId, 
                ValorTotal = Total,
                Itens = CartItems.Select(c => new ERP.Application.DTOs.OrcamentoItemDto
                {
                    ProductId = c.ProductId,
                    ProductName = c.ProductName,
                    Quantity = c.Quantity,
                    UnitPrice = c.UnitPrice,
                    DiscountPercent = c.DiscountPercent
                }).ToList()
            };

            var orcamentoSalvo = await _orcamentoService.SalvarOrcamentoAsync(dto);
            
            MessageBox.Show($"✅ Orçamento {orcamentoSalvo.Numero} salvo com sucesso!\n\nVálido até: {orcamentoSalvo.DataValidade:dd/MM/yyyy}", 
                "TTSoft - Orçamentos", MessageBoxButton.OK, MessageBoxImage.Information);
            
            ClearCart(); 
        }
        catch (Exception ex) 
        { 
            MessageBox.Show($"Erro ao salvar orçamento: {ex.Message}"); 
        }
    }

    private void VerificarOrcamentoPendente()
    {
        if (OrcamentoPendente != null)
        {
            ClearCart(); 
            _orcamentoCarregadoId = OrcamentoPendente.Id;
            
            if (OrcamentoPendente.CustomerId.HasValue)
            {
                _selectedCustomerId = OrcamentoPendente.CustomerId;
                SelectedCustomerName = OrcamentoPendente.CustomerName ?? "Consumidor Final";
            }

            if (OrcamentoPendente.Itens != null)
            {
                foreach (var item in OrcamentoPendente.Itens)
                {
                    CartItems.Add(new CartItem 
                    { 
                        ProductId = item.ProductId, 
                        ProductName = item.ProductName, 
                        NormalUnitPrice = item.UnitPrice, 
                        Quantity = item.Quantity, 
                        UnitPrice = item.UnitPrice, 
                        DiscountPercent = item.DiscountPercent 
                    });
                }
            }
            
            RefreshTotals();
            OrcamentoPendente = null; 
        }
    }

    private async Task SearchCustomerListAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomerSearchTerm)) 
        {
            CustomerSearchResults.Clear();
            return;
        }

        try 
        {
            var results = await _customerService.SearchAsync(CustomerSearchTerm);
            CustomerSearchResults.Clear();
            if (results != null)
            {
                foreach (var c in results.Take(10))
                {
                    CustomerSearchResults.Add(c);
                }
            }
        }
        catch { }
    }

    private void SelectCustomer(CustomerDto? customer)
    {
        if (customer != null)
        {
            _selectedCustomerId  = customer.Id;
            SelectedCustomerName = customer.Name;
            _customerSearchTerm  = string.Empty;
            OnPropertyChanged(nameof(CustomerSearchTerm));
            CustomerSearchResults.Clear();
            OnPropertyChanged(nameof(TemClienteSelecionado));
            SalvarEstadoCarrinho();
            RegistrarClienteFrequente(customer);

            // Sprint C: aplicar grupo de preço do cliente
            GrupoPrecoCliente = (ERP.Domain.Enums.GrupoPreco)customer.GrupoPreco;

            // Sprint D: alerta de limite de crédito
            if (customer.LimiteCredito > 0 && customer.SaldoDevedor >= customer.LimiteCredito)
            {
                System.Windows.MessageBox.Show(
                    $"⚠ Atenção: {customer.Name} está com o limite de crédito ESGOTADO!\n" +
                    $"Limite: {customer.LimiteCredito:C} | Devedor: {customer.SaldoDevedor:C}",
                    "Limite de Crédito", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            else if (customer.LimiteCredito > 0 &&
                     customer.SaldoDevedor / customer.LimiteCredito >= 0.8m)
            {
                System.Windows.MessageBox.Show(
                    $"⚠ {customer.Name} está com {customer.SaldoDevedor / customer.LimiteCredito * 100:F0}% do limite utilizado.\n" +
                    $"Disponível: {customer.LimiteCredito - customer.SaldoDevedor:C}",
                    "Limite próximo", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
    }

    private void LimparCliente()
    {
        GrupoPrecoCliente = ERP.Domain.Enums.GrupoPreco.A;
        _selectedCustomerId = null;
        SelectedCustomerName = "Consumidor Final";
        OnPropertyChanged(nameof(TemClienteSelecionado));
        SalvarEstadoCarrinho();
    }

    private void IncreaseQty(CartItem? item)
    {
        if (item != null)
        {
            item.Quantity++;
            RefreshTotals();
        }
    }

    private void DecreaseQty(CartItem? item)
    {
        if (item != null && item.Quantity > 1)
        {
            item.Quantity--;
            RefreshTotals();
        }
    }

    private async Task IniciarRadarSefazAsync()
    {
        while (true)
        {
            try
            {
                using (var scope = ERP.WPF.App.Services.CreateScope())
                {
                    var contingencyService = scope.ServiceProvider.GetRequiredService<INfeContingencyService>();
                    
                    bool online = await contingencyService.VerificarConexaoSefazAsync();

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        ModoContingenciaVisivel = online ? Visibility.Collapsed : Visibility.Visible;
                    });
                }
            }
            catch { }
            
            await Task.Delay(30000); 
        }
    }

    private void AbrirResumoCaixa(object? obj)
    {
        var telaResumo = new ERP.WPF.Views.ResumoCaixaView(); 
        var caixaVm = ERP.WPF.App.Services.GetRequiredService<ResumoCaixaViewModel>();

        telaResumo.DataContext = caixaVm;
        telaResumo.Owner = System.Windows.Application.Current.MainWindow;
        telaResumo.ShowDialog();
    }

    private async Task ReimprimirUltimoReciboAsync()
    {
        try
        {
            if (_idUltimaVendaDesteCaixa == null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    MessageBox.Show("Você ainda não realizou nenhuma venda neste computador hoje para ser reimpressa.", 
                    "Atenção", MessageBoxButton.OK, MessageBoxImage.Information));
                return;
            }

            using (var scope = ERP.WPF.App.Services.CreateScope())
            {
                var saleService = scope.ServiceProvider.GetRequiredService<ISaleService>();
                var vendaDetalhe = await saleService.GetDetailAsync(_idUltimaVendaDesteCaixa.Value);

                if (vendaDetalhe == null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        MessageBox.Show("A venda selecionada não foi encontrada no banco de dados.", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning));
                    return;
                }

                string nomeCliente = vendaDetalhe.CustomerName ?? "CONSUMIDOR FINAL";

                var ultimaVenda = new ERP.Domain.Entities.Sale
                {
                    Id         = vendaDetalhe.Id,
                    SaleNumber = vendaDetalhe.SaleNumber,
                    SaleDate   = vendaDetalhe.SaleDate,
                    Total      = vendaDetalhe.Total,
                    CustomerId = vendaDetalhe.CustomerId,
                    Items      = vendaDetalhe.Items.Select(i => new ERP.Domain.Entities.SaleItem
                    {
                        ProductId   = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity    = i.Quantity,
                        UnitPrice   = i.UnitPrice,
                    }).ToList(),
                    Payments = vendaDetalhe.Payments.Select(p => new ERP.Domain.Entities.SalePayment
                    {
                        PaymentMethod = Enum.Parse<ERP.Domain.Enums.PaymentMethod>(p.PaymentMethod),
                        Amount        = p.Amount,
                    }).ToList(),
                };

                if (ultimaVenda == null) return;

                var itensCarrinho = ultimaVenda.Items.Select(i => new CartItem 
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    NormalUnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    DiscountPercent = i.DiscountPercent,
                    Observacao = "" 
                }).ToList();

                var pagamentos = new System.Collections.Generic.List<(string, decimal)>();

                if (ultimaVenda.Payments != null && ultimaVenda.Payments.Any())
                {
                    foreach (var p in ultimaVenda.Payments)
                    {
                        string formaPagamento = p.PaymentMethod.ToString();

                        if (formaPagamento == "0") formaPagamento = "DINHEIRO";
                        else if (formaPagamento == "1") formaPagamento = "CARTÃO CRÉDITO";
                        else if (formaPagamento == "2") formaPagamento = "CARTÃO DÉBITO";
                        else if (formaPagamento == "3") formaPagamento = "PIX";

                        pagamentos.Add((formaPagamento, p.Amount)); 
                    }
                }
                else
                {
                    pagamentos.Add(("VALOR RECEBIDO", ultimaVenda.Total));
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    ERP.WPF.Helpers.ReciboPrinter.Imprimir(
                        ultimaVenda.Id, 
                        itensCarrinho, 
                        ultimaVenda.Total, 
                        ultimaVenda.DiscountAmount, 
                        nomeCliente, 
                        ERP.WPF.State.AppSession.UserName ?? "VENDEDOR", 
                        pagamentos, 
                        0, 
                        ultimaVenda.Notes ?? "", 
                        "REIMPRESSÃO DE VENDA", 
                        ultimaVenda.SaleDate 
                    );
                });
            }
        }
        catch (Exception ex)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
                MessageBox.Show($"Erro ao tentar reimprimir: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }

    private void AbrirConsultaRapida()
    {
        var consultaVm = new ConsultaPrecoViewModel(_productService);

        consultaVm.OnAdicionarAoCarrinho = (produtoEncontrado, qtdDigitada, isAtacado) => 
        {
            produtoEncontrado.QuantidadeGrade = qtdDigitada;
            AddToCart(produtoEncontrado);
        };

        var window = new Views.ConsultaPrecoView(consultaVm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        window.ShowDialog();
    }

    private async Task CarregarMetaEVendasAsync()
    {
        try
        {
            using var scope = _sp.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ERP.Persistence.Context.AppDbContext>();

            var hoje    = DateTime.Today;
            var mes     = hoje.Month;
            var ano     = hoje.Year;
            var operador = ERP.Domain.CurrentUser.Name ?? "";

            var meta = await ctx.MetasVendas.AsNoTracking()
                .Where(m => m.Mes == mes && m.Ano == ano &&
                           (m.VendedorNome == "Geral" || m.VendedorNome == operador))
                .OrderByDescending(m => m.VendedorNome == operador) 
                .FirstOrDefaultAsync();

            var vendidoHoje = await ctx.Sales.AsNoTracking()
                .Where(s => s.SaleDate.Date == hoje &&
                            s.Status != ERP.Domain.Enums.SaleStatus.Cancelada &&
                            (string.IsNullOrEmpty(operador) || s.SellerName == operador))
                .SumAsync(s => (decimal?)s.Total) ?? 0;

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                MetaDia      = meta?.ValorMeta ?? 0;
                VendidoHoje  = vendidoHoje;
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Falha ao carregar meta do dia no PDV");
        }
    }

    public void IncrementarVendidoHoje(decimal valorVenda)
    {
        VendidoHoje += valorVenda;
    }

    private IServiceProvider _sp => ERP.WPF.App.Services;
}