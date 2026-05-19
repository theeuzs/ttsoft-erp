using System.Windows;
using ERP.WPF.ViewModels;

namespace ERP.WPF.Views;

public partial class AbrirCaixaView : Window
{
    public AbrirCaixaView(AbrirCaixaViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        // Ensina a ViewModel como fechar esta janela específica
        viewModel.OnFechar = this.Close; 
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        this.Close(); // Fecha a tela se o usuário desistir
    }
}