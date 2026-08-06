using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class NfeRecebidaView : UserControl
{
    public NfeRecebidaView()
    {
        InitializeComponent();
        DataContext = new ViewModels.NfeRecebidaViewModel();
    }
}
