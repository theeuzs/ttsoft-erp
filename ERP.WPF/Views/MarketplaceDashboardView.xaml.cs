using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class MarketplaceDashboardView : UserControl
{
    public MarketplaceDashboardView()
    {
        InitializeComponent();
        DataContext = new ViewModels.MarketplaceDashboardViewModel();
    }
}
