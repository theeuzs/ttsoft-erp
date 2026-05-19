using System.Windows;

namespace ERP.WPF.Views;

public partial class FidelidadeView : Window
{
    public decimal DescontoAplicado { get; private set; } = 0;

    public FidelidadeView(ViewModels.FidelidadeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.OnConfirmado = desconto =>
        {
            DescontoAplicado = desconto;
            DialogResult     = true;
            Close();
        };
    }

    private void BtnFechar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
