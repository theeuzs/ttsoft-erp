using ERP.WPF.ViewModels;
using System.Windows;

namespace ERP.WPF.Views;

public partial class SalesReportView : Window
{
    public SalesReportView(SalesReportViewModel viewModel)
    {
        InitializeComponent();
        
        // 👇 É AQUI QUE A MÁGICA ACONTECE: Ligamos a tela (XAML) ao cérebro (ViewModel)
        DataContext = viewModel;
    }
}