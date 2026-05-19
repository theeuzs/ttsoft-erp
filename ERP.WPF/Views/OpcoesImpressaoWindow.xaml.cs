using System.Windows;

namespace ERP.WPF.Views;

public partial class OpcoesImpressaoWindow : Window
{
    public string FormatoEscolhido { get; private set; } = string.Empty;

    public OpcoesImpressaoWindow()
    {
        InitializeComponent();
    }

    private void BtnA4_Click(object sender, RoutedEventArgs e)
    {
        FormatoEscolhido = "A4";
        DialogResult = true; // Avisa que deu certo e fecha
    }

    private void BtnCupom_Click(object sender, RoutedEventArgs e)
    {
        FormatoEscolhido = "Cupom";
        DialogResult = true; // Avisa que deu certo e fecha
    }
}