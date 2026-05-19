using Serilog;
using System.Windows;

namespace ERP.WPF.Views;

public partial class LogoutDialog : Window
{
    public LogoutDialog()
    {
        InitializeComponent();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // async void obrigatório — event handler WPF
    // CORREÇÃO BUG #5: sem try/catch, uma falha no backup derrubaria o processo
    // silenciosamente. Com o catch, o usuário recebe feedback e pode tentar sair mesmo assim.
    private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        PanelBotoes.Visibility  = Visibility.Collapsed;
        PanelLoading.Visibility = Visibility.Visible;

        try
        {
            await ERP.WPF.Helpers.BackupService.RealizarBackupAutomaticoAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Falha no backup automático ao logout");

            // Permite sair mesmo com falha no backup — o usuário não fica preso
            var resposta = MessageBox.Show(
                $"O backup automático falhou:\n\n{ex.Message}\n\nDeseja sair mesmo assim?",
                "Aviso de backup", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            DialogResult = resposta == MessageBoxResult.Yes;
        }
    }
}
