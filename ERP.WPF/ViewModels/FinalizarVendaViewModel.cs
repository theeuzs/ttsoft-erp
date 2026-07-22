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

        Pagamentos.Add(new PagamentoItem { Forma = FormaPagamento, Valor = ValorDigitado });
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
    public PaymentMethod FormaPagamento { get => _formaPagamento; set => SetProperty(ref _formaPagamento, value); }
    public IEnumerable<PaymentMethod> FormasPagamento => Enum.GetValues<PaymentMethod>();

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
                    var pixView = new ERP.WPF.Views.PixQrCodeView(
                        valor:            pagamentoPix.Valor,
                        chavePix:         config.ChavePix,
                        nomeBeneficiario: config.NomeFantasia,
                        cidade:           "BRASIL",
                        txid:             $"ERP{DateTime.Now:yyyyMMddHHmmss}");

                    bool? resultado = pixView.ShowDialog();
                }
            }

            var fiscalCalculator = ERP.WPF.App.Services.GetRequiredService<ERP.Domain.Services.Fiscal.IFiscalCalculator>();
            decimal impostosAproximados = fiscalCalculator.CalcularTributosAproximados(this.TotalVenda, 13.45m);
            string msgFiscal = $"\nTrib. Aprox. R$: {impostosAproximados:N2} (Lei 12.741/12)";
            string observacaoCompleta = (this.EntregarNoEndereco && !string.IsNullOrWhiteSpace(this.EnderecoEntrega) ? this.EnderecoEntrega : "") + msgFiscal;
            // Observação geral é passada separadamente para o recibo (aparece antes dos itens)

            var dto = new CreateSaleDto
            {
                CustomerId = SelectedCustomer?.Id,
                SellerName = SelectedVendedor?.Name,
                UsuarioId = ERP.WPF.State.AppSession.UserId, 
                Notes = observacaoCompleta,
                DiscountAmount = this.Desconto + this.DescontoFidelidade,
                Payments = Pagamentos.Select(p => new CreateSalePaymentDto { PaymentMethod = p.Forma, Amount = p.Valor }).ToList(), 
                Items = ItensCarrinho.Select(i => new CreateSaleItemDto { ProductId = i.ProductId, Quantity = i.Quantity, UnitPrice = i.UnitPrice, DiscountPercent = 0, FatorConversao = i.FatorConversao, TotalItem = i.Total }).ToList()
            };

            var vendaSalva = await _saleService.CreateAsync(dto);
            await SalvarNoCaixaEContasAReceberAsync(vendaSalva.Id);

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
            else if (tipoEmissao == "NFCE")
            {
                await EmitirNfceFocusAsync(vendaSalva.Id);
            }
            else if (tipoEmissao == "NFE")
            {
                await EmitirNfeA4FocusAsync(vendaSalva.Id);
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
    private async Task EmitirNfceFocusAsync(Guid vendaId)
    {
        var config = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
        var nfceService = ERP.WPF.App.Services.GetRequiredService<INfceEmissionService>();

        string? cpfCnpjLimpo = null;
        if (!string.IsNullOrWhiteSpace(SelectedCustomer?.Document))
            cpfCnpjLimpo = new string(SelectedCustomer.Document.Where(char.IsDigit).ToArray());

        var nfceRequest = new FocusNfceRequest
        {
            DataEmissao = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"), 
            CpfCnpj = cpfCnpjLimpo,
            Nome = SelectedCustomer?.Name,
            Itens = ItensCarrinho.Select((item, index) => new FocusItemRequest
            {
                NumeroItem = (index + 1).ToString(),
                CodigoProduto = item.ProductId.ToString().Substring(0, 6), 
                Descricao = item.ProductName,
                QuantidadeComercial = item.Quantity.ToString("F2", CultureInfo.InvariantCulture),
                ValorUnitarioComercial = item.UnitPrice.ToString("F2", CultureInfo.InvariantCulture),
                ValorBruto = item.Total.ToString("F2", CultureInfo.InvariantCulture),
                CodigoNcm = string.IsNullOrWhiteSpace(item.Ncm) ? "00000000" : item.Ncm.Replace(".", "").Replace("-", "").Trim(),
                IcmsSituacaoTributaria = string.IsNullOrWhiteSpace(item.Csosn) ? "102" : item.Csosn.Split('-')[0].Trim(),
                IcmsOrigem = string.IsNullOrWhiteSpace(item.IcmsOrigem) ? "0" : item.IcmsOrigem,
                Cfop = string.IsNullOrWhiteSpace(item.Cfop) ? "5102" : item.Cfop.Replace(".", "").Trim(),
                PisSituacaoTributaria = "99",
                CofinsSituacaoTributaria = "99"
            }).ToList(),
            Pagamentos = Pagamentos.Select(p => new FocusPagamentoRequest
            {
                FormaPagamento = TraduzirFormaPagamentoParaSefaz(p.Forma),
                ValorPagamento = p.Forma == PaymentMethod.Dinheiro 
                    ? (p.Valor - Troco).ToString("F2", CultureInfo.InvariantCulture) 
                    : p.Valor.ToString("F2", CultureInfo.InvariantCulture)
            }).ToList()
        };

        var (sucesso, mensagem, urlDanfe) = await nfceService.EmitirNfceAsync(vendaId.ToString(), nfceRequest, config.TokenFocusNfe, config.UsarAmbienteProducao);

        if (sucesso && !string.IsNullOrWhiteSpace(urlDanfe))
        {
            MessageBox.Show($"✅ {mensagem}", "Sefaz - Sucesso!", MessageBoxButton.OK, MessageBoxImage.Information);
            try
            {
                string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";
                await _saleService.AtualizarDadosNfceAsync(vendaId, urlDanfe, "Autorizada", ambienteSefaz, vendaId.ToString());
            } catch { }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = urlDanfe, UseShellExecute = true });
        }
        else
        {
            if (mensagem.Contains("Erro de Comunicação") && !mensagem.Contains("UnprocessableEntity") && !mensagem.Contains("erro_validacao_schema"))
            {
                try
                {
                    var contingencyService = ERP.WPF.App.Services.GetRequiredService<INfeContingencyService>();
                    string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(nfceRequest);
                    
                    await contingencyService.RegistrarNotaPendenteAsync(vendaId, "NFCE", jsonPayload);

                    string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";
                    await _saleService.AtualizarDadosNfceAsync(vendaId, "", "Contingência", ambienteSefaz, vendaId.ToString());

                    MessageBox.Show("📡 Venda salva em MODO CONTINGÊNCIA!\n\nA internet parece estar instável. A nota será enviada para a SEFAZ automaticamente assim que a conexão voltar.\n\nImprimindo recibo provisório...", "Modo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                    
                    var fiscalCalculator = ERP.WPF.App.Services.GetRequiredService<ERP.Domain.Services.Fiscal.IFiscalCalculator>();
                    decimal impostosAproximados = fiscalCalculator.CalcularTributosAproximados(this.TotalVenda, 13.45m);
                    string msgFiscal = $"\nTrib. Aprox. R$: {impostosAproximados:N2} (Lei 12.741/12)";
                    string observacaoCompleta = (this.EntregarNoEndereco && !string.IsNullOrWhiteSpace(this.EnderecoEntrega) ? this.EnderecoEntrega : "") + msgFiscal;
            // Observação geral é passada separadamente para o recibo (aparece antes dos itens)
                    
                    ImprimirReciboInterno(vendaId, observacaoCompleta);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Falha catastrófica ao tentar salvar nota em contingência: {ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show($"❌ Falha na emissão da NFC-e:\n{mensagem}", "Rejeição Sefaz", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task EmitirNfeA4FocusAsync(Guid vendaId)
    {
        var config = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
        var nfeService = ERP.WPF.App.Services.GetRequiredService<INfeEmissionService>();

        string? cpfCnpjLimpo = null;
        if (!string.IsNullOrWhiteSpace(SelectedCustomer?.Document)) cpfCnpjLimpo = new string(SelectedCustomer.Document.Where(char.IsDigit).ToArray());
        string? cepLimpo = null;
        if (!string.IsNullOrWhiteSpace(SelectedCustomer?.ZipCode)) cepLimpo = new string(SelectedCustomer.ZipCode.Where(char.IsDigit).ToArray());
        string? ieLimpa = null;
        if (!string.IsNullOrWhiteSpace(SelectedCustomer?.Ie)) ieLimpa = new string(SelectedCustomer.Ie.Where(char.IsDigit).ToArray());
        else if (cpfCnpjLimpo?.Length > 11) ieLimpa = "ISENTO"; 

        var nfeRequest = new FocusNfceRequest
        {
            DataEmissao = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"), 
            TipoDocumento = "1", 
            CpfCnpj = cpfCnpjLimpo,
            Nome = SelectedCustomer?.Name,
            LogradouroDestinatario = string.IsNullOrWhiteSpace(SelectedCustomer?.Street) ? "Nao Informado" : SelectedCustomer.Street,
            NumeroDestinatario = string.IsNullOrWhiteSpace(SelectedCustomer?.Number) ? "S/N" : SelectedCustomer.Number,
            BairroDestinatario = string.IsNullOrWhiteSpace(SelectedCustomer?.Neighborhood) ? "Centro" : SelectedCustomer.Neighborhood,
            MunicipioDestinatario = string.IsNullOrWhiteSpace(SelectedCustomer?.City) ? "Curitiba" : SelectedCustomer.City,
            UfDestinatario = string.IsNullOrWhiteSpace(SelectedCustomer?.State) ? "PR" : SelectedCustomer.State,
            CepDestinatario = string.IsNullOrWhiteSpace(cepLimpo) ? "00000000" : cepLimpo,
            IeDestinatario = ieLimpa,
            Itens = ItensCarrinho.Select((item, index) => new FocusItemRequest
            {
                NumeroItem = (index + 1).ToString(),
                CodigoProduto = item.ProductId.ToString().Substring(0, 6), 
                Descricao = item.ProductName,
                QuantidadeComercial = item.Quantity.ToString("F2", CultureInfo.InvariantCulture),
                ValorUnitarioComercial = item.UnitPrice.ToString("F2", CultureInfo.InvariantCulture),
                ValorBruto = item.Total.ToString("F2", CultureInfo.InvariantCulture),
                CodigoNcm = string.IsNullOrWhiteSpace(item.Ncm) ? "00000000" : item.Ncm.Replace(".", ""), 
                IcmsSituacaoTributaria = string.IsNullOrWhiteSpace(item.Csosn) ? "102" : item.Csosn.Split('-')[0].Trim(),
                IcmsOrigem = string.IsNullOrWhiteSpace(item.IcmsOrigem) ? "0" : item.IcmsOrigem,
                Cfop = string.IsNullOrWhiteSpace(item.Cfop) ? "5102" : item.Cfop,
                PisSituacaoTributaria = "99",
                CofinsSituacaoTributaria = "99"
            }).ToList(),
            Pagamentos = Pagamentos.Select(p => new FocusPagamentoRequest
            {
                FormaPagamento = TraduzirFormaPagamentoParaSefaz(p.Forma),
                ValorPagamento = p.Forma == PaymentMethod.Dinheiro 
                    ? (p.Valor - Troco).ToString("F2", CultureInfo.InvariantCulture) 
                    : p.Valor.ToString("F2", CultureInfo.InvariantCulture)
            }).ToList()
        };

        var (sucesso, mensagem, urlDanfe) = await nfeService.EmitirNfeA4Async(vendaId.ToString(), nfeRequest, config.TokenFocusNfe, config.UsarAmbienteProducao);

        if (sucesso)
        {
            if (!string.IsNullOrWhiteSpace(urlDanfe))
            {
                MessageBox.Show($"✅ {mensagem}", "Sefaz - Sucesso!", MessageBoxButton.OK, MessageBoxImage.Information);
                try
                {
                    string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";
                    await _saleService.AtualizarDadosNfceAsync(vendaId, urlDanfe, "Autorizada", ambienteSefaz, vendaId.ToString());
                } catch { }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = urlDanfe, UseShellExecute = true });
            }
            else
            {
                MessageBox.Show($"⏳ {mensagem}\n\nSua NF-e está na fila da SEFAZ para ser aprovada.\nAssim que liberar, o PDF aparecerá na sua tela de 'Notas Fiscais' (F10).", 
                                "Aguardando Sefaz", MessageBoxButton.OK, MessageBoxImage.Information);
                try
                {
                    string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";
                    await _saleService.AtualizarDadosNfceAsync(vendaId, "", "Processando", ambienteSefaz, vendaId.ToString());
                } catch { }
            }
        }
        else
        {
            if (mensagem.Contains("Erro de Comunicação") && !mensagem.Contains("UnprocessableEntity") && !mensagem.Contains("erro_validacao_schema"))
            {
                try
                {
                    var contingencyService = ERP.WPF.App.Services.GetRequiredService<INfeContingencyService>();
                    string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(nfeRequest);
                    await contingencyService.RegistrarNotaPendenteAsync(vendaId, "NFE", jsonPayload);
                    
                    string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";
                    await _saleService.AtualizarDadosNfceAsync(vendaId, "", "Contingência", ambienteSefaz, vendaId.ToString());

                    MessageBox.Show("📡 Venda salva em MODO CONTINGÊNCIA!\n\nA internet está instável. A NF-e será transmitida automaticamente depois.", "Modo Offline", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch { }
            }
            else
            {
                MessageBox.Show($"❌ Falha na emissão da NF-e (A4):\n{mensagem}", "Rejeição Sefaz", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private string TraduzirFormaPagamentoParaSefaz(PaymentMethod metodo)
    {
        return metodo switch
        {
            PaymentMethod.Dinheiro => "01",
            PaymentMethod.CartaoCredito => "03",
            PaymentMethod.CartaoDebito => "04",
            PaymentMethod.APrazo => "05", 
            PaymentMethod.Pix => "17",
            _ => "99" 
        };
    }

    // ==========================================================
    // FUNÇÕES AUXILIARES (Para o código ficar limpo)
    // ==========================================================
    private async Task SalvarNoCaixaEContasAReceberAsync(Guid vendaId)
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
                Pagamentos.Select(p => (p.Forma, p.Valor)));
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
}