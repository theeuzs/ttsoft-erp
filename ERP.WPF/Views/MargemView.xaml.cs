using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class MargemView : UserControl
{
    public MargemView()
    {
        InitializeComponent();
        DataContext = new ViewModels.MargemViewModel();
    }
}
