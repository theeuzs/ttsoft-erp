using ERP.Application.Interfaces;
using ERP.WPF.Reports;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualBasic;
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

/// <summary>
/// Uma linha da grid "Contas em Aberto do Cliente" com estado de seleção —
/// wrapper só do WPF, não polui a entidade ContaReceber (compartilhada com
/// EF Core/backend) com estado de UI. Saldo aqui já considera ValorDesconto
/// (o bug antigo da tela mostrava ValorTotal-ValorRecebido de uma linha
/// "global" repetida em toda linha — Saldo por instância corrige os dois
/// problemas de uma vez).
/// </summary>
public class ContaReceberSelecionavel : BaseViewModel
{
    public ContaReceber Conta { get; }

    public Guid     Id             => Conta.Id;
    public DateTime DataEmissao    => Conta.DataEmissao;
    public string   Descricao      => Conta.Descricao;
    public DateTime DataVencimento => Conta.DataVencimento;
    public decimal  ValorTotal     => Conta.ValorTotal;
    public decimal  ValorRecebido  => Conta.ValorRecebido;
    public decimal  Saldo          => Conta.ValorTotal - Conta.ValorRecebido - Conta.ValorDesconto;

    private bool _isSelecionada;
    public bool IsSelecionada { get => _isSelecionada; set => SetProperty(ref _isSelecionada, value); }

    public ContaReceberSelecionavel(ContaReceber conta) => Conta = conta;
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

    // ── Contas selecionáveis da janelinha (populadas ao abrir o cliente) ─────
    public ObservableCollection<ContaReceberSelecionavel> ContasSelecionaveis { get; } = new();

    private bool? _selecionarTodas = false;
    public bool? SelecionarTodas
    {
        get => _selecionarTodas;
        set
        {
            SetProperty(ref _selecionarTodas, value);
            if (value.HasValue)
                foreach (var c in ContasSelecionaveis) c.IsSelecionada = value.Value;
        }
    }

    private decimal _totalSelecionado;
    public decimal TotalSelecionado { get => _totalSelecionado; private set => SetProperty(ref _totalSelecionado, value); }

    private decimal _valorDesconto;
    public decimal ValorDesconto
    {
        get => _valorDesconto;
        set { SetProperty(ref _valorDesconto, value); RecalcularValorAPagar(); }
    }

    private decimal _valorAPagar;
    public decimal ValorAPagar { get => _valorAPagar; set => SetProperty(ref _valorAPagar, value); }

    public System.Collections.Generic.IEnumerable<PaymentMethod> FormasPagamento =>
        System.Enum.GetValues<PaymentMethod>()
              .Where(p => p != PaymentMethod.APrazo && p != PaymentMethod.Haver);

    private PaymentMethod _formaPagamentoSelecionada = PaymentMethod.Dinheiro;
    public PaymentMethod FormaPagamentoSelecionada
    {
        get => _formaPagamentoSelecionada;
        set => SetProperty(ref _formaPagamentoSelecionada, value);
    }

    // ── Comandos ─────────────────────────────────────────────────────────────
    public ICommand CarregarCommand               { get; }
    public ICommand ImprimirCarneCommand          { get; }  // Sprint N
    public ICommand EnviarCobrancaWhatsAppCommand { get; }  // Sprint P
    public ICommand AbrirDetalhesCommand       { get; } // Novo comando da janelinha
    public ICommand VerReciboVendaCommand      { get; } // Botão de ver a compra original
    public ICommand ReceberSelecionadasCommand { get; }
    public ICommand CancelarContaCommand       { get; }
    public ICommand VerHistoricoContaCommand   { get; }

    public FinanceiroViewModel()
    {
        CarregarCommand               = new RelayCommand(async _ => await CarregarContasAsync());
        ImprimirCarneCommand          = new AsyncRelayCommand(async p => await ImprimirCarneAsync(p as ResumoClienteDevedor));
        EnviarCobrancaWhatsAppCommand = new AsyncRelayCommand(async p => await EnviarCobrancaWhatsAppAsync(p as ResumoClienteDevedor));
        VerReciboVendaCommand      = new RelayCommand(async c => await AbrirReciboVendaAsync(c as ContaReceber));
        ReceberSelecionadasCommand = new AsyncRelayCommand(async _ => await ReceberSelecionadasAsync());
        CancelarContaCommand       = new AsyncRelayCommand(async p => await CancelarContaAsync(p as ContaReceberSelecionavel));
        VerHistoricoContaCommand   = new RelayCommand(p => AbrirHistoricoConta(p as ContaReceberSelecionavel));

        // Abre a janelinha modal de detalhes do cliente
        AbrirDetalhesCommand       = new RelayCommand(c => AbrirDetalhesCliente(c as ResumoClienteDevedor));

        _ = CarregarContasAsync();
    }

    private void RecalcularValorAPagar()
        => ValorAPagar = Math.Max(0, TotalSelecionado - ValorDesconto);

    private void AtualizarTotaisSelecao()
    {
        TotalSelecionado = ContasSelecionaveis.Where(c => c.IsSelecionada).Sum(c => c.Saldo);
        RecalcularValorAPagar();
    }

    private void AbrirHistoricoConta(ContaReceberSelecionavel? item)
    {
        if (item == null) return;
        var view = new ERP.WPF.Views.ContaHistoricoView(item.Id, $"Histórico: {item.Descricao}")
        {
            Owner = System.Windows.Application.Current.Windows
                .OfType<ERP.WPF.Views.ContasClienteView>().FirstOrDefault()
                ?? System.Windows.Application.Current.MainWindow
        };
        view.ShowDialog();
    }

