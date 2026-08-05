using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class OrganizacaoVendasView : UserControl
{
    public OrganizacaoVendasView()
    {
        InitializeComponent();
        DataContext = new ViewModels.OrganizacaoVendasViewModel();
    }
}
