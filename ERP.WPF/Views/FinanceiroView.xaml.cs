using System.Windows.Controls; // 👈 ESSA LINHA RESOLVE O ERRO VERMELHO!

namespace ERP.WPF.Views;

public partial class FinanceiroView : UserControl
{
    public FinanceiroView()
    {
        InitializeComponent();
        // NÃO coloque DataContext aqui, o CreateView já faz isso automaticamente!
    }
}