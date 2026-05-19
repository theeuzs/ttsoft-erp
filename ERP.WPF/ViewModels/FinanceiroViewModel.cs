using ERP.Application.Interfaces;
using ERP.WPF.Reports;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

// ── 1. CLASSE DE AGRUPAMENTO (O "Pacote" do Cliente) ────────────────────────
public class ResumoClienteDevedor : BaseViewModel
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    private decimal _totalPendente;
    public decimal TotalPendente { get => _totalPendente; set => SetProperty(ref _totalPendente, value); }

    private int _qtdContas;
    public int QtdContas { get => _qtdContas; set => SetProperty(ref _qtdContas, value); }

    // Guarda as contas individuais do cliente para mostrar na janelinha
    public ObservableCollection<ContaReceber> Contas { get; } = new();
}

public class FinanceiroViewModel : BaseViewModel
{
    // ── Resumo do Topo ───────────────────────────────────────────────────────
    private decimal _totalPendente;
    public decimal TotalPendente { get => _totalPendente; set => SetProperty(ref _totalPendente, value); }

    private decimal _totalVencido;
    public decimal TotalVencido { get => _totalVencido; set => SetProperty(ref _totalVencido, value); }

    private int _qtdClientes;
    public int QtdClientes { get => _qtdClientes; set => SetProperty(ref _qtdClientes, value); }

    // ── Listas ───────────────────────────────────────────────────────────────
    // 👇 A lista principal agora é a de Clientes Agrupados!
    public ObservableCollection<ResumoClienteDevedor> ClientesDevedores { get; } = new();

    // ── Aba selecionada ──────────────────────────────────────────────────────
    private int _abaSelecionada = 0;
    public int AbaSelecionada
    {
        get => _abaSelecionada;
        set => SetProperty(ref _abaSelecionada, value);
    }

    // ── Conta selecionada para baixa (Usada dentro da janelinha) ─────────────
    private ContaReceber? _selectedConta;
    public ContaReceber? SelectedConta
    {
        get => _selectedConta;
        set
        {
            SetProperty(ref _selectedConta, value);
            if (value != null)
                ValorRecebido = value.ValorTotal - value.ValorRecebido; // saldo restante
            
            OnPropertyChanged(nameof(PainelVisivel));
            OnPropertyChanged(nameof(SaldoRestante));
            OnPropertyChanged(nameof(Troco));
        }
    }

    public Visibility PainelVisivel => SelectedConta != null ? Visibility.Visible : Visibility.Collapsed;

    public decimal SaldoRestante => SelectedConta != null
        ? SelectedConta.ValorTotal - SelectedConta.ValorRecebido : 0;

    public System.Collections.Generic.IEnumerable<PaymentMethod> FormasPagamento =>
        System.Enum.GetValues<PaymentMethod>()
              .Where(p => p != PaymentMethod.APrazo && p != PaymentMethod.Haver);

    private PaymentMethod _formaPagamentoSelecionada = PaymentMethod.Dinheiro;
    public PaymentMethod FormaPagamentoSelecionada
    {
        get => _formaPagamentoSelecionada;
        set => SetProperty(ref _formaPagamentoSelecionada, value);
    }

    private decimal _valorRecebido;
    public decimal ValorRecebido
    {
        get => _valorRecebido;
        set { SetProperty(ref _valorRecebido, value); OnPropertyChanged(nameof(Troco)); }
    }

    public decimal Troco => SelectedConta != null && ValorRecebido > SaldoRestante
        ? ValorRecebido - SaldoRestante : 0;

    // ── Comandos ─────────────────────────────────────────────────────────────
    public ICommand CarregarCommand               { get; }
    public ICommand ImprimirCarneCommand          { get; }  // Sprint N
    public ICommand EnviarCobrancaWhatsAppCommand { get; }  // Sprint P
    public ICommand PrepararRecebimentoCommand { get; }
    public ICommand CancelarRecebimentoCommand { get; }
    public ICommand ConfirmarBaixaCommand      { get; }
    public ICommand BaixaTotalCommand          { get; }
    public ICommand AbrirDetalhesCommand       { get; } // Novo comando da janelinha
    public ICommand VerReciboVendaCommand      { get; } // Botão de ver a compra original

