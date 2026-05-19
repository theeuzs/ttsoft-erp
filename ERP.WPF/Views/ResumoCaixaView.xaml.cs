using System.Windows;

namespace ERP.WPF.Views;

public partial class ResumoCaixaView : Window
{
    public ResumoCaixaView()
    {
        InitializeComponent();
    }

    // Esse é o método que o botão "✕" do XAML está chamando para fechar a tela
    private void Fechar_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}