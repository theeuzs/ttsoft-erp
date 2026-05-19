using System.Windows;
using ERP.WPF.ViewModels;

namespace ERP.WPF.Views;

public partial class MovimentoCaixaView : Window
{
    public MovimentoCaixaView(MovimentoCaixaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.OnFechar = this.Close;
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }
}