    public FinanceiroViewModel()
    {
        CarregarCommand               = new RelayCommand(async _ => await CarregarContasAsync());
        ImprimirCarneCommand          = new AsyncRelayCommand(async p => await ImprimirCarneAsync(p as ResumoClienteDevedor));
        EnviarCobrancaWhatsAppCommand = new AsyncRelayCommand(async p => await EnviarCobrancaWhatsAppAsync(p as ResumoClienteDevedor));
        PrepararRecebimentoCommand = new RelayCommand(p => { if (p is ContaReceber c) SelectedConta = c; });
        CancelarRecebimentoCommand = new RelayCommand(_ => SelectedConta = null);
        ConfirmarBaixaCommand      = new RelayCommand(async _ => await ConfirmarBaixaAsync());
        BaixaTotalCommand          = new RelayCommand(async _ => await BaixaTotalAsync());
        
        VerReciboVendaCommand      = new RelayCommand(async c => await AbrirReciboVendaAsync(c as ContaReceber));
        
        // Abre a janelinha modal de detalhes do cliente
        AbrirDetalhesCommand       = new RelayCommand(c => AbrirDetalhesCliente(c as ResumoClienteDevedor));

        _ = CarregarContasAsync();
    }

    // ── Abre o Módulo do Cliente ─────────────────────────────────────────────
    private void AbrirDetalhesCliente(ResumoClienteDevedor? resumo)
    {
        if (resumo == null) return;
        SelectedConta = null; // Reseta o painel de pagamento

        var view = new ERP.WPF.Views.ContasClienteView(this, resumo)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        view.ShowDialog();
    }

