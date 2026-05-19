using ERP.WPF.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ERP.WPF.Views;

public partial class FinalizarVendaView : Window
{
    public FinalizarVendaView(FinalizarVendaViewModel viewModel)
    {
        InitializeComponent();
        this.DataContext = viewModel;

        if (viewModel != null)
        {
            viewModel.OnRequestClose += (s, e) => this.DialogResult = true;
        }
    }

    private void Fechar_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
        this.Close();
    }

    private void BtnAddPagamento_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var vm = this.DataContext as FinalizarVendaViewModel;
            if (vm != null)
            {
                if (vm.FaltaPagar <= 0)
                    CmbEntrega.Focus();
                else
                    TxtValor.Focus();
            }
        }));
    }

   private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
{
    // ── Enter na lista de clientes ────────────────────────────────────
    if (e.Key == Key.Enter && ListaClientesFinalizar.IsVisible
        && ListaClientesFinalizar.SelectedItem != null)
    {
        var vm = DataContext as FinalizarVendaViewModel;
        vm?.SelecionarClienteCommand.Execute(ListaClientesFinalizar.SelectedItem);
        TxtBuscaClienteFinalizar.Focus();
        e.Handled = true;
        return;
    }

    if (e.Key == Key.Enter)
    {
        if (e.OriginalSource is Button) return;
        if (e.OriginalSource is ComboBox cb && cb.IsDropDownOpen) return;

        e.Handled = true;
        var request = new TraversalRequest(FocusNavigationDirection.Next);
        if (e.OriginalSource is UIElement element)
            element.MoveFocus(request);
    }
}

    private void TextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()));
    }

    private void CmbEntrega_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var vm    = this.DataContext as FinalizarVendaViewModel;
        var combo = sender as ComboBox;
        if (vm != null && combo != null)
            vm.EntregarNoEndereco = (combo.SelectedIndex == 1);
    }

    // ── Navegação por teclado na busca de cliente ─────────────────────
    private void TxtBuscaCliente_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (ListaClientesFinalizar.Items.Count == 0) return;

        if (e.Key == Key.Down)
        {
            ListaClientesFinalizar.Focus();
            ListaClientesFinalizar.SelectedIndex = 0;
            var item = ListaClientesFinalizar.ItemContainerGenerator
                .ContainerFromIndex(0) as ListBoxItem;
            item?.Focus();
            e.Handled = true;
        }
    }

    private void ListaClientes_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ListaClientesFinalizar.SelectedItem != null)
        {
            var vm = DataContext as FinalizarVendaViewModel;
            vm?.SelecionarClienteCommand.Execute(ListaClientesFinalizar.SelectedItem);
            TxtBuscaClienteFinalizar.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            var vm = DataContext as FinalizarVendaViewModel;
            if (vm != null) vm.ClienteListaAberta = false;
            TxtBuscaClienteFinalizar.Focus();
            e.Handled = true;
        }
    }
}