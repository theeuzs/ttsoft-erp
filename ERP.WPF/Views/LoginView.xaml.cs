using ERP.WPF.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace ERP.WPF.Views;

public partial class LoginView : Window
{
    private readonly LoginViewModel _viewModel;
    private bool _senhaVisivel = false;

    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.OnLoginResult += (s, sucesso) =>
        {
            if (sucesso)
            {
                this.DialogResult = true;
                this.Close();
            }
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // Animação escalonada: painel esquerdo → card → campos
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        // 1. Painel esquerdo: slide da esquerda + fade
        AnimateElement(LeftPanel, fadeIn: true, slideX: -60, delay: 0.1, ease: ease);

        // 2. Card: slide da direita + fade  
        AnimateElement(LoginCard, fadeIn: true, slideX: 50, delay: 0.35, ease: ease);
    }

    private void AnimateElement(UIElement el, bool fadeIn, double slideX = 0,
                                double slideY = 0, double delay = 0, EasingFunctionBase? ease = null)
    {
        var translateX = new DoubleAnimation(slideX, 0,
            TimeSpan.FromSeconds(0.55))
        {
            BeginTime = TimeSpan.FromSeconds(delay),
            EasingFunction = ease
        };
        var translateY = new DoubleAnimation(slideY, 0,
            TimeSpan.FromSeconds(0.55))
        {
            BeginTime = TimeSpan.FromSeconds(delay),
            EasingFunction = ease
        };
        var fadeAnim = new DoubleAnimation(0, 1,
            TimeSpan.FromSeconds(0.5))
        {
            BeginTime = TimeSpan.FromSeconds(delay)
        };

        var transform = new System.Windows.Media.TranslateTransform();
        el.RenderTransform = transform;

        transform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, translateX);
        transform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, translateY);
        el.BeginAnimation(OpacityProperty, fadeAnim);
    }

    // ── PasswordBox → ViewModel ──────────────────────────────────────────
    private void TxtSenha_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Senha = txtSenha.Password;
            if (_senhaVisivel) SenhaNaked.Text = txtSenha.Password;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // ── Toggle mostrar/ocultar senha ────────────────────────────────────
    private void BtnToggleSenha_Click(object sender, RoutedEventArgs e)
    {
        _senhaVisivel = !_senhaVisivel;

        if (_senhaVisivel)
        {
            SenhaNaked.Text = txtSenha.Password;
            txtSenha.Visibility       = Visibility.Collapsed;
            SenhaNakedBorder.Visibility = Visibility.Visible;
            SenhaNaked.Focus();
            SenhaNaked.CaretIndex = SenhaNaked.Text.Length;
        }
        else
        {
            txtSenha.Password         = SenhaNaked.Text;
            SenhaNakedBorder.Visibility = Visibility.Collapsed;
            txtSenha.Visibility       = Visibility.Visible;
            txtSenha.Focus();
        }

        if (DataContext is LoginViewModel vm)
            vm.Senha = _senhaVisivel ? SenhaNaked.Text : txtSenha.Password;
    }

    // Sync quando digita no campo texto visível
    private void SenhaNaked_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_senhaVisivel && DataContext is LoginViewModel vm)
        {
            vm.Senha = SenhaNaked.Text;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // ── WhatsApp ─────────────────────────────────────────────────────────
    private void AbrirWhatsApp(object sender, MouseButtonEventArgs e)
        => AbrirLink("https://wa.me/5541996272846?text=Ol%C3%A1%2C+preciso+de+ajuda+com+o+ConstruTTor+ERP");

    private void EsqueciSenha_Click(object sender, MouseButtonEventArgs e)
        => AbrirLink("https://wa.me/5541996272846?text=Ol%C3%A1%2C+esqueci+minha+senha+do+ConstruTTor+ERP");

    private static void AbrirLink(string url)
        => Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

    // ── Controles de janela ──────────────────────────────────────────────
    private void BtnSair_Click(object sender, RoutedEventArgs e)
        => System.Windows.Application.Current.Shutdown();

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        => this.WindowState = WindowState.Minimized;

    private void BtnMaxRestore_Click(object sender, RoutedEventArgs e)
        => this.WindowState = this.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                this.Left = e.GetPosition(this).X - this.Width / 2;
                this.Top = 5;
            }
            DragMove();
        }
    }
}
