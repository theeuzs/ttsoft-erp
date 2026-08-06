using System.Windows;

namespace ERP.WPF.Views;

public partial class ExportarNotasView : Window
{
    public ExportarNotasView()
    {
        InitializeComponent();
        var vm = new ViewModels.ExportarNotasViewModel();
        DataContext = vm;
        vm.OnConcluido += () => Close();
    }
}
