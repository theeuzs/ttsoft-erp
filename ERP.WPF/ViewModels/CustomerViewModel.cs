using ERP.Application.DTOs;
using ERP.Domain.Enums;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class CustomerViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;
    private Guid? _currentCustomerId;

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

    // ── Lista e seleção ───────────────────────────────────────────────────
    public ObservableCollection<CustomerDto> CustomersList { get; } = new();

    private string _searchTerm = string.Empty;
    public string SearchTerm
    {
        get => _searchTerm;
        set { SetProperty(ref _searchTerm, value); _ = BuscarAsync(); }
    }

    private CustomerDto? _selectedCustomer;
    public CustomerDto? SelectedCustomer
    {
        get => _selectedCustomer;
        set { SetProperty(ref _selectedCustomer, value); if (_selectedCustomer != null) PreencherFormulario(_selectedCustomer); }
    }

    // ── Campos do formulário ──────────────────────────────────────────────
    private string _name = string.Empty;
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _document = string.Empty;
    public string Document { get => _document; set => SetProperty(ref _document, value); }

    private string _phone = string.Empty;
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }

    // ── Sprint C: Grupo de preço ────────────────────────────────────────────
    private GrupoPreco _grupoPreco = GrupoPreco.A;
    public GrupoPreco GrupoPreco
    {
        get => _grupoPreco;
        set => SetProperty(ref _grupoPreco, value);
    }
    public IEnumerable<GrupoPreco> GruposDisponiveis { get; } =
        Enum.GetValues<GrupoPreco>();

    // ── Sprint D: Crediário ──────────────────────────────────────────────────
    private decimal _limiteCredito;
    public decimal LimiteCredito
    {
        get => _limiteCredito;
        set => SetProperty(ref _limiteCredito, value);
    }

    private decimal _saldoDevedor;
    public decimal SaldoDevedor
    {
        get => _saldoDevedor;
        set { SetProperty(ref _saldoDevedor, value); OnPropertyChanged(nameof(CreditoDisponivel)); OnPropertyChanged(nameof(SituacaoCredito)); }
    }
    public decimal CreditoDisponivel => LimiteCredito > 0 ? Math.Max(0, LimiteCredito - SaldoDevedor) : decimal.MaxValue;
    public string SituacaoCredito
    {
        get
        {
            if (LimiteCredito <= 0) return "Sem limite definido";
            var pct = SaldoDevedor / LimiteCredito * 100;
            return pct >= 100 ? "🔴 LIMITE ESGOTADO" : pct >= 80 ? "🟡 Limite próximo" : "🟢 Disponível";
        }
    }

    private string _email = string.Empty;
    public string Email { get => _email; set => SetProperty(ref _email, value); }

    private string _stateRegistration = string.Empty;
    public string StateRegistration { get => _stateRegistration; set => SetProperty(ref _stateRegistration, value); }

    private string _zipCode = string.Empty;
    public string ZipCode { get => _zipCode; set => SetProperty(ref _zipCode, value); }

    private string _street = string.Empty;
    public string Street { get => _street; set => SetProperty(ref _street, value); }

    private string _number = string.Empty;
    public string Number { get => _number; set => SetProperty(ref _number, value); }

    private string _complement = string.Empty;
    public string Complement { get => _complement; set => SetProperty(ref _complement, value); }

    private string _neighborhood = string.Empty;
    public string Neighborhood { get => _neighborhood; set => SetProperty(ref _neighborhood, value); }

    private string _city = string.Empty;
    public string City { get => _city; set => SetProperty(ref _city, value); }

    private string _state = string.Empty;
    public string State { get => _state; set => SetProperty(ref _state, value); }

    // ── Resumo ────────────────────────────────────────────────────────────
    private int _totalClientes;
    public int TotalClientes { get => _totalClientes; private set => SetProperty(ref _totalClientes, value); }

    public string TituloFormulario => _currentCustomerId.HasValue ? "✏️ Editando Cliente" : "➕ Novo Cliente";

    public HaverViewModel? HaverVm => _currentCustomerId.HasValue
        ? new HaverViewModel(_currentCustomerId.Value, Name)
        : null;

    // ── Comandos ──────────────────────────────────────────────────────────
    public ICommand NewCommand            { get; }
    public ICommand SaveCommand           { get; }
    public ICommand DeleteCommand         { get; }
    public ICommand SearchCnpjCommand     { get; }
    public ICommand AbrirHistoricoCommand { get; }
    public ICommand LimparBuscaCommand    { get; }

    public CustomerViewModel(ICustomerService customerService)
    {
        _customerService = customerService;

        NewCommand            = new RelayCommand(_ => ClearForm());
        SaveCommand           = new RelayCommand(async _ => await SaveCustomerAsync());
        DeleteCommand         = new RelayCommand(async _ => await DeleteCustomerAsync());
        SearchCnpjCommand     = new RelayCommand(async _ => await SearchCnpjAsync());
        AbrirHistoricoCommand = new RelayCommand(p => AbrirHistorico(p as CustomerDto));
        LimparBuscaCommand    = new RelayCommand(_ => SearchTerm = string.Empty);

        ProximaPaginaCommand  = new RelayCommand(async _ => { CurrentPage++; await LoadCustomersAsync(); }, _ => PodeProxima);
        AnteriorPaginaCommand = new RelayCommand(async _ => { CurrentPage--; await LoadCustomersAsync(); }, _ => PodeAnterior);

        _ = LoadCustomersAsync();
    }

    // ── Carregamento paginado ─────────────────────────────────────────────
    private async Task LoadCustomersAsync()
    {
        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();

            var resultado = await service.GetPagedAsync(
                page:     CurrentPage,
                pageSize: PageSize,
                search:   string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm);

            TotalPages = Math.Max(1, resultado.TotalPages);
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            TotalClientes = resultado.TotalItems;

            CustomersList.Clear();
            foreach (var c in resultado.Items) CustomersList.Add(c);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar clientes:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    // Busca com debounce simples — volta para página 1 ao pesquisar
    private async Task BuscarAsync()
    {
        CurrentPage = 1;
        await LoadCustomersAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private void PreencherFormulario(CustomerDto c)
    {
        _currentCustomerId = c.Id;
        Name               = c.Name         ?? string.Empty;
        Document           = c.Document     ?? string.Empty;
        Phone              = c.Phone        ?? string.Empty;
        GrupoPreco         = (GrupoPreco)c.GrupoPreco;
        LimiteCredito      = c.LimiteCredito;
        SaldoDevedor       = c.SaldoDevedor;
        Email              = string.Empty;
        StateRegistration  = c.Ie           ?? string.Empty;
        ZipCode            = c.ZipCode      ?? string.Empty;
        Street             = c.Street       ?? string.Empty;
        Number             = c.Number       ?? string.Empty;
        Complement         = string.Empty;
        Neighborhood       = c.Neighborhood ?? string.Empty;
        City               = c.City         ?? string.Empty;
        State              = c.State        ?? string.Empty;
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(HaverVm));
    }

    private void ClearForm()
    {
        _currentCustomerId = null;
        Name = Document = Phone = Email = StateRegistration = string.Empty;
        GrupoPreco    = GrupoPreco.A;
        LimiteCredito = 0;
        SaldoDevedor  = 0;
        ZipCode = Street = Number = Complement = Neighborhood = City = State = string.Empty;
        SelectedCustomer = null;
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(HaverVm));
    }

    private void AbrirHistorico(CustomerDto? cliente)
    {
        if (cliente == null) return;
        var vm   = new CustomerHistoryViewModel(cliente.Id, cliente.Name);
        var view = new Views.CustomerHistoryView { DataContext = vm };
        view.ShowDialog();
    }

    // ── Salvar ────────────────────────────────────────────────────────────
    private async Task SaveCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            MessageBox.Show("O campo Nome é obrigatório.", "Atenção",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // ── CORREÇÃO: Scope isolado por operação de escrita ──────────
            // Garante DbContext fresco com ChangeTracker limpo.
            // Sem isso, ICustomerService vindo do root provider age como
            // Singleton e acumula estado entre operações.
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();

            var dto = new CreateCustomerDto
            {
                Name              = Name,
                Document          = string.IsNullOrWhiteSpace(Document) ? null : Document,
                Phone             = Phone,        Email        = Email,
                StateRegistration = StateRegistration,
                ZipCode           = ZipCode,      Street       = Street,
                Number            = Number,       Complement   = Complement,
                Neighborhood      = Neighborhood, City         = City,
                State             = State,
                GrupoPreco        = (int)GrupoPreco,
                LimiteCredito     = LimiteCredito
            };

            if (_currentCustomerId.HasValue)
            {
                await service.UpdateAsync(_currentCustomerId.Value, dto);
                MessageBox.Show("✅ Cliente atualizado com sucesso!", "Sucesso",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await service.CreateAsync(dto);
                MessageBox.Show("✅ Cliente cadastrado com sucesso!", "Sucesso",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            ClearForm();
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar:\n{ex.InnerException?.Message ?? ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Excluir ───────────────────────────────────────────────────────────
    private async Task DeleteCustomerAsync()
    {
        if (!_currentCustomerId.HasValue)
        {
            MessageBox.Show("Selecione um cliente na lista primeiro.", "Atenção",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Excluir o cliente '{Name}'?\nEsta ação não pode ser desfeita.",
            "Confirmar Exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();

            await service.DeleteAsync(_currentCustomerId.Value);
            MessageBox.Show("✅ Cliente excluído.", "Sucesso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            ClearForm();
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao excluir:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Consulta CNPJ ─────────────────────────────────────────────────────
    private async Task SearchCnpjAsync()
    {
        string cnpj = new string(Document?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());

        if (cnpj.Length != 14)
        {
            MessageBox.Show("Digite um CNPJ válido com 14 números.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var client   = new HttpClient();
            var response       = await client.GetAsync($"https://brasilapi.com.br/api/cnpj/v1/{cnpj}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var r = doc.RootElement;

                if (r.TryGetProperty("razao_social",  out var v)) Name         = v.GetString() ?? Name;
                if (r.TryGetProperty("cep",            out v))    ZipCode      = v.GetString() ?? ZipCode;
                if (r.TryGetProperty("logradouro",     out v))    Street       = v.GetString() ?? Street;
                if (r.TryGetProperty("numero",         out v))    Number       = v.GetString() ?? Number;
                if (r.TryGetProperty("complemento",    out v))    Complement   = v.GetString() ?? Complement;
                if (r.TryGetProperty("bairro",         out v))    Neighborhood = v.GetString() ?? Neighborhood;
                if (r.TryGetProperty("municipio",      out v))    City         = v.GetString() ?? City;
                if (r.TryGetProperty("uf",             out v))    State        = v.GetString() ?? State;
                if (r.TryGetProperty("ddd_telefone_1", out v))    Phone        = v.GetString() ?? Phone;

                MessageBox.Show("✅ Dados importados da Receita Federal!", "Sucesso",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("CNPJ não encontrado na Receita.", "Aviso",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro de conexão:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}