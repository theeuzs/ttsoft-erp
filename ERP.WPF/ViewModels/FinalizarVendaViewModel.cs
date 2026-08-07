using ERP.Application.DTOs;
using ERP.Application.DTOs.FocusNfe; 
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using Dapper; 
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System;
using System.Threading.Tasks;
using System.Globalization; 
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ERP.WPF.ViewModels;

public class FinalizarVendaViewModel : BaseViewModel
{
    private readonly ISaleService _saleService;
    private readonly ICustomerService _customerService; 
    private readonly Action<Guid> _onSuccess;
    private readonly string _clienteEnderecoFormatado;

    public event EventHandler OnRequestClose;

    public ObservableCollection<CartItem> ItensCarrinho { get; }
    public decimal TotalVenda { get; }

    // ==========================================================
    // 0. LÓGICA DO VENDEDOR
    // ==========================================================
    public ObservableCollection<UserDto> Vendedores { get; } = new();

    private UserDto _selectedVendedor;
    public UserDto SelectedVendedor
    {
        get => _selectedVendedor;
        set => SetProperty(ref _selectedVendedor, value);
    }

    private async Task CarregarVendedoresAsync()
    {
        try
        {
            Vendedores.Clear();
            using (var scope = ERP.WPF.App.Services.CreateScope())
            {
                var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                var listaDeUsuarios = await userService.GetAllAsync();
                
                foreach (var vendedor in listaDeUsuarios)
                    Vendedores.Add(vendedor);
            }

            var nomeLogado = ERP.WPF.State.AppSession.UserName?.ToUpper();
            SelectedVendedor = Vendedores.FirstOrDefault(v => 
                v.Username?.ToUpper() == nomeLogado || 
                v.Name?.ToUpper() == nomeLogado);
        }
        catch (Exception ex) { MessageBox.Show($"Erro ao carregar vendedores: {ex.Message}", "Aviso Vila Verde"); }
    }

    // ==========================================================
    // 1. LÓGICA DE CLIENTES E CADASTRO RÁPIDO
    // ==========================================================
    public ObservableCollection<CustomerDto> Customers { get; } = new();

    private CustomerDto _selectedCustomer;
    public CustomerDto SelectedCustomer
    {
        get => _selectedCustomer;
        set 
        { 
            SetProperty(ref _selectedCustomer, value); 
            if (_selectedCustomer != null && EntregarNoEndereco)
            {
                string rua = _selectedCustomer.Street ?? "";
                string numero = !string.IsNullOrWhiteSpace(_selectedCustomer.Number) ? $", {_selectedCustomer.Number}" : "";
                string bairro = !string.IsNullOrWhiteSpace(_selectedCustomer.Neighborhood) ? $" - {_selectedCustomer.Neighborhood}" : "";
                EnderecoEntrega = $"{rua}{numero}{bairro}";
            }
        }
    }

    // ── Busca de cliente ────────────────────────────────
    private string _clienteBusca = string.Empty;
    public string ClienteBusca
    {
        get => _clienteBusca;
        set { SetProperty(ref _clienteBusca, value); FiltrarClientes(value); }
    }

    private bool _clienteListaAberta;
    public bool ClienteListaAberta
    {
        get => _clienteListaAberta;
        set => SetProperty(ref _clienteListaAberta, value);
    }

    public ObservableCollection<CustomerDto> ClientesFiltrados { get; } = new();

    public ICommand SelecionarClienteCommand => new RelayCommand(p =>
    {
        if (p is CustomerDto c)
        {
            SelectedCustomer   = c;
            _clienteBusca      = c.Name;
            OnPropertyChanged(nameof(ClienteBusca));
            ClienteListaAberta = false;
        }
    });

    private void FiltrarClientes(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Length < 2)
        {
            ClienteListaAberta = false;
            ClientesFiltrados.Clear();
            return;
        }

        var filtrados = Customers
            .Where(c => c.Name.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                        (c.Document?.Contains(termo) ?? false))
            .Take(10)
            .ToList();

