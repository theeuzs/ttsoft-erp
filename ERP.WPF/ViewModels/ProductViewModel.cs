using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using ERP.WPF.Commands;
using ERP.WPF.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ERP.WPF.Views;

namespace ERP.WPF.ViewModels;

public class ProductViewModel : BaseViewModel
{
    private readonly IProductService _productService;
    private readonly IMotorFiscalService _motorFiscal;
    private readonly AjusteEstoqueViewModel _ajusteVm;

    // ── Paginação ─────────────────────────────────────────────────────────
    private const int PageSize = 50;
    private int _currentPage = 1;
    private int _totalPages  = 1;

    public int    CurrentPage  { get => _currentPage; private set { SetProperty(ref _currentPage, value); AtualizarNavegacao(); } }
    public int    TotalPages   { get => _totalPages;  private set { SetProperty(ref _totalPages, value);  AtualizarNavegacao(); } }
    public string PaginaInfo   => $"Página {CurrentPage} de {TotalPages}";
    public bool   PodeAnterior => CurrentPage > 1;
    public bool   PodeProxima  => CurrentPage < TotalPages;

    public ICommand ProximaPaginaCommand  { get; }
    public ICommand AnteriorPaginaCommand { get; }

    private void AtualizarNavegacao()
    {
        OnPropertyChanged(nameof(PaginaInfo));
        OnPropertyChanged(nameof(PodeAnterior));
        OnPropertyChanged(nameof(PodeProxima));
        CommandManager.InvalidateRequerySuggested();
    }

    // ── Proteção de estoque ───────────────────────────────────────────────
    private decimal _stockOriginal;
    private bool    _stockEditado;

