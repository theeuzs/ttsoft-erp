using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ERP.WPF.ViewModels;

namespace ERP.WPF.Views;

public partial class ConsultaPrecoView : Window
{
    public ConsultaPrecoView(ConsultaPrecoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.OnFechar = Close;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e) { TxtLeitor.Focus(); }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();

        // Puxa a ViewModel UMA vez só para evitar erro de escopo (CS0136)
        var vm = DataContext as ConsultaPrecoViewModel;
        if (vm == null) return;

        // 👇 ATALHO "M" PARA FOCAR NA QUANTIDADE 👇
        if (e.Key == Key.M && vm.ProdutoVisivel == Visibility.Visible)
        {
            if (!TxtQuantidade.IsFocused)
            {
                TxtQuantidade.Focus();
                TxtQuantidade.SelectAll();
                e.Handled = true;
            }
        }

        // 👇 ATALHO "ALT + U" PARA ADICIONAR AO CARRINHO 👇
        if (e.KeyboardDevice.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.U)
        {
            if (vm.AdicionarCarrinhoCommand.CanExecute(null))
            {
                vm.AdicionarCarrinhoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void TxtLeitor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (DataContext is ConsultaPrecoViewModel vm) vm.BuscarCommand.Execute(null);
        }
    }

    // 👇 ENTER NA QUANTIDADE ADICIONA DIRETO AO CARRINHO 👇
    private void TxtQuantidade_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            
            // Um pequeno delay para garantir que o Text do TextBox sincronizou com a ViewModel
            Dispatcher.InvokeAsync(() => 
            {
                if (DataContext is ConsultaPrecoViewModel vm && vm.AdicionarCarrinhoCommand.CanExecute(null))
                {
                    vm.AdicionarCarrinhoCommand.Execute(null);
                }
            }, DispatcherPriority.Input);
        }
    }
}