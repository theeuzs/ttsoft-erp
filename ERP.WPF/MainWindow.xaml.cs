using ERP.WPF.ViewModels;
using ERP.WPF.Services;
using ERP.WPF.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System;
using System.Windows.Threading;
using System.IO;
using System.Linq;

namespace ERP.WPF;

public partial class MainWindow : Window
{
    private DispatcherTimer _timerLicenca;
    private DispatcherTimer _timerInatividade;
    private DispatcherTimer _timerBackup;
    private DateTime _ultimaAtividade = DateTime.Now;
    private const int MinutosInatividade = 59;
    
    // Variável para evitar que o evento Closing entre em loop
    private bool _podeFechar = false; 

    public MainWindow()
    {
        InitializeComponent();
        NavigateTo("pdv");
        TxtNomeUsuario.Text = ERP.WPF.State.AppSession.UserName?.ToUpper() ?? "USUÁRIO";

        this.PreviewKeyDown += MainWindow_PreviewKeyDown;

        IniciarMonitorDeLicenca();
        IniciarTimerInatividade();
        IniciarTimerBackup();

        var notifVm = new NotificacoesViewModel();
        _ = notifVm.VerificarNotificacoesAsync();
    }

    // ── Timer de inatividade ──────────────────────────────────────────────
    private void IniciarTimerInatividade()
    {
        _timerInatividade = new DispatcherTimer();
        _timerInatividade.Interval = TimeSpan.FromMinutes(1);
        _timerInatividade.Tick += (s, e) =>
        {
            if ((DateTime.Now - _ultimaAtividade).TotalMinutes >= MinutosInatividade)
            {
                _timerInatividade.Stop();
                MessageBox.Show("Sessão encerrada por inatividade.", "TTSoft ERP",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                BtnLogout_Click(null, null);
            }
        };
        _timerInatividade.Start();

        this.PreviewMouseMove += (s, e) => _ultimaAtividade = DateTime.Now;
        this.PreviewKeyDown   += (s, e) => _ultimaAtividade = DateTime.Now;
    }

    private void IniciarTimerBackup()
    {
        _timerBackup = new DispatcherTimer();
        _timerBackup.Interval = TimeSpan.FromHours(1); // Verifica a cada hora
        _timerBackup.Tick += async (s, e) =>
        {
            // Só roda entre 2h e 3h da manhã
            if (DateTime.Now.Hour == 2)
            {
                // Verifica se já fez backup hoje
                string pasta = @"C:\TTSoft_Backups";
                bool jaFezHoje = Directory.Exists(pasta) &&
                    Directory.GetFiles(pasta, "*.bak")
                        .Any(f => File.GetCreationTime(f).Date == DateTime.Today);

                if (!jaFezHoje)
                    await ERP.WPF.Helpers.BackupService.RealizarBackupAutomaticoAsync(silencioso: true);
            }
        };
        _timerBackup.Start();
    }

    // ── Controles de Teclado e Navegação ──────────────────────────────────
    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool isTyping = e.OriginalSource is TextBox ||
                        e.OriginalSource is PasswordBox ||
                        e.OriginalSource is ComboBox;

        if (!isTyping)
        {
            if (e.Key == Key.R)
            {
                this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.X || e.Key == Key.Y)
            {
                if (ContentArea.Content is PdvView pdvView && pdvView.DataContext is PdvViewModel pdvVm)
                {
                    if (e.Key == Key.X && pdvVm.AbrirResumoCaixaCommand?.CanExecute(null) == true)
                    {
                        pdvVm.AbrirResumoCaixaCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }

                    if (e.Key == Key.Y && pdvVm.ReimprimirUltimoReciboCommand?.CanExecute(null) == true)
                    {
                        pdvVm.ReimprimirUltimoReciboCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        // O teclado apenas pede para navegar. A trava de quem pode entrar fica lá no NavigateTo()!
        switch (e.Key)
        {
            case Key.F1:  NavigateTo("dashboard"); e.Handled = true; break;
            case Key.F2:  NavigateTo("pdv"); e.Handled = true; break;
            case Key.F3:  NavigateTo("products"); e.Handled = true; break;
            case Key.F4:  NavigateTo("customers"); e.Handled = true; break;
            case Key.F5:  NavigateTo("sales"); e.Handled = true; break;
            case Key.F6:  NavigateTo("orcamentos"); e.Handled = true; break;
            case Key.F7:  NavigateTo("users"); e.Handled = true; break;
            case Key.F8:  NavigateTo("financeiro"); e.Handled = true; break;
            case Key.F9:  NavigateTo("contaspagar"); e.Handled = true; break;
            case Key.F10: NavigateTo("auditoria"); e.Handled = true; break;

            case Key.F11:
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
                e.Handled = true;
                break;

            case Key.Escape:
                var f = Keyboard.FocusedElement;
                if (f is TextBox || f is ListBoxItem || f is DataGridCell || f is UserControl)
                    break;
                if (this.WindowState == WindowState.Maximized)
                {
                    this.WindowStyle = WindowStyle.SingleBorderWindow;
                    this.WindowState = WindowState.Normal;
                    e.Handled = true;
                }
                break;
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
            NavigateTo(tag);
    }

    // Mapeamento de página → permissão necessária (páginas livres não aparecem aqui)
    private static readonly System.Collections.Generic.Dictionary<string, string> _pagePermissions = new()
    {
        ["dashboard"]    = ERP.WPF.State.PermissionChecker.ReportFinancial,
        ["users"]        = ERP.WPF.State.PermissionChecker.UsersView,
        ["financeiro"]   = ERP.WPF.State.PermissionChecker.FinanceiroView,
        ["contaspagar"]  = ERP.WPF.State.PermissionChecker.DespesasView,
        ["auditoria"]    = ERP.WPF.State.PermissionChecker.AuditView,
        ["compras"]      = ERP.WPF.State.PermissionChecker.ComprasView,
        ["margem"]       = ERP.WPF.State.PermissionChecker.MargemView,
        ["inventario"]   = ERP.WPF.State.PermissionChecker.InventarioView,
        ["fluxocaixa"]   = ERP.WPF.State.PermissionChecker.FluxoCaixaView,
        ["notificacoes"] = ERP.WPF.State.PermissionChecker.InventarioView,
        ["nfce"]         = ERP.WPF.State.PermissionChecker.NotasFiscais,
        ["config"]       = ERP.WPF.State.PermissionChecker.ConfigView,
        ["filiais"]      = ERP.WPF.State.PermissionChecker.UsersView, // Gerente+
    };

    public void NavigateTo(string page)
    {
        // Bouncer dinâmico: verifica permissão pelo código, não pelo nome do cargo
        if (_pagePermissions.TryGetValue(page, out var requiredPerm))
        {
            if (!ERP.WPF.State.PermissionChecker.Has(requiredPerm))
                return; // Silencioso — nem pisca a tela
        }

        ContentArea.Content = page switch
        {
            "pdv"         => CreateView<PdvView, PdvViewModel>(),
            "products"    => CreateView<ProductView, ProductViewModel>(),
            "sales"       => CreateView<SaleView, SaleViewModel>(),
            "orcamentos"  => CreateView<OrcamentosView, OrcamentosViewModel>(),
            "dashboard"   => CreateView<DashboardView, DashboardViewModel>(),
            "customers"   => CreateView<CustomerView, CustomerViewModel>(),
            "users"       => CreateView<UserView, UserViewModel>(),
            "config"      => CreateView<ConfiguracoesView, ConfiguracoesViewModel>(),
            "integracoes" => CreateView<IntegracoesView, IntegracoesViewModel>(),
            "filiais"     => CreateView<FilialView, FilialViewModel>(),
            "bi"          => CreateView<BIView, BIViewModel>(),
            "financeiro"  => CreateView<FinanceiroView, FinanceiroViewModel>(),
            "contaspagar" => CreateView<ContaPagarView, ContaPagarViewModel>(),
            "contasbancarias" => CreateView<ContaBancariaView, ContaBancariaViewModel>(),
            "importXml"   => CreateView<NfeImportView, NfeImportViewModel>(),
            "sped"        => CreateView<SpedView, SpedViewModel>(),
            "nfce"        => CreateView<NotasFiscaisView, NotasFiscaisViewModel>(),
            "auditoria"   => CreateView<AuditLogView, AuditLogViewModel>(),
            "compras"     => CreateView<ComprasView, ComprasViewModel>(),
            "historicocompras" => CreateView<HistoricoComprasView, HistoricoComprasViewModel>(),
            "historicovendas" => CreateView<HistoricoVendasView, HistoricoVendasViewModel>(),
            "conciliacaobancaria" => CreateView<ConciliacaoBancariaView, ConciliacaoBancariaViewModel>(),
            "operadorasrecebimento" => CreateView<OperadoraRecebimentoView, OperadoraRecebimentoViewModel>(),
            "recebiveisoperadora" => CreateView<RecebivelOperadoraView, RecebivelOperadoraViewModel>(),
            "extratofinanceiro" => CreateView<ExtratoFinanceiroView, ExtratoFinanceiroViewModel>(),
            "fluxocaixa"  => new FluxoCaixaView(),
            "margem"      => new MargemView(),
            "inventario"  => new InventarioView(),
            "notificacoes" => new Views.NotificacoesView(),
            "catalogo"     => CreateView<CatalogoView, CatalogoViewModel>(),
            "chat"         => AbrirChat(),

            _ => new TextBlock
            {
                Text = "Em desenvolvimento...",
                FontSize = 20,
                Margin = new Thickness(20),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private UIElement AbrirChat()
    {
        // Abre a janela flutuante de chat e retorna um placeholder na navegação principal
        var chatService = ERP.WPF.App.Services
            .GetRequiredService<ERP.WPF.Services.ChatService>();

        var chatWindow = new Views.ChatPopupWindow(chatService);
        chatWindow.Left  = SystemParameters.PrimaryScreenWidth  - 380;
        chatWindow.Top   = SystemParameters.PrimaryScreenHeight - 520;
        chatWindow.Show();

        return new TextBlock
        {
            Text = "💬 Chat aberto em janela flutuante →",
            FontSize = 18,
            Margin = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"))
        };
    }

    private TView CreateView<TView, TViewModel>()
        where TView : UserControl, new()
        where TViewModel : BaseViewModel
    {
        var view = new TView();
        var vm   = App.Services.GetRequiredService<TViewModel>();
        view.DataContext = vm;
        return view;
    }

    // ── Fechamento via "X" da Janela (Encerra o Aplicativo) ───────────────
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_podeFechar) return;

        e.Cancel = true;

        _timerInatividade?.Stop();
        _timerLicenca?.Stop();

        var dialog = new Views.LogoutDialog { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            _podeFechar = true; 
            System.Windows.Application.Current.Shutdown(); 
        }
        else
        {
            _timerInatividade?.Start();
            _timerLicenca?.Start();
        }
    }

    // ── Fechamento via Botão "Sair do Sistema" (Volta para o Login) ───────
    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        _timerInatividade?.Stop();
        _timerLicenca?.Stop();

        var dialog = new Views.LogoutDialog { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            _podeFechar = true; 
            
            ERP.WPF.State.AppSession.Logout();
            
            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            this.Close(); 

            var loginVm   = App.Services.GetRequiredService<LoginViewModel>();
            var telaLogin = new LoginView(loginVm);

            if (telaLogin.ShowDialog() == true)
            {
                var newMainWindow = new MainWindow();
                System.Windows.Application.Current.MainWindow = newMainWindow;
                System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;
                newMainWindow.Show();
            }
        }
        else
        {
            _timerInatividade?.Start();
            _timerLicenca?.Start();
        }
    }

    // ── Monitor de licença ────────────────────────────────────────────────
    private void IniciarMonitorDeLicenca()
    {
        _timerLicenca = new DispatcherTimer();
        _timerLicenca.Interval = TimeSpan.FromHours(1);
        _timerLicenca.Tick += VerificarLicenca_Tick;
        _timerLicenca.Start();
    }

    private void VerificarLicenca_Tick(object sender, EventArgs e)
    {
        if (VerificarSeVenceuNoBancoLocal())
        {
            _timerLicenca.Stop();
            MessageBox.Show("Sua licença expirou! Por favor, realize o pagamento para continuar vendendo.",
                "Licença Expirada - TTSoft", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Windows.Application.Current.Shutdown();
        }
    }

    private bool VerificarSeVenceuNoBancoLocal()
    {
        DateTime dataVencimento = ERP.WPF.State.AppSession.DataVencimentoLicenca;
        return DateTime.Now > dataVencimento;
    }
}