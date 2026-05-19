using System.Windows;
using System.Windows.Input;
using ERP.WPF.ViewModels;

namespace ERP.WPF.Views;

public partial class VincularProdutoWindow : Window
{
    public VincularProdutoWindow(VincularProdutoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Configura o callback para fechar a janela definindo o DialogResult
        viewModel.OnRequestClose = (resultado) => 
        {
            DialogResult = resultado;
            Close();
        };
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TxtBusca.Focus();
    }

    // Permite buscar apertando ENTER direto no TextBox
    private void TxtBusca_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is VincularProdutoViewModel vm)
            {
                vm.BuscarCommand.Execute(null);
            }
        }
    }

    // Permite confirmar o vínculo dando um duplo clique rápido na linha da Grid
    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is VincularProdutoViewModel vm && vm.ProdutoSelecionado != null)
        {
            vm.ConfirmarCommand.Execute(null);
        }
    }
}