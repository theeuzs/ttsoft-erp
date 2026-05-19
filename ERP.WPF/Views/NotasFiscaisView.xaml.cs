using ERP.WPF.ViewModels;
using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class NotasFiscaisView : UserControl
{
    public NotasFiscaisView()
    {
        InitializeComponent();

        this.Loaded += (s, e) =>
        {
            if (DataContext is NotasFiscaisViewModel vm && vm.AtualizarListaCommand.CanExecute(null))
            {
                vm.AtualizarListaCommand.Execute(null);
            }
        };
    }
}