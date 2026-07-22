using ERP.Application.Interfaces;
using ERP.Domain.Entities; 
using ERP.Domain.Enums;    
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ERP.Application.DTOs;
using Microsoft.Extensions.DependencyInjection;
using ERP.WPF.Commands;

namespace ERP.WPF.ViewModels;

public class SaleViewModel : BaseViewModel
{
    private readonly ISaleService _saleServiceFallback;
    private readonly ICaixaService _caixaServiceFallback; 

    private decimal _totalRevenue;
    public decimal TotalRevenue { get => _totalRevenue; set { _totalRevenue = value; OnPropertyChanged(nameof(TotalRevenue)); OnPropertyChanged(nameof(FaturamentoExibicao)); } }

    // S17: faturamento escondido por padrão — tela fica visível no balcão,
    // cliente não precisa ver o total faturado. Clique no olho pra revelar.
    private bool _faturamentoVisivel = false;
    public bool FaturamentoVisivel
    {
        get => _faturamentoVisivel;
        set { _faturamentoVisivel = value; OnPropertyChanged(nameof(FaturamentoVisivel)); OnPropertyChanged(nameof(FaturamentoExibicao)); }
    }

    public string FaturamentoExibicao => FaturamentoVisivel ? TotalRevenue.ToString("C2") : "R$ ***";

    private int _totalSalesCount;
    public int TotalSalesCount { get => _totalSalesCount; set { _totalSalesCount = value; OnPropertyChanged(nameof(TotalSalesCount)); } }

    private decimal _averageTicket;
    public decimal AverageTicket { get => _averageTicket; set { _averageTicket = value; OnPropertyChanged(nameof(AverageTicket)); } }

    private DateTime _startDate;
    public DateTime StartDate { get => _startDate; set { _startDate = value; OnPropertyChanged(nameof(StartDate)); } }

    private DateTime _endDate;
    public DateTime EndDate { get => _endDate; set { _endDate = value; OnPropertyChanged(nameof(EndDate)); } }

    private string _selectedPaymentFilter = "Todas as Formas";
    public string SelectedPaymentFilter { get => _selectedPaymentFilter; set { _selectedPaymentFilter = value; OnPropertyChanged(nameof(SelectedPaymentFilter)); } }

