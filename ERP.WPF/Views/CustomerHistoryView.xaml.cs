using System.Windows;

namespace ERP.WPF.Views;

public partial class CustomerHistoryView : Window
{
    public CustomerHistoryView()
    {
        // É ESTA LINHA AQUI QUE DESENHA A TELA! Se ela sumir, a tela fica branca.
        InitializeComponent();
    }
    private void BtnFechar_Click(object sender, RoutedEventArgs e) => Close();
}