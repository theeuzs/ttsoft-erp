using ERP.Application.DTOs;
using ERP.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class DevolucaoView : Window
{
    public DevolucaoView(DevolucaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.OnFechar += () => Close();
    }

    // S17 FIX: o clique não selecionava o cliente porque o MouseBinding anterior
    // não passava CommandParameter nenhum (comando sempre recebia null). Trocado
    // por SelectionChanged, mesmo padrão confiável já usado no Orçamento.
    private void ListaClientesEncontrados_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is CustomerDto cliente
            && DataContext is DevolucaoViewModel vm && vm.SelecionarClienteCommand.CanExecute(cliente))
        {
            vm.SelecionarClienteCommand.Execute(cliente);
        }
    }
}