    // ── Carregamento & Agrupamento ───────────────────────────────────────────
    public async Task CarregarContasAsync()
    {
        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContaReceberService>();

            var pendentes = await service.GetPendentesAsync();
            var resumo    = await service.GetResumoAsync();

            // 👇 A MÁGICA DO AGRUPAMENTO POR CLIENTE 👇
            var agrupado = pendentes.GroupBy(c => c.CustomerId).Select(g =>
            {
                var r = new ResumoClienteDevedor
                {
                    CustomerId = g.Key,
                    CustomerName = g.First().Customer?.Name ?? "Cliente Não Identificado",
                    TotalPendente = g.Sum(x => x.ValorTotal - x.ValorRecebido),
                    QtdContas = g.Count()
                };
                foreach (var c in g.OrderBy(x => x.DataVencimento)) r.Contas.Add(c);
                return r;
            }).ToList();

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                ClientesDevedores.Clear();
                foreach (var c in agrupado) ClientesDevedores.Add(c);

                TotalPendente = resumo.TotalPendente;
                TotalVencido  = resumo.TotalVencido;
                QtdClientes   = resumo.QtdClientes;
            });
        }
        catch (Exception ex) { MessageBox.Show($"Erro ao carregar contas: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    // ── Baixa parcial ────────────────────────────────────────────────────────
    private async Task ConfirmarBaixaAsync()
    {
        if (SelectedConta == null || ValorRecebido <= 0) return;

        decimal valorAplicar = Math.Min(ValorRecebido, SaldoRestante);
        var contaPaga = SelectedConta; 

        try
        {
            // Registra no caixa
            var caixaService = App.Services.GetRequiredService<ICaixaService>();
            await caixaService.RegistrarMovimentoAsync(
                ERP.WPF.State.AppSession.UserId,
                valorAplicar,
                $"RECEBIMENTO FIADO - {contaPaga.Customer?.Name ?? "Cliente"}",
                FormaPagamentoSelecionada,
                TipoMovimentoCaixa.RecebimentoConta);

            // Dá baixa no banco de dados
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContaReceberService>();
            await service.DarBaixaParcialAsync(contaPaga.Id, valorAplicar);

            ERP.WPF.ViewModels.PdvViewModel.NotificacaoCaixaAlterado?.Invoke();

            bool pago = valorAplicar >= SaldoRestante;
            MessageBox.Show(
                pago
                    ? $"✅ Pagamento total recebido!\n\nTroco: R$ {Troco:N2}"
                    : $"✅ Pagamento parcial de R$ {valorAplicar:N2} registrado!\n\nSaldo restante: R$ {SaldoRestante - valorAplicar:N2}",
                "Recebimento", MessageBoxButton.OK, MessageBoxImage.Information);

            // 👇 IMPRIME O COMPROVANTE (DISPARO AUTOMÁTICO) 👇
            ImprimirComprovantePagamento(contaPaga, valorAplicar, Math.Max(0, SaldoRestante - valorAplicar));

            // 👇 MÁGICA VISUAL ATUALIZADA (Para a Janelinha) 👇
            var resumoCliente = ClientesDevedores.FirstOrDefault(r => r.CustomerId == contaPaga.CustomerId);
            
            if (pago)
            {
                if (resumoCliente != null)
                {
                    resumoCliente.Contas.Remove(contaPaga);
                    resumoCliente.TotalPendente -= valorAplicar;
                    resumoCliente.QtdContas--;

                    if (resumoCliente.Contas.Count == 0) ClientesDevedores.Remove(resumoCliente);
                }
            }
            else
            {
                // Se foi parcial, apenas desconta do total do cliente na tela
                if (resumoCliente != null) resumoCliente.TotalPendente -= valorAplicar;
                contaPaga.ValorRecebido += valorAplicar; // Atualiza a linha da tela
            }

            TotalPendente -= valorAplicar;
            SelectedConta = null;
        }
        catch (Exception ex) { MessageBox.Show($"Erro ao registrar: {ex.Message}"); }
    }

    // ── Baixa total rápida ───────────────────────────────────────────────────
    private async Task BaixaTotalAsync()
    {
        if (SelectedConta == null) return;

        decimal saldo = SaldoRestante;
        var confirm = MessageBox.Show(
            $"Confirmar recebimento total de R$ {saldo:N2} de {SelectedConta.Customer?.Name}?",
            "Baixa Total", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        var contaPaga = SelectedConta; 

        try
        {
            var caixaService = App.Services.GetRequiredService<ICaixaService>();
            await caixaService.RegistrarMovimentoAsync(
                ERP.WPF.State.AppSession.UserId,
                saldo,
                $"RECEBIMENTO FIADO - {contaPaga.Customer?.Name ?? "Cliente"}",
                FormaPagamentoSelecionada,
                TipoMovimentoCaixa.RecebimentoConta);

            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContaReceberService>();
            await service.DarBaixaTotalAsync(contaPaga.Id);

            ERP.WPF.ViewModels.PdvViewModel.NotificacaoCaixaAlterado?.Invoke();

            MessageBox.Show("✅ Conta liquidada com sucesso!", "Sucesso",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // 👇 IMPRIME O COMPROVANTE (DISPARO AUTOMÁTICO) 👇
            ImprimirComprovantePagamento(contaPaga, saldo, 0);

            // 👇 MÁGICA VISUAL ATUALIZADA (Para a Janelinha) 👇
            var resumoCliente = ClientesDevedores.FirstOrDefault(r => r.CustomerId == contaPaga.CustomerId);
            if (resumoCliente != null)
            {
                resumoCliente.Contas.Remove(contaPaga);
                resumoCliente.TotalPendente -= saldo;
                resumoCliente.QtdContas--;

                if (resumoCliente.Contas.Count == 0) ClientesDevedores.Remove(resumoCliente);
            }

            TotalPendente -= saldo;
            SelectedConta = null;
        }
        catch (Exception ex) { MessageBox.Show($"Erro: {ex.Message}"); }
    }

    // ── 1. VISUALIZAR A COMPRA ORIGINAL ──────────────────────────────────────
    private async Task AbrirReciboVendaAsync(ContaReceber? conta)
    {
        if (conta == null || !conta.SaleId.HasValue)
        {
            MessageBox.Show("Esta conta não está vinculada a uma venda automática do PDV (pode ter sido um lançamento manual).", 
                            "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var saleService = scope.ServiceProvider.GetRequiredService<ISaleService>();
            var detalhesVenda = await saleService.GetDetailAsync(conta.SaleId.Value);

            if (detalhesVenda == null) return;

            // Transforma os itens da venda pro formato do ReciboPrinter
            var itensParaImprimir = detalhesVenda.Items.Select(item => new ViewModels.CartItem
            {
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                NormalUnitPrice = item.UnitPrice,
                DiscountPercent = item.DiscountPercent
            }).ToList();

            var pagamentosParaImprimir = detalhesVenda.Payments.Select(pag => (pag.PaymentMethod, pag.Amount)).ToList();

            // Usa o seu Printer já existente para mostrar a tela!
            ERP.WPF.Helpers.ReciboPrinter.Visualizar(
                detalhesVenda.Id, itensParaImprimir, detalhesVenda.Total, detalhesVenda.DiscountAmount,
                detalhesVenda.CustomerName ?? "Consumidor Final", detalhesVenda.SellerName ?? "Balcão",
                pagamentosParaImprimir, 0, detalhesVenda.Observation ?? "",
                dataVenda: detalhesVenda.SaleDate, numeroVenda: detalhesVenda.SaleNumber);
        }
        catch (Exception ex) { MessageBox.Show($"Erro ao puxar recibo: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { IsBusy = false; }
    }

    // ── 2. IMPRIMIR COMPROVANTE DE PAGAMENTO DE CONTA ────────────────────────
    private void ImprimirComprovantePagamento(ContaReceber conta, decimal valorPago, decimal saldoRestante)
    {
        var confirm = MessageBox.Show("Deseja imprimir o comprovante de pagamento para o cliente?", 
                                      "Imprimir Comprovante", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            // Cria um documento no formato de Bobina Térmica (Aprox 80mm / 300px)
            var doc = new System.Windows.Documents.FlowDocument
            {
                PagePadding = new Thickness(10),
                PageWidth = 300,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"), // Fonte de impressora
                FontSize = 12
            };

            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("VILA VERDE MATERIAIS DE CONSTRUCAO")) { TextAlignment = TextAlignment.Center, FontWeight = FontWeights.Bold });
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("COMPROVANTE DE PAGAMENTO")) { TextAlignment = TextAlignment.Center });
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("-----------------------------------------")) { TextAlignment = TextAlignment.Center });

            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"DATA: {DateTime.Now:dd/MM/yyyy HH:mm}")));
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"CLIENTE: {conta.Customer?.Name}")));
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"REF: {conta.Descricao}")));
            
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("-----------------------------------------")) { TextAlignment = TextAlignment.Center });
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"VALOR PAGO: R$ {valorPago:N2}")) { FontWeight = FontWeights.Bold, FontSize = 14 });
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"SALDO RESTANTE: R$ {saldoRestante:N2}")));
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("-----------------------------------------")) { TextAlignment = TextAlignment.Center });
            
            doc.Blocks.Add(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Obrigado pela preferencia!")) { TextAlignment = TextAlignment.Center });

            var pd = new System.Windows.Controls.PrintDialog();
            if (pd.ShowDialog() == true) // Abre a telinha pra escolher a impressora térmica
            {
                pd.PrintDocument(((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator, "Comprovante Pagamento");
            }
        }
        catch (Exception ex) { MessageBox.Show($"Erro na impressora: {ex.Message}"); }
    }
    // ── Sprint N: Imprimir carnê de parcelamento ──────────────────────────────
    private async Task ImprimirCarneAsync(ResumoClienteDevedor? resumo)
    {
        if (resumo == null || !resumo.Contas.Any()) return;
        try
        {
            IsBusy = true;
            var config = ERP.WPF.Helpers.ConfiguracaoService.Carregar();

            var parcelas = resumo.Contas
                .Select(conta => new ERP.Application.DTOs.ParcelaDto
                {
                    Id             = conta.Id,
                    NumeroParcela  = conta.NumeroParcela,
                    TotalParcelas  = conta.TotalParcelas,
                    ValorTotal     = conta.ValorTotal,
                    ValorRecebido  = conta.ValorRecebido,
                    DataVencimento = conta.DataVencimento,
                    DataPagamento  = conta.DataPagamento,
                    Status         = conta.Status,
                    FormaPagamento = conta.FormaPagamento,
                    ParcelamentoId = conta.ParcelamentoId
                })
                .OrderBy(p => p.DataVencimento)
                .ToList();

            var doc = new CarnePdfReport(
                config,
                nomeCliente:     resumo.CustomerName,
                telefoneCliente: null,
                descricao:       resumo.Contas.FirstOrDefault()?.Descricao ?? "Crediário",
                parcelas:        parcelas);

            PdfReportBase.SalvarEAbrir(doc, $"Carne_{resumo.CustomerName.Replace(" ", "_")}");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Erro ao gerar carnê:\n{ex.Message}", "Erro",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    // ── Sprint P: Cobrança via WhatsApp Web ───────────────────────────────────
    private async Task EnviarCobrancaWhatsAppAsync(ResumoClienteDevedor? resumo)
    {
        if (resumo == null) return;
        try
        {
            using var scope = ERP.WPF.App.Services.CreateScope();
            var customerService = scope.ServiceProvider
                .GetRequiredService<ERP.Application.Interfaces.ICustomerService>();
            var cliente = await customerService.GetByIdAsync(resumo.CustomerId);

            var vencidas = resumo.Contas
                .Where(c => c.DataVencimento.Date < DateTime.Today && c.Status == "Pendente")
                .OrderBy(c => c.DataVencimento)
                .ToList();

            if (!vencidas.Any())
            {
                System.Windows.MessageBox.Show("Sem parcelas vencidas para este cliente.", "Aviso",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var total     = vencidas.Sum(cc => cc.ValorTotal - cc.ValorRecebido);
            var venceu    = vencidas.Min(cc => cc.DataVencimento);
            var nomeLoja  = ERP.WPF.Helpers.ConfiguracaoService.Carregar().NomeFantasia;

            var texto = $"Olá, {resumo.CustomerName}! 👋\n\n" +
                        $"Passando para informar que você possui {vencidas.Count} parcela(s) " +
                        $"em aberto na *{nomeLoja}*:\n\n" +
                        $"💰 *Total: {total:C}*\n" +
                        $"📅 Vencida desde: {venceu:dd/MM/yyyy}\n\n" +
                        "Acesse nossa loja para regularizar ou ligue para nós. 😊";

            var enc = Uri.EscapeDataString(texto);
            string url;

            if (!string.IsNullOrWhiteSpace(cliente?.Phone))
            {
                var numero = new string(cliente.Phone.Where(char.IsDigit).ToArray());
                if (!numero.StartsWith("55")) numero = "55" + numero;
                url = $"https://wa.me/{numero}?text={enc}";
            }
            else
                url = $"https://web.whatsapp.com/send?text={enc}";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Erro ao abrir WhatsApp Web:\n{ex.Message}", "Erro",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

}