        ClientesFiltrados.Clear();
        foreach (var c in filtrados) ClientesFiltrados.Add(c);
        ClienteListaAberta = filtrados.Any();
    }

    public ICommand QuickCreateCustomerCommand => new RelayCommand(_ => OpenQuickCreateCustomer());

    private async Task OpenQuickCreateCustomer()
    {
        using var scope = ERP.WPF.App.Services.CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        var quickVm   = new QuickCustomerViewModel(customerService);
        var quickView = new Views.QuickCustomerView(quickVm);

        if (quickView.ShowDialog() == true)
        {
            await LoadCustomersAsync(null);
            var novoCliente = Customers.FirstOrDefault(c => c.Name == quickVm.NomeSalvo);
            if (novoCliente != null) SelectedCustomer = novoCliente;
        }
    }

    // ==========================================================
    // CONSTRUTOR
    // ==========================================================
    public FinalizarVendaViewModel(
        ISaleService saleService, 
        ICustomerService customerService,
        ObservableCollection<CartItem> itens, 
        decimal total, 
        Guid? clienteId, 
        string clienteNome,
        string clienteEndereco,
        Action<Guid> onSuccess)
    {
        _saleService = saleService;
        _customerService = customerService;
        ItensCarrinho = itens;
        TotalVenda = total;
        _clienteEnderecoFormatado = clienteEndereco ?? string.Empty;
        _onSuccess = onSuccess;

        ValorDigitado = Math.Round(total, 2);
        
        AbrirFidelidadeCommand = new AsyncRelayCommand(_ => AbrirFidelidadeAsync());
        FinalizarNormalCommand = new AsyncRelayCommand(_ => FinalizarVendaAsync("NORMAL"), _ => FaltaPagar <= 0);
        FinalizarNfceCommand = new AsyncRelayCommand(_ => FinalizarVendaAsync("NFCE"), _ => FaltaPagar <= 0);
        FinalizarNfeCommand = new AsyncRelayCommand(_ => FinalizarVendaAsync("NFE"), _ => FaltaPagar <= 0);
        
        _ = CarregarDadosIniciaisAsync(clienteId);
    }

   private async Task LoadCustomersAsync(Guid? clienteIdSelecionado)
   {
       try
       {
           using (var scope = ERP.WPF.App.Services.CreateScope())
           {
               var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
               var clientesDoBanco = await customerService.GetAllAsync();

               Customers.Clear();
               foreach (var c in clientesDoBanco.OrderBy(c => c.Name))
                   Customers.Add(c);

               if (clienteIdSelecionado.HasValue)
               {
                   SelectedCustomer = Customers.FirstOrDefault(c => c.Id == clienteIdSelecionado.Value);
                   if (SelectedCustomer != null)
                   {
                       _clienteBusca = SelectedCustomer.Name;
                       OnPropertyChanged(nameof(ClienteBusca));
                   }
               }
           }
       }
       catch { }
   }

    private async Task CarregarDadosIniciaisAsync(Guid? clienteIdSelecionado)
    {
        await LoadCustomersAsync(clienteIdSelecionado);
        await CarregarVendedoresAsync();
        await CarregarOperadoraPadraoAsync();
    }

    // ── Item 2.3 (Comercial) — taxa de operadora informativa no PDV.
    // Não altera o valor que o cliente paga (isso seria a Interpretação B,
    // não escolhida) — só mostra pro operador quanto a loja vai receber
    // líquido, reaproveitando OperadoraRecebimento.CalcularRecebimento que
    // já existia pronto, sem nenhum fluxo real consumindo ele até agora.
    private ERP.Application.DTOs.OperadoraRecebimentoDto? _operadoraPadrao;

    private async Task CarregarOperadoraPadraoAsync()
    {
        try
        {
            using var scope = ERP.WPF.App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOperadoraRecebimentoService>();
            var ativas  = await service.ObterAtivasAsync();
            _operadoraPadrao = ativas.FirstOrDefault(o => o.OperadoraPadrao);
        }
        catch
        {
            // Best-effort — sem operadora padrão configurada, a tela de venda
            // continua funcionando normal, só sem essa informação extra.
            _operadoraPadrao = null;
        }
    }

    private decimal _valorTaxaOperadora;
    public decimal ValorTaxaOperadora { get => _valorTaxaOperadora; private set => SetProperty(ref _valorTaxaOperadora, value); }

    private decimal _valorLiquidoOperadora;
    public decimal ValorLiquidoOperadora { get => _valorLiquidoOperadora; private set => SetProperty(ref _valorLiquidoOperadora, value); }

    public bool MostrarResumoOperadora => _operadoraPadrao != null && ValorTaxaOperadora > 0;

    private void AtualizarResumoOperadora()
    {
        if (_operadoraPadrao == null)
        {
            ValorTaxaOperadora = 0;
            ValorLiquidoOperadora = 0;
            OnPropertyChanged(nameof(MostrarResumoOperadora));
            return;
        }

        var valorDebito           = Pagamentos.Where(p => p.Forma == PaymentMethod.CartaoDebito).Sum(p => p.Valor);
        var valorCreditoVista     = Pagamentos.Where(p => p.Forma == PaymentMethod.CartaoCredito && !p.EhParcelado).Sum(p => p.Valor);
        var valorCreditoParcelado = Pagamentos.Where(p => p.Forma == PaymentMethod.CartaoCredito && p.EhParcelado).Sum(p => p.Valor);

        var taxaDebito           = Math.Round(valorDebito           * (_operadoraPadrao.TaxaDebitoPercentual           / 100m), 2);
        var taxaCreditoVista     = Math.Round(valorCreditoVista     * (_operadoraPadrao.TaxaCreditoVistaPercentual     / 100m), 2);
        var taxaCreditoParcelado = Math.Round(valorCreditoParcelado * (_operadoraPadrao.TaxaCreditoParceladoPercentual / 100m), 2);

        ValorTaxaOperadora    = taxaDebito + taxaCreditoVista + taxaCreditoParcelado;
        ValorLiquidoOperadora = (valorDebito + valorCreditoVista + valorCreditoParcelado) - ValorTaxaOperadora;
        OnPropertyChanged(nameof(MostrarResumoOperadora));
    }

    // ==========================================================
    // 2. LÓGICA DO DESCONTO, MATEMÁTICA E PAGAMENTOS
    // ==========================================================

    private decimal _descontoPercentual;
    public decimal DescontoPercentual
    {
        get => _descontoPercentual;
        set { if (_descontoPercentual != value) ProcessarDesconto(value, 0, true); }
    }

    private decimal _descontoReais;
    public decimal DescontoReais
    {
        get => _descontoReais;
        set { if (_descontoReais != value) ProcessarDesconto(0, value, false); }
    }

    private void ProcessarDesconto(decimal percentual, decimal valorReais, bool alterouPercentual)
    {
        decimal novoPercentual = percentual;
        decimal novoValorReais = valorReais;

        // 1. Calcula a matemática cruzada
        if (alterouPercentual)
        {
            novoValorReais = Math.Round(TotalVenda * (novoPercentual / 100m), 2);
        }
        else
        {
            if (TotalVenda > 0)
                novoPercentual = Math.Round((novoValorReais / TotalVenda) * 100m, 2);
            else
                novoPercentual = 0;
        }

        // 2. Trava para não dar desconto maior que o valor da compra
        if (novoValorReais > TotalVenda)
        {
            novoValorReais = TotalVenda;
            novoPercentual = 100m;
        }

        // 3. Trava de Segurança do Perfil do Usuário
        decimal maxDescontoPermitido = ERP.WPF.State.PermissionChecker.GetMaxDiscountPercentage();

        if (novoPercentual > maxDescontoPermitido)
        {
            var telaSenha = new ERP.WPF.Views.SenhaGerenteView();
            telaSenha.Owner = System.Windows.Application.Current.MainWindow;
            telaSenha.ShowDialog();

            if (!telaSenha.Autorizado)
            {
                MessageBox.Show($"Seu perfil permite um desconto máximo de {maxDescontoPermitido:N0}%.\nAutorização do gerente não fornecida.", 
                    "Desconto Bloqueado", MessageBoxButton.OK, MessageBoxImage.Warning);
                
                // Reverte os valores da tela para o que já estava validado antes
                OnPropertyChanged(nameof(DescontoPercentual));
                OnPropertyChanged(nameof(DescontoReais));
                return;
            }
        }

        // Se passou em tudo ou foi autorizado, aplica na veia!
        _descontoPercentual = novoPercentual;
        _descontoReais = novoValorReais;
        
        // Atualiza a tela
        OnPropertyChanged(nameof(DescontoPercentual));
        OnPropertyChanged(nameof(DescontoReais));

        // Aciona o setter antigo do Desconto pra atualizar os totais
        this.Desconto = novoValorReais; 
    }

    private decimal _desconto;
    public decimal Desconto
    {
        get => _desconto;
        set
        {
            if (SetProperty(ref _desconto, value))
            {
                AtualizarTotais();
                OnPropertyChanged(nameof(Desconto)); 
                OnPropertyChanged(nameof(TemDesconto)); 
                OnPropertyChanged(nameof(ValorComDesconto));

                if (FaltaPagar > 0 && Pagamentos.Count == 0) ValorDigitado = Math.Round(FaltaPagar, 2); 
            }
        }
    }

    public bool TemDesconto => Desconto > 0 || DescontoFidelidade > 0;
    public decimal ValorComDesconto => TotalVenda - Desconto - DescontoFidelidade;
    public decimal TotalComDesconto => TotalVenda - Desconto - DescontoFidelidade;
    public ObservableCollection<PagamentoItem> Pagamentos { get; } = new();

    private decimal _valorDigitado;
    public decimal ValorDigitado 
    { 
        get => _valorDigitado; 
        set 
        { 
            SetProperty(ref _valorDigitado, value); 
            CommandManager.InvalidateRequerySuggested(); 
            OnPropertyChanged(nameof(TrocoDinamico)); 
            OnPropertyChanged(nameof(FaltaPagarDinamico));
        } 
    }

    public decimal TotalPago => Pagamentos.Sum(p => p.Valor);
    public decimal FaltaPagar => Math.Max(0, TotalComDesconto - TotalPago);
    public decimal Troco => Math.Max(0, TotalPago - TotalComDesconto);
    public decimal FaltaPagarDinamico => Math.Max(0, FaltaPagar - ValorDigitado);
    public decimal TrocoDinamico => Math.Max(0, ValorDigitado - FaltaPagar);

    public ICommand AdicionarPagamentoCommand => new RelayCommand(async _ => 
    {
        if (ValorDigitado <= 0) return;

        if (FormaPagamento == PaymentMethod.APrazo && SelectedCustomer == null)
        {
            MessageBox.Show("Para vender A Prazo, é OBRIGATÓRIO selecionar um cliente cadastrado!",
                "Atenção - Vila Verde", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // ── Validação de saldo Haver ──────────────────────────────────────
        if (FormaPagamento == PaymentMethod.Haver)
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Selecione um cliente para usar o saldo Haver!",
                    "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var scope = ERP.WPF.App.Services.CreateScope();
            var customerSvc = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var clienteDto  = await customerSvc.GetByIdAsync(SelectedCustomer.Id);
            var saldoReal   = clienteDto?.HaverBalance ?? 0;

            if (ValorDigitado > saldoReal)
            {
                MessageBox.Show($"Saldo Haver insuficiente!\nSaldo disponível: R$ {saldoReal:N2}",
                    "Saldo Insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        // ─────────────────────────────────────────────────────────────────

        if (FormaPagamento != PaymentMethod.Dinheiro && ValorDigitado > FaltaPagar)
        {
            MessageBox.Show($"Pagamentos no {FormaPagamento} não podem gerar troco.\nO valor será ajustado automaticamente.",
                "Vila Verde - Aviso");
            ValorDigitado = Math.Round(FaltaPagar, 2);
        }

        Pagamentos.Add(new PagamentoItem { Forma = FormaPagamento, Valor = ValorDigitado, EhParcelado = IsParceladoDigitado });
        IsParceladoDigitado = false;
        AtualizarTotais();
        ValorDigitado = Math.Round(FaltaPagar, 2);

    }, _ => FaltaPagar > 0 && ValorDigitado > 0);

    public ICommand RemoverPagamentoCommand => new RelayCommand(param => 
    {
        if (param is PagamentoItem p) 
        {
            Pagamentos.Remove(p);
            AtualizarTotais();
            ValorDigitado = Math.Round(FaltaPagar, 2);
        }
    });

    private void AtualizarTotais()
    {
        AtualizarResumoOperadora();
        OnPropertyChanged(nameof(Desconto));
        OnPropertyChanged(nameof(TotalComDesconto));
        OnPropertyChanged(nameof(TotalPago));
        OnPropertyChanged(nameof(FaltaPagar));
        OnPropertyChanged(nameof(Troco));
        OnPropertyChanged(nameof(TrocoDinamico));
        OnPropertyChanged(nameof(FaltaPagarDinamico));
        CommandManager.InvalidateRequerySuggested();
    }

    // ==========================================================
    // 4. PROPRIEDADES DA TELA
    // ==========================================================
    private PaymentMethod _formaPagamento = PaymentMethod.Dinheiro;
    public PaymentMethod FormaPagamento
    {
        get => _formaPagamento;
        set { SetProperty(ref _formaPagamento, value); OnPropertyChanged(nameof(MostrarCheckboxParcelado)); }
    }
    public IEnumerable<PaymentMethod> FormasPagamento => Enum.GetValues<PaymentMethod>();

    private bool _isParceladoDigitado;
    /// <summary>Checkbox "É parcelado?" — só aparece quando FormaPagamento é
    /// Cartão de Crédito, define qual taxa de operadora usar no resumo
    /// informativo (item 2.3).</summary>
    public bool IsParceladoDigitado { get => _isParceladoDigitado; set => SetProperty(ref _isParceladoDigitado, value); }

    public bool MostrarCheckboxParcelado => FormaPagamento == PaymentMethod.CartaoCredito;

    private bool _entregarNoEndereco;
    // ── Observação Geral do Pedido ─────────────────────────────────────────────
    private string _observacaoGeral = string.Empty;
    public string ObservacaoGeral
    {
        get => _observacaoGeral;
        set { _observacaoGeral = value; OnPropertyChanged(nameof(ObservacaoGeral)); }
    }

    public bool EntregarNoEndereco 
    { 
        get => _entregarNoEndereco; 
        set 
        { 
            SetProperty(ref _entregarNoEndereco, value); 
            OnPropertyChanged(nameof(EnderecoVisivel)); 
            if (value) EnderecoEntrega = SelectedCustomer != null ? $"{SelectedCustomer.Street}, {SelectedCustomer.Number} - {SelectedCustomer.Neighborhood}" : _clienteEnderecoFormatado;
            else EnderecoEntrega = string.Empty; 
        } 
    }
    public Visibility EnderecoVisivel => EntregarNoEndereco ? Visibility.Visible : Visibility.Collapsed;
    private string _enderecoEntrega = string.Empty;
    public string EnderecoEntrega { get => _enderecoEntrega; set => SetProperty(ref _enderecoEntrega, value); }

    public decimal  DescontoFidelidade      { get; private set; } = 0;
    private int     _pontosADebitar            = 0; // só debitado após venda confirmada
    public ICommand AbrirFidelidadeCommand { get; }
    public ICommand FinalizarNormalCommand { get; }
    public ICommand FinalizarNfceCommand { get; }
    public ICommand FinalizarNfeCommand { get; }

    private bool _mostrarComissao;
    public bool MostrarComissao { get => _mostrarComissao; set { SetProperty(ref _mostrarComissao, value); OnPropertyChanged(nameof(ComissaoVisivel)); } }
    public Visibility ComissaoVisivel => MostrarComissao ? Visibility.Visible : Visibility.Collapsed;
    public decimal TotalComissao => ItensCarrinho.Sum(item => item.Total * 0.01m);
    public ICommand ToggleComissaoCommand => new RelayCommand(_ => MostrarComissao = !MostrarComissao);

    // ==========================================================
    // 6. O CORAÇÃO DA TELA: SALVAR E EMITIR NOTA
    // ==========================================================
    private async Task FinalizarVendaAsync(string tipoEmissao)
    {
        IsBusy = true;
        try
        {
            if (tipoEmissao == "NFE" && SelectedCustomer == null)
            {
                MessageBox.Show("Para emitir NF-e (A4), você precisa selecionar um cliente com CPF/CNPJ e endereço completo!", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pagamentoPix = Pagamentos.FirstOrDefault(p => p.Forma == PaymentMethod.Pix);
            if (pagamentoPix != null)
            {
                var config = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
                if (!string.IsNullOrWhiteSpace(config.ChavePix))
                {
                    string txid = $"ERP{DateTime.Now:yyyyMMddHHmmss}";
                    var pixView = new ERP.WPF.Views.PixQrCodeView(
                        valor:            pagamentoPix.Valor,
                        chavePix:         config.ChavePix,
                        nomeBeneficiario: config.NomeFantasia,
                        cidade:           "BRASIL",
                        txid:             txid);

                    // Código morto da auditoria ativado (06/08/2026) — a
                    // janela já tinha o método ConfirmarPagamentoAutomaticamente()
                    // pronto, esperando por isso, com o comentário "Chamado
                    // pelo PDV quando recebe confirmação externa". PixPollingService
                    // existia, testado, mas nunca era instanciado em lugar
                    // nenhum. Sem token configurado, IniciarPolling() é um
                    // no-op (mesmo comportamento manual de sempre).
                    using var poller = new ERP.WPF.Helpers.PixPollingService();
                    poller.PagamentoConfirmado += () => pixView.ConfirmarPagamentoAutomaticamente();
                    poller.IniciarPolling(txid, config.PixApiToken, config.PixProvedor);

                    bool? resultado = pixView.ShowDialog();
                    poller.Parar();
                }
            }

            var fiscalCalculator = ERP.WPF.App.Services.GetRequiredService<ERP.Domain.Services.Fiscal.IFiscalCalculator>();
            decimal impostosAproximados = fiscalCalculator.CalcularTributosAproximados(this.TotalVenda, 13.45m);
            string msgFiscal = $"\nTrib. Aprox. R$: {impostosAproximados:N2} (Lei 12.741/12)";
            string observacaoCompleta = (this.EntregarNoEndereco && !string.IsNullOrWhiteSpace(this.EnderecoEntrega) ? this.EnderecoEntrega : "") + msgFiscal;
            // Observação geral é passada separadamente para o recibo (aparece antes dos itens)

            // Idempotência financeira granular (achado de auditoria pré-Fase-2 do
            // Offline-First, 08/2026) — gera o Id de cada linha de pagamento AQUI,
            // uma vez só, ANTES de qualquer chamada de rede. O mesmo Id vai tanto
            // pro CreateSaleDto (vira SalePayment.Id no banco) quanto, mais abaixo,
            // pro ProcessarRecebimentoVendaAsync — é essa igualdade que permite ao
            // Motor Financeiro saber exatamente qual linha está processando.
            var linhasPagamento = Pagamentos
                .Select(p => (Id: Guid.NewGuid(), p.Forma, p.Valor))
                .ToList();

            var dto = new CreateSaleDto
            {
                CustomerId = SelectedCustomer?.Id,
                SellerName = SelectedVendedor?.Name,
                UsuarioId = ERP.WPF.State.AppSession.UserId, 
                Notes = observacaoCompleta,
                DiscountAmount = this.Desconto + this.DescontoFidelidade,
                Payments = linhasPagamento.Select(p => new CreateSalePaymentDto { Id = p.Id, PaymentMethod = p.Forma, Amount = p.Valor }).ToList(), 
                Items = ItensCarrinho.Select(i => new CreateSaleItemDto { ProductId = i.ProductId, Quantity = i.Quantity, UnitPrice = i.UnitPrice, DiscountPercent = 0, FatorConversao = i.FatorConversao, TotalItem = i.Total }).ToList()
            };

            var vendaSalva = await _saleService.CreateAsync(dto);
            await SalvarNoCaixaEContasAReceberAsync(vendaSalva.Id, linhasPagamento);

            // Debitar pontos de fidelidade SÓ após venda confirmada
            if (_pontosADebitar > 0 && SelectedCustomer != null)
            {
                try
                {
                    using var scope = ERP.WPF.App.Services.CreateScope();
                    var fid = scope.ServiceProvider
                        .GetRequiredService<ERP.Application.Interfaces.IFidelidadeService>();
                    await fid.ResgatarPontosAsync(SelectedCustomer.Id, _pontosADebitar, "Resgate PDV");
                    _pontosADebitar = 0;
                }
                catch { /* não bloqueia a venda se falhar */ }
            }

            if (tipoEmissao == "NORMAL")
            {
                var resposta = MessageBox.Show("✅ Venda finalizada com sucesso!\n\nDeseja imprimir o recibo da venda?", 
                                               "Vila Verde - Impressão", 
                                               MessageBoxButton.YesNo, 
                                               MessageBoxImage.Question);
                
                if (resposta == MessageBoxResult.Yes)
                {
                    ImprimirReciboInterno(vendaSalva.Id, observacaoCompleta, vendaSalva.SaleNumber);
                }
            }
            else if (tipoEmissao == "NFCE" || tipoEmissao == "NFE")
            {
                await EmitirNotaViaFiscalServiceAsync(vendaSalva.Id, tipoEmissao);
            }

            _onSuccess?.Invoke(vendaSalva.Id); 
            ERP.WPF.ViewModels.PdvViewModel.NotificacaoCaixaAlterado?.Invoke();
            OnRequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) { MessageBox.Show($"❌ Erro ao finalizar: {ex.Message}", "Erro na Venda", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsBusy = false; }
    }

    // ==========================================================
    // 7. INTEGRAÇÃO COM A FOCUS NFE (O MOTOR DE DISPARO)
    // ==========================================================
    private async Task EmitirNotaViaFiscalServiceAsync(Guid vendaId, string tipoDocumento)
    {
        // Etapa 1 da refatoracao fiscal: a logica de montar o payload e chamar
        // o Focus NFe agora mora em IFiscalService (Application/Infrastructure),
        // sem nenhuma dependencia de UI. Esse metodo so decide o que MOSTRAR
        // pro operador com base no resultado - a regra de negocio em si nao mudou.
        var fiscalService = ERP.WPF.App.Services.GetRequiredService<IFiscalService>();
        var resultado = await fiscalService.EmitirNotaAsync(vendaId, tipoDocumento);

        if (resultado.Sucesso && resultado.EmContingencia)
        {
            MessageBox.Show(
                "\ud83d\udce1 Venda salva em MODO CONTINGENCIA!\n\nA internet parece estar instavel. A nota sera enviada para a SEFAZ automaticamente assim que a conexao voltar.",
                "Modo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else if (resultado.Sucesso && !string.IsNullOrWhiteSpace(resultado.UrlDanfe))
        {
            MessageBox.Show($"\u2705 {resultado.Mensagem}", "Sefaz - Sucesso!", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = resultado.UrlDanfe, UseShellExecute = true });
        }
        else
        {
            MessageBox.Show($"\u274c Falha na emissao fiscal: {resultado.Mensagem}", "Erro Fiscal", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    // ==========================================================
    // FUNÇÕES AUXILIARES (Para o código ficar limpo)
    // ==========================================================
    private async Task SalvarNoCaixaEContasAReceberAsync(
        Guid vendaId, List<(Guid Id, PaymentMethod Forma, decimal Valor)> linhasPagamento)
    {
        using (var scope = ERP.WPF.App.Services.CreateScope())
        {
            // S17 FIX: essa lógica inteira (Dinheiro→Caixa, PIX→Conta Bancária,
            // Cartão→só Caixa por enquanto, A Prazo→Conta a Receber, Haver→saldo
            // do cliente) morava toda aqui dentro do PDV. Movida pro
            // IMotorFinanceiroService — o PDV não decide mais essa regra sozinho,
            // só entrega os dados da venda. Quando "Recebíveis de Operadora"
            // existir, essa mudança acontece só no Motor Financeiro, o PDV nunca
            // mais precisa ser tocado por causa disso.
            var motorFinanceiro = scope.ServiceProvider.GetRequiredService<IMotorFinanceiroService>();

            Guid usuarioId = ERP.WPF.State.AppSession.UserId;
            var nomeCliente  = SelectedCustomer?.Name ?? "Consumidor final";
            var nomeVendedor = SelectedVendedor?.Name ?? "Balcão";
            var nomeOperador = ERP.WPF.State.AppSession.UserName ?? "PDV";

            await motorFinanceiro.ProcessarRecebimentoVendaAsync(
                vendaId, usuarioId, SelectedCustomer?.Id, nomeCliente, nomeVendedor, nomeOperador, Troco,
                linhasPagamento.Select(p => (p.Id, p.Forma, p.Valor)));
        }
    }

    private void ImprimirReciboInterno(Guid idGerado, string observacaoCompleta, string? numeroVenda = null)
    {
        ERP.WPF.Helpers.ReciboPrinter.Imprimir(
            idGerado, this.ItensCarrinho, this.TotalVenda, this.Desconto,
            this.SelectedCustomer?.Name ?? "CONSUMIDOR FINAL", this.SelectedVendedor?.Name,
            this.Pagamentos.Select(p => (p.Forma.ToString(), p.Valor)), this.Troco,
            observacaoCompleta,
            numeroVenda: numeroVenda,
            observacaoGeral: string.IsNullOrWhiteSpace(this.ObservacaoGeral) ? null : this.ObservacaoGeral
        );

        if (this.EntregarNoEndereco && !string.IsNullOrWhiteSpace(this.EnderecoEntrega))
        {
            ERP.WPF.Helpers.ReciboPrinter.Imprimir(
                idGerado, this.ItensCarrinho, this.TotalVenda, this.Desconto,
                this.SelectedCustomer?.Name ?? "CONSUMIDOR FINAL", this.SelectedVendedor?.Name,
                this.Pagamentos.Select(p => (p.Forma.ToString(), p.Valor)), this.Troco,
                observacaoCompleta,
                "VIA DA ENTREGA",
                numeroVenda: numeroVenda,
                observacaoGeral: string.IsNullOrWhiteSpace(this.ObservacaoGeral) ? null : this.ObservacaoGeral
            );
        }
    }
    // ── Sprint Q: Programa de Fidelidade ─────────────────────────────────────
    private async Task AbrirFidelidadeAsync()
    {
        if (SelectedCustomer == null)
        {
            MessageBox.Show("Selecione um cliente para usar o programa de fidelidade.",
                "Fidelidade", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        using var scope = ERP.WPF.App.Services.CreateScope();
        var svc  = scope.ServiceProvider.GetRequiredService<ERP.Application.Interfaces.IFidelidadeService>();
        var vm   = new FidelidadeViewModel(SelectedCustomer.Id, SelectedCustomer.Name, svc);
        var view = new ERP.WPF.Views.FidelidadeView(vm);

        if (view.ShowDialog() == true && view.DescontoAplicado > 0)
        {
            DescontoFidelidade = view.DescontoAplicado;
            _pontosADebitar    = vm.PontosParaResgatar;
            OnPropertyChanged(nameof(DescontoFidelidade));
            OnPropertyChanged(nameof(TotalComDesconto));
            OnPropertyChanged(nameof(TemDesconto));
            OnPropertyChanged(nameof(ValorComDesconto));
            MessageBox.Show($"✅ Desconto de {view.DescontoAplicado:C} aplicado!",
                "Fidelidade", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
public class PagamentoItem
{
    public PaymentMethod Forma { get; set; }
    public decimal Valor { get; set; }

    /// <summary>Só pra escolher qual taxa de operadora usar no resumo
    /// informativo (item 2.3) — o sistema não rastreia parcelamento em
    /// lugar nenhum, então isso não vai pro servidor, é só exibição local.</summary>
    public bool EhParcelado { get; set; }
}