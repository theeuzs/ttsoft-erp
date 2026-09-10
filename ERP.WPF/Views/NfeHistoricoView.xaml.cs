using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class NfeHistoricoView : UserControl
{
    public NfeHistoricoView()
    {
        InitializeComponent();
        DataContext = new ViewModels.NfeHistoricoViewModel();
    }
}
