using ERP.WPF.Services;
using Serilog;
using System.Windows;

namespace ERP.WPF.Views
{
    public partial class UpdateView : Window
    {
        private readonly VersaoInfo _info;

        public UpdateView(VersaoInfo info)
        {
            InitializeComponent();
            _info = info;
            TxtVersao.Text = $"Versão {info.VersaoAtual}  —  {info.DataLancamento}";
            TxtNotas.Text  = info.Notas;
        }

        private void BtnDepois_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        // async void é obrigatório para event handlers WPF (ICommand.Execute / eventos de UI)
        // CORREÇÃO BUG #5: adicionado try/catch — sem ele, uma exceção aqui derruba
        // o AppDomain sem ser observada, pois async void não tem awaiter externo.
        private async void BtnAtualizar_Click(object sender, RoutedEventArgs e)
        {
            BtnAtualizar.IsEnabled  = false;
            BtnDepois.IsEnabled     = false;
            PnlBotoes.Visibility    = Visibility.Collapsed;
            PgbDownload.Visibility  = Visibility.Visible;
            TxtProgresso.Visibility = Visibility.Visible;
            TxtProgresso.Text       = "Baixando... 0%";

            try
            {
                bool atualizou = await UpdateService.BaixarEAplicarAsync(_info, pct =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        PgbDownload.Value = pct;
                        TxtProgresso.Text = $"Baixando... {pct}%";
                    });
                });

                DialogResult = atualizou;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao baixar/aplicar atualização v{Versao}", _info.VersaoAtual);

                // Restaura UI para o usuário tentar novamente
                BtnAtualizar.IsEnabled  = true;
                BtnDepois.IsEnabled     = true;
                PnlBotoes.Visibility    = Visibility.Visible;
                PgbDownload.Visibility  = Visibility.Collapsed;
                TxtProgresso.Visibility = Visibility.Collapsed;

                MessageBox.Show(
                    $"Não foi possível baixar a atualização:\n\n{ex.Message}\n\nTente novamente ou atualize manualmente.",
                    "Erro na atualização", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
