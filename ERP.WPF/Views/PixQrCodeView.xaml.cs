using ERP.WPF.Helpers;
using QRCoder;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ERP.WPF.Views;

public partial class PixQrCodeView : Window
{
    private readonly DispatcherTimer _timer = new();
    private int _segundosRestantes = 120; // 2 minutos de timeout
    private bool _confirmado = false;

    public PixQrCodeView(decimal valor, string chavePix, string nomeBeneficiario, string cidade, string txid)
    {
        InitializeComponent();

        this.Closing += (s, e) => { if (DialogResult == null) DialogResult = _confirmado; };

        TxtValor.Text = $"R$ {valor:N2}";

        // Gera o payload PIX (padrão Banco Central)
        string payload = PixQrCodeService.GerarPayload(
            chavePix:         chavePix,
            nomeBeneficiario: nomeBeneficiario,
            cidade:           cidade,
            valor:            valor,
            txid:             txid);

        TxtPayload.Text = payload;

        // Gera o QR Code
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData      = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            using var qrCode      = new BitmapByteQRCode(qrData);
            byte[] qrBytes        = qrCode.GetGraphic(10);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = new System.IO.MemoryStream(qrBytes);
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.EndInit();

            ImgQrCode.Source = bmp;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar QR Code:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // ── Timer de countdown ────────────────────────────────────────────────
        AtualizarTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick    += Timer_Tick;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _segundosRestantes--;
        AtualizarTimer();

        if (_segundosRestantes <= 0)
        {
            _timer.Stop();
            TxtStatus.Text = "⏰ Tempo esgotado. Confirme manualmente se o pagamento foi recebido.";
            TxtTimer.Text  = "";
            BorderStatus.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FEE2E2"));
            BorderStatus.BorderBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#FCA5A5"));
            TxtStatus.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#991B1B"));
        }
    }

    private void AtualizarTimer()
    {
        var min = _segundosRestantes / 60;
        var seg = _segundosRestantes % 60;
        TxtTimer.Text = $"({min:D2}:{seg:D2})";
    }

    /// <summary>
    /// Chamado pelo PDV quando recebe confirmação externa (webhook, polling de API bancária).
    /// Fecha a janela automaticamente com DialogResult = true.
    /// </summary>
    public void ConfirmarPagamentoAutomaticamente()
    {
        Dispatcher.Invoke(() =>
        {
            _timer.Stop();
            _confirmado = true;

            TxtStatus.Text = "✅ Pagamento recebido!";
            TxtTimer.Text  = "";
            BorderStatus.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#D1FAE5"));
            BorderStatus.BorderBrush = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#6EE7B7"));
            TxtStatus.Foreground = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#065F46"));
            BtnConfirmar.Content    = "✅ Pagamento Confirmado!";
            BtnConfirmar.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString("#059669"));

            // Fecha após 1.5s para o operador ver a confirmação
            var fecharTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            fecharTimer.Tick += (_, _) =>
            {
                fecharTimer.Stop();
                DialogResult = true;
                Close();
            };
            fecharTimer.Start();
        });
    }

    private void CopiarPayload_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(TxtPayload.Text);
        MessageBox.Show("Código PIX copiado!", "PIX", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _confirmado = true;
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