    // ── Abre o Módulo do Cliente ─────────────────────────────────────────────
    private void AbrirDetalhesCliente(ResumoClienteDevedor? resumo)
    {
        if (resumo == null) return;

        ContasSelecionaveis.Clear();
        SelecionarTodas  = false;
        ValorDesconto    = 0;
        ValorAPagar      = 0;
        foreach (var conta in resumo.Contas)
        {
            var item = new ContaReceberSelecionavel(conta);
            item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ContaReceberSelecionavel.IsSelecionada)) AtualizarTotaisSelecao(); };
            ContasSelecionaveis.Add(item);
        }

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
                    TotalPendente = g.Sum(x => x.ValorTotal - x.ValorRecebido - x.ValorDesconto),
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

    // ── Recebimento em lote (várias contas selecionadas de uma vez) ──────────
    private async Task ReceberSelecionadasAsync()
    {
        var selecionadas = ContasSelecionaveis.Where(c => c.IsSelecionada).ToList();
        if (selecionadas.Count == 0)
        {
            MessageBox.Show("Selecione ao menos uma conta.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (ValorAPagar <= 0)
        {
            MessageBox.Show("Informe um valor a pagar maior que zero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var clienteId   = selecionadas.First().Conta.CustomerId;
        var nomeCliente = selecionadas.First().Conta.Customer?.Name ?? "Cliente";

        try
        {
            var caixaService = App.Services.GetRequiredService<ICaixaService>();
            await caixaService.RegistrarMovimentoAsync(
                ERP.WPF.State.AppSession.UserId,
                ValorAPagar,
                $"RECEBIMENTO FIADO (lote de {selecionadas.Count}) - {nomeCliente}",
                FormaPagamentoSelecionada,
                TipoMovimentoCaixa.RecebimentoConta);

            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContaReceberService>();
            await service.DarBaixaEmLoteAsync(
                selecionadas.Select(c => c.Id), ValorAPagar, ValorDesconto, FormaPagamentoSelecionada.ToString());

            ERP.WPF.ViewModels.PdvViewModel.NotificacaoCaixaAlterado?.Invoke();

            MessageBox.Show(
                $"✅ {selecionadas.Count} conta(s) processada(s)!\n\nValor pago: R$ {ValorAPagar:N2}" +
                (ValorDesconto > 0 ? $"\nDesconto: R$ {ValorDesconto:N2}" : ""),
                "Recebimento", MessageBoxButton.OK, MessageBoxImage.Information);

            await RecarregarContasDoClienteAsync(clienteId);
            await CarregarContasAsync(); // atualiza os totais da lista principal também
        }
        catch (Exception ex) { MessageBox.Show($"Erro ao registrar: {ex.Message}"); }
    }

    // ── Cancelar uma conta específica (inline, sem afetar as outras) ─────────
    private async Task CancelarContaAsync(ContaReceberSelecionavel? item)
    {
        if (item == null) return;

        var motivo = Interaction.InputBox(
            $"Motivo do cancelamento de '{item.Descricao}' (saldo R$ {item.Saldo:N2}):",
            "Cancelar Conta", "");

        if (string.IsNullOrWhiteSpace(motivo)) return; // usuário cancelou o próprio diálogo

        var confirm = MessageBox.Show(
            $"Confirma cancelar essa conta no valor de R$ {item.Saldo:N2}?\n\nMotivo: {motivo}",
            "Confirmar Cancelamento", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContaReceberService>();
            await service.CancelarAsync(item.Id, motivo);

            MessageBox.Show("Conta cancelada.", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);

            var clienteId = item.Conta.CustomerId;
            await RecarregarContasDoClienteAsync(clienteId);
            await CarregarContasAsync();
        }
        catch (Exception ex) { MessageBox.Show($"Erro ao cancelar: {ex.Message}"); }
    }

    // ── Recarrega as contas de um cliente específico (depois de lote/cancelamento) ──
    private async Task RecarregarContasDoClienteAsync(Guid clienteId)
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IContaReceberService>();
        var contasAtualizadas = (await service.GetPorClienteAsync(clienteId))
            .Where(c => c.Status == "Pendente")
            .OrderBy(c => c.DataVencimento)
            .ToList();

        var resumo = ClientesDevedores.FirstOrDefault(r => r.CustomerId == clienteId);

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ContasSelecionaveis.Clear();
            foreach (var conta in contasAtualizadas)
            {
                var item = new ContaReceberSelecionavel(conta);
                item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ContaReceberSelecionavel.IsSelecionada)) AtualizarTotaisSelecao(); };
                ContasSelecionaveis.Add(item);
            }
            SelecionarTodas = false;
            ValorDesconto   = 0;
            AtualizarTotaisSelecao();

            if (resumo != null)
            {
                resumo.Contas.Clear();
                foreach (var c in contasAtualizadas) resumo.Contas.Add(c);
                resumo.QtdContas = resumo.Contas.Count;
                resumo.TotalPendente = resumo.Contas.Sum(c => c.ValorTotal - c.ValorRecebido - c.ValorDesconto);

                if (resumo.Contas.Count == 0) ClientesDevedores.Remove(resumo);
            }
        });
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