using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class LegacyImportView : UserControl
{
    public LegacyImportView()
    {
        InitializeComponent();
        DataContext = new ViewModels.LegacyImportViewModel();
    }
}
