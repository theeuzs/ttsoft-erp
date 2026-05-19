using ERP.WPF.Helpers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ERP.WPF.Views;

public partial class TemaView : Window
{
    private bool _modoEscuro = false;

    public TemaView()
    {
        InitializeComponent();
        CarregarConfigAtual();
    }

    private void CarregarConfigAtual()
    {
        var config = ThemeService.Carregar();
        TxtNome.Text        = config.NomeSistema;
        TxtCorPrimaria.Text = config.CorPrimaria;
        TxtCorAcento.Text   = config.CorAcento;
        _modoEscuro         = config.ModoEscuro;

        AtualizarVisualToggle(animar: false);
        AtualizarPreview(null, null);
    }

    // ── Toggle modo escuro ────────────────────────────────────────────────
    private void Toggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _modoEscuro = !_modoEscuro;
        AtualizarVisualToggle(animar: true);
        AtualizarPreview(null, null);
    }

    private void AtualizarVisualToggle(bool animar)
    {
        if (_modoEscuro)
        {
            BordaToggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"));
            if (animar)
            {
                var anim = new ThicknessAnimation(new Thickness(3, 0, 0, 0), new Thickness(25, 0, 0, 0),
                    TimeSpan.FromMilliseconds(200));
                CirculoToggle.BeginAnimation(MarginProperty, anim);
            }
            else
            {
                CirculoToggle.Margin = new Thickness(25, 0, 0, 0);
            }
        }
        else
        {
            BordaToggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
            if (animar)
            {
                var anim = new ThicknessAnimation(new Thickness(25, 0, 0, 0), new Thickness(3, 0, 0, 0),
                    TimeSpan.FromMilliseconds(200));
                CirculoToggle.BeginAnimation(MarginProperty, anim);
            }
            else
            {
                CirculoToggle.Margin = new Thickness(3, 0, 0, 0);
            }
        }
    }

    // ── Preview ao vivo ───────────────────────────────────────────────────
    private void AtualizarPreview(object? sender, TextChangedEventArgs? e)
    {
        try
        {
            var corPrimaria = ParseColor(TxtCorPrimaria.Text, "#1E3A5F");
            var corAcento   = ParseColor(TxtCorAcento.Text,  "#3B82F6");

            // Sidebar sempre usa a cor primária
            PreviewSidebar.Background   = new SolidColorBrush(corPrimaria);
            PreviewCorPrimaria.Background = new SolidColorBrush(corPrimaria);
            PreviewBotao.Background     = new SolidColorBrush(corAcento);
            PreviewCorAcento.Background = new SolidColorBrush(corAcento);
            PreviewNomeSistema.Text     = TxtNome.Text;

            // Conteúdo muda com modo escuro
            if (_modoEscuro)
            {
                PreviewConteudo.Background    = new SolidColorBrush(ParseColor("#0F172A", "#0F172A"));
                PreviewInput.Background       = new SolidColorBrush(ParseColor("#1E293B", "#1E293B"));
                PreviewInput.BorderBrush      = new SolidColorBrush(ParseColor("#334155", "#334155"));
                PreviewInput.BorderThickness  = new Thickness(1);
                PreviewTextoTitulo.Foreground = new SolidColorBrush(corAcento);
            }
            else
            {
                PreviewConteudo.Background    = new SolidColorBrush(ParseColor("#FFFFFF", "#FFFFFF"));
                PreviewInput.Background       = new SolidColorBrush(ParseColor("#F1F5F9", "#F1F5F9"));
                PreviewInput.BorderBrush      = new SolidColorBrush(ParseColor("#E2E8F0", "#E2E8F0"));
                PreviewInput.BorderThickness  = new Thickness(0);
                PreviewTextoTitulo.Foreground = new SolidColorBrush(ParseColor("#1E293B", "#1E293B"));
            }
        }
        catch { /* cor inválida ainda sendo digitada — ignora */ }
    }

    // ── Presets ───────────────────────────────────────────────────────────
    private void AplicarPreset(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        var partes = tag.Split('|');
        if (partes.Length < 2) return;

        TxtCorPrimaria.Text = partes[0];
        TxtCorAcento.Text   = partes[1];

        // Preset "Dark Pro" ativa o modo escuro automaticamente
        if (partes.Length >= 3 && partes[2] == "dark")
        {
            _modoEscuro = true;
            AtualizarVisualToggle(animar: true);
        }

        AtualizarPreview(null, null);
    }

    // ── Salvar ────────────────────────────────────────────────────────────
    private void Salvar_Click(object sender, RoutedEventArgs e)
    {
        var config = new ThemeConfig
        {
            NomeSistema  = TxtNome.Text.Trim(),
            CorPrimaria  = TxtCorPrimaria.Text.Trim(),
            CorAcento    = TxtCorAcento.Text.Trim(),
            CorTextoMenu = "#FFFFFF",
            CorFundo     = _modoEscuro ? "#0F172A" : "#F1F5F9",
            ModoEscuro   = _modoEscuro,
        };

        ThemeService.Salvar(config);
        ThemeService.Aplicar(config);
        ThemeService.AplicarFundoJanelas();

        MessageBox.Show(
            "✅ Tema salvo e aplicado!\n\n" +
            "Para que o modo escuro afete 100% das telas, reinicie o sistema.",
            "Tema Salvo", MessageBoxButton.OK, MessageBoxImage.Information);

        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e) => Close();

    private static Color ParseColor(string hex, string fallback)
    {
        try   { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return (Color)ColorConverter.ConvertFromString(fallback); }
    }
}
