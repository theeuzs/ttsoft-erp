using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class SincronizacaoDiagnosticoView : UserControl
{
    public SincronizacaoDiagnosticoView()
    {
        InitializeComponent();
        DataContext = new ViewModels.SincronizacaoDiagnosticoViewModel();
    }
}
