using ERP.WPF.ViewModels;
using System.Windows;

namespace ERP.WPF.Views;

public partial class QuickCustomerView : Window
{
    public QuickCustomerView(QuickCustomerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Escuta o ViewModel. Quando ele mandar fechar, a gente fecha a janela de verdade.
        viewModel.OnRequestClose += (s, sucesso) => 
        {
            this.DialogResult = sucesso;
            this.Close();
        };
    }
}