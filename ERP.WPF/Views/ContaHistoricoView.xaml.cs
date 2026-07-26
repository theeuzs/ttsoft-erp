using System;
using System.Windows;

namespace ERP.WPF.Views;

public partial class ContaHistoricoView : Window
{
    public ContaHistoricoView(Guid contaId, string titulo)
    {
        InitializeComponent();
        DataContext = new ERP.WPF.ViewModels.ContaHistoricoViewModel(contaId, titulo);
    }
}