    private string _searchText;
    public string SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(nameof(SearchText)); } }

    public ObservableCollection<SaleDto> SalesList { get; set; } = new ObservableCollection<SaleDto>();

    public ICommand FilterCommand          { get; }
    public ICommand VisualizarReciboCommand { get; }
    public ICommand ReimprimirReciboCommand { get; }
    public ICommand CancelarVendaCommand   { get; }
    public ICommand DevolverItensCommand   { get; }
    public ICommand EnviarWhatsAppCommand  { get; }
    public ICommand AlternarFaturamentoCommand { get; }

    public SaleViewModel(ISaleService saleService, ICaixaService caixaService)
    {
        _saleServiceFallback  = saleService;
        _caixaServiceFallback = caixaService;

        StartDate = DateTime.Today.AddDays(-3);
        EndDate   = DateTime.Today;

        FilterCommand           = new RelayCommand(async (_) => await LoadSalesAsync());
        VisualizarReciboCommand = new AsyncRelayCommand(async v => await AbrirPreview(v as SaleDto));
        ReimprimirReciboCommand = new AsyncRelayCommand(async v => await MandarImprimir(v as SaleDto));
        CancelarVendaCommand    = new AsyncRelayCommand(async v => await CancelarVendaAsync(v as SaleDto));
        DevolverItensCommand    = new AsyncRelayCommand(async v => await AbrirDevolucaoAsync(v as SaleDto));
        EnviarWhatsAppCommand   = new AsyncRelayCommand(async v => await MandarWhatsApp(v as SaleDto));
        AlternarFaturamentoCommand = new RelayCommand(_ => FaturamentoVisivel = !FaturamentoVisivel);

        _ = LoadSalesAsync();
    }

    private async Task ExecuteWithFreshSaleServiceAsync(Func<ISaleService, Task> action)
    {
        var app  = System.Windows.Application.Current;
        var prop = app.GetType().GetProperty("ServiceProvider");
        if (prop != null && prop.GetValue(app) is IServiceProvider provider)
        {
            using var scope = provider.CreateScope();
            await action(scope.ServiceProvider.GetRequiredService<ISaleService>());
        }
        else { await action(_saleServiceFallback); }
    }

    private async Task<T> ExecuteWithFreshSaleServiceAsync<T>(Func<ISaleService, Task<T>> action)
    {
        var app  = System.Windows.Application.Current;
        var prop = app.GetType().GetProperty("ServiceProvider");
        if (prop != null && prop.GetValue(app) is IServiceProvider provider)
        {
            using var scope = provider.CreateScope();
            return await action(scope.ServiceProvider.GetRequiredService<ISaleService>());
        }
        return await action(_saleServiceFallback);
    }

    private async Task ExecuteWithFreshCaixaServiceAsync(Func<ICaixaService, Task> action)
    {
        var app  = System.Windows.Application.Current;
        var prop = app.GetType().GetProperty("ServiceProvider");
        if (prop != null && prop.GetValue(app) is IServiceProvider provider)
        {
            using var scope = provider.CreateScope();
            await action(scope.ServiceProvider.GetRequiredService<ICaixaService>());
        }
        else { await action(_caixaServiceFallback); }
    }

    private async Task CancelarVendaAsync(SaleDto venda)
    {
        if (venda == null) return;

        if (venda.Status == SaleStatus.Cancelada)
        {
            MessageBox.Show("Esta venda já consta como cancelada no sistema!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 👇 AQUI ESTÁ A MÁGICA DA SEGURANÇA 👇
        if (!ERP.WPF.State.PermissionChecker.Has(ERP.WPF.State.PermissionChecker.SaleCancel))
        {
            // O cara não tem permissão! Vamos abrir a tela de Senha do Gerente.
            var telaAcessoRestrito = new ERP.WPF.Views.SenhaGerenteView();
            telaAcessoRestrito.Owner = System.Windows.Application.Current.MainWindow;
            telaAcessoRestrito.ShowDialog();

            // Se o gerente não botar a senha (ou cancelar), a gente morre a operação aqui.
            if (!telaAcessoRestrito.Autorizado)
            {
                return; // Silencioso, sem MessageBox feia. O cara simplesmente não consegue passar.
            }
        }
        // Se o cara TIVER permissão (ex: Matheus), ele passa reto pelo bloco acima e não pede senha!

        // Se chegou aqui, ou ele é gerente, ou um gerente autorizou ele na tela de cima.
        var confirmacao = MessageBox.Show(
            $"Tem certeza que deseja CANCELAR a venda {venda.SaleNumber} no valor de R$ {venda.Total:N2}?\n\n" +
            "Os produtos voltarão ao estoque e o caixa registrará o estorno de todas as formas de pagamento.",
            "Confirmar Cancelamento", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacao != MessageBoxResult.Yes) return;

        try
        {
            // S17 FIX cancelamento: trava de segurança PRIMEIRO, antes de tocar em
            // qualquer coisa (estoque, status, financeiro). Se a venda tiver um
            // recebível de cartão já liquidado, aborta aqui — nada foi alterado.
            var motorFinanceiro = ERP.WPF.App.Services.GetRequiredService<IMotorFinanceiroService>();
            await motorFinanceiro.VerificarPodeCancelarVendaAsync(venda.Id);

            var detalhes = await ExecuteWithFreshSaleServiceAsync(s => s.GetDetailAsync(venda.Id));
            await ExecuteWithFreshSaleServiceAsync(s => s.CancelAsync(venda.Id, "Cancelado pelo usuário no Histórico"));

            if (detalhes != null && detalhes.Payments != null)
            {
                decimal totalPago = detalhes.Payments.Sum(p => p.Amount);
                decimal troco     = totalPago > detalhes.Total ? totalPago - detalhes.Total : 0;
                var pagamentos    = detalhes.Payments.ToList();

                using (var scope = ERP.WPF.App.Services.CreateScope())
                {
                    var uow          = scope.ServiceProvider.GetRequiredService<ERP.Domain.Interfaces.IUnitOfWork>();
                    var todasContas  = await uow.ContasReceber.GetAllAsync();
                    var contasVenda  = todasContas.Where(c => c.SaleId == venda.Id && c.Status == "Pendente").ToList();
                    foreach (var conta in contasVenda)
                    {
                        conta.Status    = "Cancelada";
                        conta.Descricao += " (Venda Cancelada)";
                    }
                    if (contasVenda.Any()) await uow.CommitAsync();
                }

                Guid usuarioId = ERP.WPF.State.AppSession.UserId;

                // S17 FIX: antes, esse loop tratava toda forma de pagamento como
                // Sangria de Caixa (inclusive PIX e Cartão) — nunca revertia o que
                // o Motor Financeiro tinha criado em Conta Bancária/Recebível, e
                // ainda quebrava com erro de saldo insuficiente pra formas que
                // nunca deveriam ter sido tratadas como dinheiro físico. Agora o
                // Motor Financeiro decide o que fazer com cada forma, igual já
                // faz na hora de criar.
                var pagamentosTuple = pagamentos
                    .Where(p => p.Amount > 0 && Enum.TryParse<PaymentMethod>(p.PaymentMethod.ToString(), out _))
                    .Select(p => (Enum.Parse<PaymentMethod>(p.PaymentMethod.ToString()), p.Amount));

                await motorFinanceiro.EstornarVendaAsync(
                    venda.Id, usuarioId, $"ESTORNO VENDA {venda.SaleNumber}", troco, pagamentosTuple);
            }

            MessageBox.Show("Venda cancelada com sucesso! O estoque foi restaurado e o caixa atualizado.", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadSalesAsync();
            PdvViewModel.NotificacaoCaixaAlterado?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao tentar cancelar a venda:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadSalesAsync()
    {
        try
        {
            DateTime dataFimAjustada = EndDate.Date.AddDays(1).AddTicks(-1);

            IEnumerable<SaleDto> allSales;
            using (var scope = App.Services.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<ISaleService>();
                allSales = await service.GetAllAsync(StartDate.Date, dataFimAjustada);
            }

            if (allSales == null || !allSales.Any())
            {
                UpdateCards(0, 0);
                SalesList.Clear();
                return;
            }

            var filteredSales = allSales.AsEnumerable();

            if (SelectedPaymentFilter != "Todas as Formas")
            {
                string filtroExato = SelectedPaymentFilter switch
                {
                    "Cartão de Crédito" => "CartaoCredito",
                    "Cartão de Débito"  => "CartaoDebito",
                    "PIX"               => "Pix",
                    _                   => SelectedPaymentFilter
                };
                filteredSales = filteredSales.Where(s =>
                    s.PaymentMethods != null &&
                    s.PaymentMethods.ToString().Contains(filtroExato, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var busca = SearchText.Trim().ToLower();
                filteredSales = filteredSales.Where(s =>
                    (s.CustomerName != null && s.CustomerName.ToLower().Contains(busca)) ||
                    (s.SaleNumber   != null && s.SaleNumber.ToLower().Contains(busca))   ||
                    (s.SellerName   != null && s.SellerName.ToLower().Contains(busca)));
            }

            var finalSales = filteredSales.OrderByDescending(s => s.SaleDate).ToList();

            SalesList.Clear();
            foreach (var sale in finalSales) SalesList.Add(sale);

            var vendasValidas = finalSales.Where(s => s.Status != SaleStatus.Cancelada).ToList();
            UpdateCards(vendasValidas.Sum(s => s.Total), vendasValidas.Count);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar o histórico:\n{ex.Message}", "Erro no Banco", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateCards(decimal revenue, int count)
    {
        TotalRevenue    = revenue;
        TotalSalesCount = count;
        AverageTicket   = count > 0 ? revenue / count : 0;
    }

    private async Task AbrirPreview(SaleDto vendaListagem)
    {
        if (vendaListagem == null) return;
        try
        {
            var detalhesVenda = await ExecuteWithFreshSaleServiceAsync(s => s.GetDetailAsync(vendaListagem.Id));
            if (detalhesVenda == null) return;

            var itensParaImprimir = detalhesVenda.Items.Select(item => new ViewModels.CartItem
            {
                ProductName       = item.ProductName,
                Quantity          = item.Quantity,
                UnitPrice         = item.UnitPrice,
                NormalUnitPrice   = item.UnitPrice,
                DiscountPercent   = item.DiscountPercent,
                LabelUnidadeVenda = item.LabelUnidadeVenda,
                UnidadeEstoque    = item.UnidadeEstoque ?? string.Empty,
                FatorConversao    = item.FatorConversao,
                TotalSalvo        = item.TotalPrice, // Total exato salvo no banco
            }).ToList();

            var pagamentosParaImprimir = detalhesVenda.Payments.Select(pag => (pag.PaymentMethod, pag.Amount)).ToList();

            Helpers.ReciboPrinter.Visualizar(
                detalhesVenda.Id, itensParaImprimir, detalhesVenda.Total, detalhesVenda.DiscountAmount,
                detalhesVenda.CustomerName ?? "Consumidor Final",
                detalhesVenda.SellerName ?? "Balcão",
                pagamentosParaImprimir, 0, detalhesVenda.Observation ?? "",
                dataVenda:   detalhesVenda.SaleDate,
                numeroVenda: detalhesVenda.SaleNumber);
        }
        catch (Exception ex) { MessageBox.Show($"Erro:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task MandarImprimir(SaleDto vendaListagem)
    {
        if (vendaListagem == null) return;
        try
        {
            var detalhesVenda = await ExecuteWithFreshSaleServiceAsync(s => s.GetDetailAsync(vendaListagem.Id));
            if (detalhesVenda == null) return;

            var itensParaImprimir = detalhesVenda.Items.Select(item => new ViewModels.CartItem
            {
                ProductName       = item.ProductName,
                Quantity          = item.Quantity,
                UnitPrice         = item.UnitPrice,
                NormalUnitPrice   = item.UnitPrice,
                DiscountPercent   = item.DiscountPercent,
                LabelUnidadeVenda = item.LabelUnidadeVenda,
                UnidadeEstoque    = item.UnidadeEstoque ?? string.Empty,
                FatorConversao    = item.FatorConversao,
                TotalSalvo        = item.TotalPrice, // Total exato salvo no banco
            }).ToList();

            var pagamentosParaImprimir = detalhesVenda.Payments.Select(pag => (pag.PaymentMethod, pag.Amount)).ToList();

            Helpers.ReciboPrinter.Imprimir(
                detalhesVenda.Id, itensParaImprimir, detalhesVenda.Total, detalhesVenda.DiscountAmount,
                detalhesVenda.CustomerName ?? "Consumidor Final",
                detalhesVenda.SellerName ?? "Balcão",
                pagamentosParaImprimir, 0, detalhesVenda.Observation ?? "",
                dataVenda:   detalhesVenda.SaleDate,
                numeroVenda: detalhesVenda.SaleNumber);
        }
        catch (Exception ex) { MessageBox.Show($"Erro:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async Task MandarWhatsApp(SaleDto vendaListagem)
    {
        if (vendaListagem == null) return;
        try
        {
            var detalhes = await ExecuteWithFreshSaleServiceAsync(s => s.GetDetailAsync(vendaListagem.Id));
            if (detalhes == null) return;

            string texto = $"*VILA VERDE MATERIAIS DE CONSTRUÇÃO*\n\n";
            texto += $"*Recibo da Venda:* {detalhes.SaleNumber}\n";
            texto += $"*Data:* {detalhes.SaleDate:dd/MM/yyyy HH:mm}\n";
            texto += $"*Cliente:* {detalhes.CustomerName ?? "Consumidor Final"}\n\n";
            texto += "*ITENS DO PEDIDO:*\n";
            foreach (var item in detalhes.Items)
                texto += $"{(int)item.Quantity}x {item.ProductName} - R$ {item.TotalPrice:N2}\n";
            texto += $"\n---------------------------\n*TOTAL DA VENDA: R$ {detalhes.Total:N2}*\n\n";
            if (detalhes.Payments != null && detalhes.Payments.Any())
            {
                texto += "*PAGO EM:*\n";
                foreach (var pag in detalhes.Payments)
                    texto += $"- {pag.PaymentMethod}: R$ {pag.Amount:N2}\n";
            }
            texto += "---------------------------\n\nAgradecemos a preferência! Volte sempre! 🧱🌱";

            string textoCodificado   = Uri.EscapeDataString(texto);
            string parametroTelefone = "";

            if (!string.IsNullOrWhiteSpace(detalhes.CustomerPhone))
            {
                string numeroLimpo = new string(detalhes.CustomerPhone.Where(char.IsDigit).ToArray());
                if (numeroLimpo.Length >= 10)
                {
                    if (!numeroLimpo.StartsWith("55")) numeroLimpo = "55" + numeroLimpo;
                    parametroTelefone = $"phone={numeroLimpo}&";
                }
            }

            string url = $"https://api.whatsapp.com/send?{parametroTelefone}text={textoCodificado}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show($"Erro no WhatsApp: {ex.Message}"); }
    }

    private async Task AbrirDevolucaoAsync(SaleDto? venda)
    {
        if (venda == null) return;

        if (venda.Status == Domain.Enums.SaleStatus.Cancelada)
        {
            System.Windows.MessageBox.Show("Não é possível devolver itens de uma venda cancelada.",
                "Aviso", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            using var scope     = App.Services.CreateScope();
            var saleService     = scope.ServiceProvider.GetRequiredService<ISaleService>();
            var detalhe         = await saleService.GetDetailAsync(venda.Id);
            if (detalhe == null) return;

            // Carrega quantidades já devolvidas para bloquear exploit de devolução múltipla
            var devolucaoSvc = scope.ServiceProvider.GetRequiredService<IDevolucaoService>();
            var jaDevolvidos = new Dictionary<Guid, decimal>();
            foreach (var item in detalhe.Items)
            {
                decimal jd = await devolucaoSvc.GetQuantidadeJaDevolvida(venda.Id, item.ProductId);
                if (jd > 0) jaDevolvidos[item.ProductId] = jd;
            }

            string nomeCliente = detalhe.CustomerName ?? "Consumidor Final";
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var vm   = new DevolucaoViewModel(venda, detalhe, detalhe.CustomerId, nomeCliente, customerService, jaDevolvidos);
            var view = new Views.DevolucaoView(vm);

            vm.OnDevolucaoConcluida += resultado =>
            {
                var itensDevolvidos = resultado.ItensDevolvidos.Select(i => new CartItem
                {
                    ProductId   = i.ProductId,
                    ProductName = i.ProductName,
                    Quantity    = i.QuantidadeDevolver,
                    UnitPrice   = i.UnitPrice,
                }).ToList();

                string obs = $"DEVOLUÇÃO PARCIAL\nVenda original: {resultado.NumeroVendaOriginal}\n" +
                             $"Crédito em Haver: R$ {resultado.ValorTotalDevolvido:N2}";

                var pagamentos = new List<(string, decimal)>
                    { ("Crédito Haver", resultado.ValorTotalDevolvido) };

                var resposta = System.Windows.MessageBox.Show(
                    $"✅ Devolução registrada!\nR$ {resultado.ValorTotalDevolvido:N2} creditados em Haver para {resultado.NomeCliente}.\n\nDeseja imprimir o recibo de devolução?",
                    "Devolução Concluída",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Information);

                if (resposta == System.Windows.MessageBoxResult.Yes)
                {
                    Helpers.ReciboPrinter.Imprimir(
                        venda.Id, itensDevolvidos, resultado.ValorTotalDevolvido, 0,
                        resultado.NomeCliente, ERP.WPF.State.AppSession.UserName ?? "",
                        pagamentos, 0, obs, "DEVOLUÇÃO", DateTime.Now, resultado.NumeroVendaOriginal);
                }

                _ = LoadSalesAsync();
            };

            view.Owner = System.Windows.Application.Current.MainWindow;
            view.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Erro ao abrir devolução:\n{ex.Message}", "Erro",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}