    public ProductViewModel(IProductService productService, AjusteEstoqueViewModel ajusteVm, IMotorFiscalService motorFiscal)
    {
        _productService = productService;
        _ajusteVm       = ajusteVm;
        _motorFiscal    = motorFiscal;

        LoadCommand   = new AsyncRelayCommand(_ => LoadProductsAsync());
        SaveCommand   = new AsyncRelayCommand(_ => SaveAsync(), _ => !IsBusy);
        SelecionarImagemCommand = new RelayCommand(_ =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Imagens|*.jpg;*.jpeg;*.png;*.webp;*.bmp|Todos|*.*",
                Title  = "Selecionar foto do produto"
            };
            if (dlg.ShowDialog() == true)
                ImageUrl = dlg.FileName;
        });
        DeleteCommand = new AsyncRelayCommand(_ => DeleteAsync(), _ => SelectedProduct != null);
        NewCommand    = new RelayCommand(_ => ResetForm());
        SearchCommand = new AsyncRelayCommand(_ => SearchAsync());
        ImportCommand = new AsyncRelayCommand(_ => ImportarPlanilhaAsync(), _ => !IsBusy);
        ExportarPlanilhaCommand = new AsyncRelayCommand(_ => ExportarPlanilhaAsync(), _ => !IsBusy);

        ProximaPaginaCommand  = new RelayCommand(async _ => { CurrentPage++; await LoadProductsAsync(); }, _ => PodeProxima);
        AnteriorPaginaCommand = new RelayCommand(async _ => { CurrentPage--; await LoadProductsAsync(); }, _ => PodeAnterior);

        AbrirAjusteEstoqueCommand = new RelayCommand(_ => ExecutarAbrirAjusteEstoque());
        NovaCategoriaCommand      = new RelayCommand(_ => CadastrarNovaCategoriaAsync().SafeFireAndForgetAsync("cadastrar categoria"));
        NovaMarcaCommand          = new RelayCommand(_ => CadastrarNovaMarcaAsync().SafeFireAndForgetAsync("cadastrar marca"));
        NovoFornecedorCommand     = new RelayCommand(_ => CadastrarNovoFornecedorAsync().SafeFireAndForgetAsync("cadastrar fornecedor"));

        CarregarListasDoBancoAsync().SafeFireAndForgetSilentAsync("carregarListasBanco");
    }

    // ── Collections ───────────────────────────────────────────────────────
    public ObservableCollection<ProductDto> Products          { get; } = new();
    public ObservableCollection<Category>   ListaCategorias   { get; } = new();
    public ObservableCollection<Brand>      ListaMarcas       { get; } = new();
    public ObservableCollection<Supplier>   ListaFornecedores { get; } = new();

    private Category? _categoriaSelecionada;
    public Category? CategoriaSelecionada { get => _categoriaSelecionada; set => SetProperty(ref _categoriaSelecionada, value); }

    private Brand? _marcaSelecionada;
    public Brand? MarcaSelecionada { get => _marcaSelecionada; set => SetProperty(ref _marcaSelecionada, value); }

    private Supplier? _fornecedorSelecionado;
    public Supplier? FornecedorSelecionado { get => _fornecedorSelecionado; set => SetProperty(ref _fornecedorSelecionado, value); }

    // ── Seleção / Form ────────────────────────────────────────────────────
    private ProductDto? _selectedProduct;
    public ProductDto? SelectedProduct
    {
        get => _selectedProduct;
        set { SetProperty(ref _selectedProduct, value); if (value != null) LoadFormFromDto(value); }
    }

    private bool _isEditing;
    public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

    private Guid   _formId;
    private string _name = string.Empty;
    public  string Name  { get => _name; set { SetProperty(ref _name, value); RecalcTaxes(); } }

    private string? _barcode; public string? Barcode { get => _barcode; set => SetProperty(ref _barcode, value); }
    private string? _sku;     public string? SKU     { get => _sku;     set => SetProperty(ref _sku, value); }
    private string  _unit = "UN"; public string Unit { get => _unit;   set => SetProperty(ref _unit, value); }

    private decimal _originalCost;
    public decimal OriginalCost { get => _originalCost; set { SetProperty(ref _originalCost, value); RecalcTaxes(); } }

    private decimal _ipiPercent;
    public decimal IpiPercent { get => _ipiPercent; set { SetProperty(ref _ipiPercent, value); RecalcTaxes(); } }

    private decimal _icmsPercent = 8.03m;
    public decimal IcmsPercent { get => _icmsPercent; set { SetProperty(ref _icmsPercent, value); RecalcTaxes(); } }

    private decimal _finalCost;
    public decimal FinalCost { get => _finalCost; private set => SetProperty(ref _finalCost, value); }

    private decimal _desiredMarginPercent;
    public decimal DesiredMarginPercent { get => _desiredMarginPercent; set { SetProperty(ref _desiredMarginPercent, value); RecalcMargin(); } }

    private decimal _salePrice;
    public decimal SalePrice { get => _salePrice; set { SetProperty(ref _salePrice, value); RecalcMargin(); } }

    // ── Produto Composto ──────────────────────────────────────────────────
    private Guid?    _parentProductId;
    private decimal  _conversionFactor = 1m;
    private ProductDto? _produtoPaiSelecionado;

    public Guid?    ParentProductId    { get => _parentProductId;    set => SetProperty(ref _parentProductId, value); }
    public decimal  ConversionFactor   { get => _conversionFactor;   set => SetProperty(ref _conversionFactor, value); }

    public ProductDto? ProdutoPaiSelecionado
    {
        get => _produtoPaiSelecionado;
        set
        {
            SetProperty(ref _produtoPaiSelecionado, value);
            ParentProductId = value?.Id;
        }
    }

    public ObservableCollection<ProductDto> ListaProdutosPai { get; } = new();

    // ── Conversão de unidade ──────────────────────────────────────────────
    private string?  _unidadeEstoque;
    private string?  _unidadeVenda;
    private decimal  _fatorConversao = 1m;
    private string?  _labelUnidadeVenda;
    public string?  UnidadeEstoque    { get => _unidadeEstoque;    set => SetProperty(ref _unidadeEstoque,    value); }
    public string?  UnidadeVenda      { get => _unidadeVenda;      set => SetProperty(ref _unidadeVenda,      value); }
    public decimal  FatorConversao    { get => _fatorConversao;    set => SetProperty(ref _fatorConversao,    value); }
    public string?  LabelUnidadeVenda { get => _labelUnidadeVenda; set => SetProperty(ref _labelUnidadeVenda, value); }

    // Propriedade de texto para o campo Fator — aceita vírgula e ponto como separador decimal
    public string FatorConversaoTexto
    {
        get => _fatorConversao.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value)) { FatorConversao = 1m; return; }
            // Aceita tanto vírgula (0,8333) quanto ponto (0.8333)
            string normalizado = value.Trim().Replace(",", ".");
            if (decimal.TryParse(normalizado, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal resultado))
            {
                FatorConversao = resultado;
                OnPropertyChanged(nameof(FatorConversaoTexto));
            }
        }
    }

    // ── Rastreamento de alteração de preço ────────────────────────────
    private DateTime? _salePriceChangedAt;
    private string?   _salePriceChangedBy;
    public DateTime? SalePriceChangedAt { get => _salePriceChangedAt; set => SetProperty(ref _salePriceChangedAt, value); }
    public string?   SalePriceChangedBy { get => _salePriceChangedBy; set => SetProperty(ref _salePriceChangedBy, value); }
    private DateTime? _costPriceChangedAt;
    private string?   _costPriceChangedBy;
    public DateTime? CostPriceChangedAt { get => _costPriceChangedAt; set => SetProperty(ref _costPriceChangedAt, value); }
    public string?   CostPriceChangedBy { get => _costPriceChangedBy; set => SetProperty(ref _costPriceChangedBy, value); }

    private decimal _realMargin; public decimal RealMargin { get => _realMargin; private set => SetProperty(ref _realMargin, value); }
    private decimal _unitProfit; public decimal UnitProfit { get => _unitProfit; private set => SetProperty(ref _unitProfit, value); }
    private decimal _markup;     public decimal Markup     { get => _markup;     private set => SetProperty(ref _markup, value); }

    private decimal _stock;
    public decimal Stock
    {
        get => _stock;
        set
        {
            if (_isEditing && value != _stockOriginal)
                _stockEditado = true;
            SetProperty(ref _stock, value);
        }
    }

    private decimal _minStock;   public decimal MinStock   { get => _minStock;   set => SetProperty(ref _minStock, value); }
    private decimal _idealStock; public decimal IdealStock { get => _idealStock; set => SetProperty(ref _idealStock, value); }

    private string? _warehouseLocation;
    public string? WarehouseLocation { get => _warehouseLocation; set => SetProperty(ref _warehouseLocation, value); }

    private bool _isActive = true;      public bool IsActive           { get => _isActive;           set => SetProperty(ref _isActive, value); }
    private bool _allowDiscount = true; public bool AllowDiscount      { get => _allowDiscount;      set => SetProperty(ref _allowDiscount, value); }

    // ── Mídia e Campanha ─────────────────────────────────────────────────────
    private string? _imageUrl;
    public string? ImageUrl
    {
        get => _imageUrl;
        set { SetProperty(ref _imageUrl, value); OnPropertyChanged(nameof(TemImagem)); OnPropertyChanged(nameof(SemImagem)); }
    }
    private string? _descricaoDetalhada;
    public string? DescricaoDetalhada { get => _descricaoDetalhada; set => SetProperty(ref _descricaoDetalhada, value); }
    private bool _emCampanha;
    public bool EmCampanha { get => _emCampanha; set => SetProperty(ref _emCampanha, value); }
    public bool TemImagem => !string.IsNullOrWhiteSpace(_imageUrl);
    public bool SemImagem => string.IsNullOrWhiteSpace(_imageUrl);
    private bool _allowNegativeStock;   public bool AllowNegativeStock { get => _allowNegativeStock; set => SetProperty(ref _allowNegativeStock, value); }

    private decimal? _wholesaleMinQuantity; public decimal? WholesaleMinQuantity { get => _wholesaleMinQuantity; set => SetProperty(ref _wholesaleMinQuantity, value); }
    private decimal? _wholesalePrice;       public decimal? WholesalePrice       { get => _wholesalePrice;       set => SetProperty(ref _wholesalePrice, value); }

    // Sprint C: Preços por grupo de cliente
    private decimal _precoBRevendedor;
    public decimal PrecoBRevendedor
    {
        get => _precoBRevendedor;
        set { SetProperty(ref _precoBRevendedor, value); RecalcMargin(); }
    }

    private decimal _precoCAtacadista;
    public decimal PrecoCAtacadista
    {
        get => _precoCAtacadista;
        set { SetProperty(ref _precoCAtacadista, value); RecalcMargin(); }
    }

    private string? _ncm;   public string? NCM        { get => _ncm;   set => SetProperty(ref _ncm, value); }
    private string? _cest;  public string? CEST       { get => _cest;  set => SetProperty(ref _cest, value); }
    private string? _cfop;  public string? CFOPPadrao { get => _cfop;  set => SetProperty(ref _cfop, value); }
    private string? _csosn; public string? CSOSN      { get => _csosn; set => SetProperty(ref _csosn, value); }

    private string _searchTerm = string.Empty;
    public string SearchTerm { get => _searchTerm; set => SetProperty(ref _searchTerm, value); }

    // ── Permissões por código (dinâmico, não hardcoded por nome de cargo) ─
    public bool PodeEditar   => ERP.WPF.State.PermissionChecker.Has(ERP.WPF.State.PermissionChecker.ProductEdit);
    public bool PodeVerCusto => ERP.WPF.State.PermissionChecker.Has(ERP.WPF.State.PermissionChecker.ProductEditPrice);

    public System.Windows.Visibility VisEdicao => PodeEditar   ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    public System.Windows.Visibility VisCusto  => PodeVerCusto ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    // ── Commands ──────────────────────────────────────────────────────────
    public ICommand LoadCommand               { get; }
    public ICommand SaveCommand               { get; }
    public ICommand SelecionarImagemCommand   { get; }
    public ICommand DeleteCommand             { get; }
    public ICommand NewCommand                { get; }
    public ICommand SearchCommand             { get; }
    public ICommand ImportCommand             { get; }
    public ICommand ExportarPlanilhaCommand   { get; }
    public ICommand NovaCategoriaCommand      { get; }
    public ICommand NovaMarcaCommand          { get; }
    public ICommand NovoFornecedorCommand     { get; }
    public ICommand AbrirAjusteEstoqueCommand { get; }

    // ── Carregamento ──────────────────────────────────────────────────────
    private async Task CarregarListasDoBancoAsync()
    {
        using var scope = App.Services.CreateScope();
        var catSvc = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var brdSvc = scope.ServiceProvider.GetRequiredService<IBrandService>();
        var supSvc = scope.ServiceProvider.GetRequiredService<ISupplierService>();

        var categorias   = (await catSvc.GetAllAsync()).Select(d => new ERP.Domain.Entities.Category { Id = d.Id, Name = d.Name }).ToList();
        var marcas       = (await brdSvc.GetAllAsync()).Select(d => new ERP.Domain.Entities.Brand    { Id = d.Id, Name = d.Name }).ToList();
        var fornecedores = (await supSvc.GetAllAsync()).Select(d => new ERP.Domain.Entities.Supplier { Id = d.Id, Name = d.Name }).ToList();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ListaCategorias.Clear();   foreach (var c in categorias)   ListaCategorias.Add(c);
            ListaMarcas.Clear();       foreach (var m in marcas)       ListaMarcas.Add(m);
            ListaFornecedores.Clear(); foreach (var f in fornecedores) ListaFornecedores.Add(f);
        });

        await LoadProductsAsync();
        // Carrega lista de produtos para seleção como pai
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ListaProdutosPai.Clear();
            // CORREÇÃO: Ordem certa -> SalePrice (0m), Stock (0m), MinStock (0m), IsActive (true)
            ListaProdutosPai.Add(new ProductDto(Guid.Empty, "(Nenhum — produto simples)", null, null, null, null, "UN", 0m, 0m, 0m, true));
            foreach (var p in Products) ListaProdutosPai.Add(p);
        });
    }

    private async Task LoadProductsAsync()
    {
        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IProductService>();

            var resultado = await service.GetPagedAsync(
                page:     CurrentPage,
                pageSize: PageSize,
                search:   string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm);

            TotalPages = Math.Max(1, resultado.TotalPages);
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            Products.Clear();
            foreach (var p in resultado.Items) Products.Add(p);

            StatusMessage = $"{resultado.TotalItems} produtos cadastrados";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message); }
        finally { IsBusy = false; }
    }

    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await LoadProductsAsync();
        // Carrega lista de produtos para seleção como pai
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ListaProdutosPai.Clear();
            // CORREÇÃO: Ordem certa -> SalePrice (0m), Stock (0m), MinStock (0m), IsActive (true)
            ListaProdutosPai.Add(new ProductDto(Guid.Empty, "(Nenhum — produto simples)", null, null, null, null, "UN", 0m, 0m, 0m, true));
            foreach (var p in Products) ListaProdutosPai.Add(p);
        });
    }

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IProductService>();

            if (IsEditing)
            {
                decimal estoqueParaSalvar = Stock;
                if (!_stockEditado)
                {
                    var atual = await service.GetByIdAsync(_formId);
                    if (atual != null) estoqueParaSalvar = atual.Stock;
                }

                var dto = new UpdateProductDto
                {
                    Id = _formId, Name = Name, Barcode = Barcode, SKU = SKU,
                    Unit = Unit, CategoryId = CategoriaSelecionada?.Id,
                    BrandId = MarcaSelecionada?.Id, SupplierId = FornecedorSelecionado?.Id,
                    OriginalCost = OriginalCost, IpiPercent = IpiPercent, IcmsPercent = IcmsPercent,
                    DesiredMarginPercent = DesiredMarginPercent, SalePrice = SalePrice,
                    Stock = estoqueParaSalvar,
                    MinStock = MinStock, IdealStock = IdealStock,
                    AllowNegativeStock = AllowNegativeStock, WarehouseLocation = WarehouseLocation,
                    IsActive = IsActive, AllowDiscount = AllowDiscount,
                    WholesaleMinQuantity = WholesaleMinQuantity, WholesalePrice = WholesalePrice,
                    PrecoBRevendedor     = PrecoBRevendedor,
                    PrecoCAtacadista     = PrecoCAtacadista,
                    NCM = NCM, CEST = CEST, CFOPPadrao = CFOPPadrao, CSOSN = CSOSN,
                    UnidadeEstoque    = UnidadeEstoque,
                    UnidadeVenda      = UnidadeVenda,
                    FatorConversao    = FatorConversao,
                    LabelUnidadeVenda = LabelUnidadeVenda,
                    ParentProductId   = ParentProductId == Guid.Empty ? null : ParentProductId,
                    ConversionFactor  = ConversionFactor > 0 ? ConversionFactor : 1m,
                };
                await service.UpdateAsync(dto);
                StatusMessage = "Produto atualizado com sucesso!";
            }
            else
            {
                var dto = new CreateProductDto
                {
                    Name = Name, Barcode = Barcode, SKU = SKU, Unit = Unit,
                    CategoryId = CategoriaSelecionada?.Id, BrandId = MarcaSelecionada?.Id,
                    SupplierId = FornecedorSelecionado?.Id,
                    OriginalCost = OriginalCost, IpiPercent = IpiPercent,
                    IcmsPercent = IcmsPercent, DesiredMarginPercent = DesiredMarginPercent,
                    SalePrice = SalePrice, Stock = Stock, MinStock = MinStock,
                    IdealStock = IdealStock, AllowNegativeStock = AllowNegativeStock,
                    WarehouseLocation = WarehouseLocation, IsActive = IsActive,
                    AllowDiscount = AllowDiscount,
                    WholesaleMinQuantity = WholesaleMinQuantity, WholesalePrice = WholesalePrice,
                    PrecoBRevendedor     = PrecoBRevendedor,
                    PrecoCAtacadista     = PrecoCAtacadista,
                    NCM = NCM, CEST = CEST, CFOPPadrao = CFOPPadrao, CSOSN = CSOSN,
                    UnidadeEstoque    = UnidadeEstoque,
                    UnidadeVenda      = UnidadeVenda,
                    FatorConversao    = FatorConversao,
                    LabelUnidadeVenda = LabelUnidadeVenda,
                    ParentProductId   = ParentProductId == Guid.Empty ? null : ParentProductId,
                    ConversionFactor  = ConversionFactor > 0 ? ConversionFactor : 1m,
                };
                await service.CreateAsync(dto);
                StatusMessage = "Produto criado com sucesso!";
            }

            await LoadProductsAsync();
            ResetForm();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
        {
            string erroBanco = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
            
            MessageBox.Show($"O banco de dados recusou o salvamento.\n\nMotivo real:\n{erroBanco}", 
                            "Erro ao Salvar no Banco", MessageBoxButton.OK, MessageBoxImage.Error);
                            
            StatusMessage = "Erro ao salvar no banco. Veja o alerta na tela.";
        }
        catch (FluentValidation.ValidationException ex)
        {
            StatusMessage = string.Join(" | ", ex.Errors.Select(e => e.ErrorMessage));
        }
        catch (Exception ex) 
        { 
            StatusMessage = $"Erro: {ex.Message}"; 
        }
        finally 
        { 
            IsBusy = false; 
        }
    }

    private async Task DeleteAsync()
    {
        if (SelectedProduct == null) return;
        if (MessageBox.Show($"Excluir '{SelectedProduct.Name}'?", "Confirmar",
            MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IProductService>();
        await service.DeleteAsync(SelectedProduct.Id);
        await LoadProductsAsync();
        ResetForm();
    }

    private void LoadFormFromDto(ProductDto dto)
    {
        _formId        = dto.Id;
        IsEditing      = true;
        _stockEditado  = false;        
        _stockOriginal = dto.Stock;    

        Name = dto.Name; Barcode = dto.Barcode; SKU = dto.SKU; Unit = dto.Unit;
        CategoriaSelecionada  = ListaCategorias.FirstOrDefault(c => c.Id == dto.CategoryId);
        MarcaSelecionada      = ListaMarcas.FirstOrDefault(m => m.Id == dto.BrandId);
        FornecedorSelecionado = ListaFornecedores.FirstOrDefault(f => f.Id == dto.SupplierId);
        OriginalCost = dto.OriginalCost; IpiPercent = dto.IpiPercent; IcmsPercent = dto.IcmsPercent;
        DesiredMarginPercent = dto.DesiredMarginPercent; SalePrice = dto.SalePrice;

        _stock = dto.Stock; OnPropertyChanged(nameof(Stock));

        MinStock = dto.MinStock; IdealStock = dto.IdealStock;
        WarehouseLocation = dto.WarehouseLocation; IsActive = dto.IsActive;
        AllowDiscount = dto.AllowDiscount; AllowNegativeStock = dto.AllowNegativeStock;
        WholesaleMinQuantity = dto.WholesaleMinQuantity; WholesalePrice = dto.WholesalePrice;
        PrecoBRevendedor = dto.PrecoBRevendedor;
        PrecoCAtacadista = dto.PrecoCAtacadista;
        NCM = dto.NCM; CEST = dto.CEST; CFOPPadrao = dto.CFOPPadrao; CSOSN = dto.CSOSN;
        SalePriceChangedAt = dto.SalePriceChangedAt; SalePriceChangedBy = dto.SalePriceChangedBy;
        CostPriceChangedAt = dto.CostPriceChangedAt; CostPriceChangedBy = dto.CostPriceChangedBy;
        UnidadeEstoque = dto.UnidadeEstoque; UnidadeVenda = dto.UnidadeVenda;
        FatorConversao = dto.FatorConversao > 0 ? dto.FatorConversao : 1m;
        OnPropertyChanged(nameof(FatorConversaoTexto));
        LabelUnidadeVenda = dto.LabelUnidadeVenda;
        ParentProductId = dto.ParentProductId;
        ConversionFactor = dto.ConversionFactor > 0 ? dto.ConversionFactor : 1m;
        ProdutoPaiSelecionado = dto.ParentProductId.HasValue
            ? ListaProdutosPai.FirstOrDefault(p => p.Id == dto.ParentProductId)
            : null;
        RecalcTaxes();
    }

    private void ResetForm()
    {
        _formId        = Guid.Empty;
        _stockEditado  = false;
        _stockOriginal = 0;
        IsEditing      = false;

        Name = string.Empty; Barcode = null; SKU = null; Unit = "UN"; OriginalCost = 0;
        IpiPercent = 0; IcmsPercent = 8.03m; SalePrice = 0;
        _stock = 0; OnPropertyChanged(nameof(Stock));
        MinStock = 0; IdealStock = 0;
        IsActive = true; AllowDiscount = true; AllowNegativeStock = false;
        WholesaleMinQuantity = null; WholesalePrice = null;
        NCM = null; CEST = null; CFOPPadrao = null; CSOSN = null;
        SalePriceChangedAt = null; SalePriceChangedBy = null;
        CostPriceChangedAt = null; CostPriceChangedBy = null;
        UnidadeEstoque = null; UnidadeVenda = null; FatorConversao = 1m; LabelUnidadeVenda = null;
        ParentProductId = null; ConversionFactor = 1m; ProdutoPaiSelecionado = null;
        SelectedProduct = null;
        CategoriaSelecionada = null; MarcaSelecionada = null; FornecedorSelecionado = null;
    }

    private void RecalcTaxes()
    {
        var prod = new ERP.Domain.Entities.Product { IpiPercent = IpiPercent, IcmsPercent = 0 };
        FinalCost = _motorFiscal.CalcularTributosVenda(prod, 1, OriginalCost).ValorTotalItem;
        RecalcMargin();
    }

    private void RecalcMargin()
    {
        decimal imposto = SalePrice > 0 ? SalePrice * (IcmsPercent / 100m) : 0;
        UnitProfit = SalePrice - FinalCost - imposto;
        RealMargin = SalePrice > 0 ? (UnitProfit / SalePrice) * 100m : 0;
        Markup     = FinalCost > 0 ? SalePrice / FinalCost : 0;
    }

    private async Task CadastrarNovaCategoriaAsync()
    {
        var popup = new QuickAddView("Nova Categoria");
        popup.ShowDialog();
        if (!popup.IsSaved) return;
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICategoryService>();
        var dto = await service.CreateAsync(popup.ItemName);
        var entity = new ERP.Domain.Entities.Category { Id = dto.Id, Name = dto.Name };
        ListaCategorias.Add(entity); CategoriaSelecionada = entity;
    }

    private async Task CadastrarNovaMarcaAsync()
    {
        var popup = new QuickAddView("Nova Marca");
        popup.ShowDialog();
        if (!popup.IsSaved) return;
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBrandService>();
        var dto = await service.CreateAsync(popup.ItemName);
        var entity = new ERP.Domain.Entities.Brand { Id = dto.Id, Name = dto.Name };
        ListaMarcas.Add(entity); MarcaSelecionada = entity;
    }

    private async Task CadastrarNovoFornecedorAsync()
    {
        var popup = new QuickAddView("Novo Fornecedor");
        popup.ShowDialog();
        if (!popup.IsSaved) return;
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISupplierService>();
        var dto = await service.CreateAsync(popup.ItemName);
        var entity = new ERP.Domain.Entities.Supplier { Id = dto.Id, Name = dto.Name };
        ListaFornecedores.Add(entity); FornecedorSelecionado = entity;
    }

    private async Task ImportarPlanilhaAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Arquivos CSV (*.csv)|*.csv",
            Title  = "Selecione a sua planilha de produtos CSV"
        };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        StatusMessage = "⏳ Importando produtos... Aguarde!";
        try
        {
            var linhas    = System.IO.File.ReadAllLines(dialog.FileName, System.Text.Encoding.UTF8);
            int contador  = 0;
            int ignorados = 0;

            for (int i = 1; i < linhas.Length; i++)
            {
                var linha = linhas[i].Trim();
                if (string.IsNullOrWhiteSpace(linha)) continue;
                var col = linha.Split(';');
                if (col.Length < 5) continue;
                try
                {
                    string  nome    = col[0].Trim().Trim('"');
                    string  barcode = col[1].Trim().Trim('"').Replace("'", "");
                    decimal custo   = LimparDinheiro(col[2]);
                    decimal venda   = LimparDinheiro(col[3]);
                    decimal estq    = LimparDinheiro(col[4]);
                    string  unit    = col.Length > 5 ? col[5].Trim().Trim('"') : "UN";
                    string  ncm     = col.Length > 7 ? col[7].Trim().Trim('"') : "";
                    string  csosn   = col.Length > 8 ? col[8].Trim().Trim('"') : "";
                    string  cfop    = col.Length > 9 ? col[9].Trim().Trim('"') : "";

                    if (string.IsNullOrWhiteSpace(nome) || venda <= 0) { ignorados++; continue; }

                    await _productService.CreateAsync(new CreateProductDto
                    {
                        Name = nome, Barcode = barcode, SKU = barcode,
                        OriginalCost = custo, SalePrice = venda, Stock = estq,
                        Unit = string.IsNullOrWhiteSpace(unit) ? "UN" : unit,
                        NCM = ncm, CSOSN = csosn, CFOPPadrao = cfop,
                        IsActive = true, AllowDiscount = true,
                        DesiredMarginPercent = 0, IpiPercent = 0, IcmsPercent = 0
                    });
                    contador++;
                    if (contador % 100 == 0) StatusMessage = $"⏳ {contador} produtos importados...";
                }
                catch { ignorados++; }
            }

            StatusMessage = $"✅ {contador} importados." + (ignorados > 0 ? $" ({ignorados} ignorados)" : "");
            CurrentPage = 1;
            await LoadProductsAsync();
        }
        catch (Exception ex) { StatusMessage = $"❌ Erro: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // S17: exporta TODOS os produtos (não só a página atual na tela), já que
    // hoje não existe nenhuma forma de tirar essa lista sem entrar direto no
    // banco. Mesmo formato CSV (;) da importação já existente — abre no Excel
    // normalmente, e pode ser reimportado depois de editado.
    private async Task ExportarPlanilhaAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "Arquivo CSV (*.csv)|*.csv",
            FileName = $"Produtos_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            Title    = "Salvar planilha de produtos"
        };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        StatusMessage = "⏳ Exportando produtos... Aguarde!";
        try
        {
            // S17 FIX: IProductService.GetAllAsync() usa o repositório genérico,
            // que NÃO inclui Category/Brand (só o SearchAsync tinha Include de
            // Category, e nem esse tinha Brand) — por isso as duas colunas
            // vinham sempre vazias. A tela de produtos nunca tinha mostrado
            // esses dois campos antes, então o bug ficou invisível até agora.
            // Consulta própria aqui, direto no contexto, com os Includes certos.
            List<Product> todos;
            using (var scope = App.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                todos = await ctx.Products
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .Include(p => p.Brand)
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }

            string CampoCsv(string? v)
            {
                v ??= string.Empty;
                return (v.Contains(';') || v.Contains('"') || v.Contains('\n'))
                    ? "\"" + v.Replace("\"", "\"\"") + "\""
                    : v;
            }

            var linhas = new List<string>
            {
                "Nome;SKU;CodigoBarras;Categoria;Marca;Unidade;Estoque;EstoqueMinimo;Custo;PrecoVenda;Ativo"
            };

            foreach (var p in todos)
            {
                linhas.Add(string.Join(';', new[]
                {
                    CampoCsv(p.Name),
                    CampoCsv(p.SKU),
                    CampoCsv(p.Barcode),
                    CampoCsv(p.Category?.Name),
                    CampoCsv(p.Brand?.Name),
                    CampoCsv(p.Unit),
                    p.Stock.ToString("N2"),
                    p.MinStock.ToString("N2"),
                    p.OriginalCost.ToString("N2"),
                    p.SalePrice.ToString("N2"),
                    p.IsActive ? "Sim" : "Não"
                }));
            }

            System.IO.File.WriteAllLines(dialog.FileName, linhas, System.Text.Encoding.UTF8);
            StatusMessage = $"✅ {todos.Count} produtos exportados.";

            var abrir = MessageBox.Show("Planilha salva! Deseja abrir agora?", "Exportação concluída",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (abrir == MessageBoxResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch (Exception ex) { StatusMessage = $"❌ Erro ao exportar: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private decimal LimparDinheiro(string valorExcel)
    {
        if (string.IsNullOrWhiteSpace(valorExcel)) return 0;
        var limpo = valorExcel.Replace("R$","").Replace("\"","").Replace(" ","").Replace(",",".").Trim();
        return decimal.TryParse(limpo, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0;
    }

    private void ExecutarAbrirAjusteEstoque()
    {
        if (!ERP.WPF.State.PermissionChecker.Has(ERP.WPF.State.PermissionChecker.StockAdjust))
        {
            var senha = new Views.SenhaGerenteView();
            senha.ShowDialog();
            if (!senha.Autorizado)
            {
                StatusMessage = "Operação bloqueada — necessário senha de gerente.";
                return;
            }
        }
        var janela = new Views.AjusteEstoqueView { DataContext = _ajusteVm, Owner = System.Windows.Application.Current.MainWindow };
        janela.ShowDialog();
        LoadProductsAsync().SafeFireAndForgetSilentAsync("loadProducts");
    }

    // ── MÁGICA DO LEITOR DE CÓDIGO DE BARRAS ──────────────────────────────
    public async Task ProcessarLeituraDeCodigoAsync(string codigoLido)
    {
        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IProductService>();

            // Busca se o produto já existe pelo EAN ou SKU
            var produtoExistente = await service.GetByBarcodeAsync(codigoLido)
                               ?? await service.GetBySkuAsync(codigoLido);

            if (produtoExistente != null)
            {
                // PRODUTO EXISTE: Carrega ele pra tela
                var dto = produtoExistente;
                
                SelectedProduct = dto; // Isso aciona a sua LoadFormFromDto() automaticamente
                StatusMessage = "✅ Produto existente carregado para edição!";
            }
            else
            {
                // PRODUTO NOVO: Move o código pro campo Barcode/SKU e limpa o Nome
                Barcode = codigoLido;
                SKU = codigoLido;
                Name = string.Empty; 
                StatusMessage = "✨ Novo código lido! Pode digitar o nome do produto.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao ler código: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}