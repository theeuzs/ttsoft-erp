using System;
using System.Windows;
using ERP.WPF.Security; // Lembre de colocar o using da pasta onde está o LicenseManager

namespace ERP.WPF.Views // Adapte para o namespace real do seu projeto
{
    public partial class LicencaWindow : Window
    {
        public LicencaWindow()
        {
            InitializeComponent();
            CarregarDadosDaLicenca();
        }

        private void CarregarDadosDaLicenca()
        {
            // Puxa o ID da máquina
            txtMachineId.Text = MachineFingerprint.GetMachineId();

            // Calcula os dias restantes
            int diasRestantes = (LicenseManager.DataVencimento - DateTime.Now).Days;

            // Preenche os textos na tela XAML
            txtStatus.Text = LicenseManager.StatusAtual.ToUpper();
            txtVencimento.Text = LicenseManager.DataVencimento.ToString("dd/MM/yyyy");
            txtDias.Text = $"{diasRestantes} dias";

            // Muda a cor se estiver perto de vencer (menos de 5 dias)
            if (diasRestantes <= 5)
            {
                txtDias.Foreground = System.Windows.Media.Brushes.Red;
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void BtnFechar_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // Fecha a janelinha
        }
    }
}