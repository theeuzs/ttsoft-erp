using Serilog;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace ERP.WPF.Views;

public partial class ProductView : UserControl
{
    public ProductView()
    {
        InitializeComponent();
    }

    // async void obrigatório — event handler WPF (KeyEventHandler)
    // CORREÇÃO BUG #5: ProcessarLeituraDeCodigoAsync pode lançar exceção (timeout,
    // produto não encontrado, etc.). Sem try/catch isso derrubava silenciosamente.
    private async void TxtNomeProduto_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        var txt = sender as TextBox;
        if (txt == null || string.IsNullOrWhiteSpace(txt.Text)) return;

        string input = txt.Text.Trim();

        if (input.All(char.IsDigit) && input.Length >= 3)
        {
            e.Handled = true;

            if (DataContext is ViewModels.ProductViewModel vm)
            {
                try
                {
                    await vm.ProcessarLeituraDeCodigoAsync(input);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Falha ao processar código de barras '{Codigo}'", input);
                    // Não exibe MessageBox — falha silenciosa mantém o foco no campo
                    // para o usuário tentar novamente
                }

                txt.Focus();
            }
        }
        else
        {
            var request = new TraversalRequest(FocusNavigationDirection.Next) { Wrapped = true };
            txt.MoveFocus(request);
            e.Handled = true;
        }
    }
}
