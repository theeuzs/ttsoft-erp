using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class GerenciadorVendasView : UserControl
{
    public GerenciadorVendasView()
    {
        InitializeComponent();
        DataContext = new ViewModels.GerenciadorVendasViewModel();
    }
}
