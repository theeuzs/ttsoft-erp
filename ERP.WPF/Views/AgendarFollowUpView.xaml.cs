using System;
using System.Windows;

namespace ERP.WPF.Views;

public partial class AgendarFollowUpView : Window
{
    public DateTime DataFollowUpEscolhida { get; private set; }
    public string?  ObservacaoEscolhida   { get; private set; }
    public bool     Confirmado            { get; private set; } = false;

    public AgendarFollowUpView(string numeroOrcamento, string clienteNome)
    {
        InitializeComponent();
        TxtOrcamentoInfo.Text = $"{numeroOrcamento} — {clienteNome}";
        DpData.SelectedDate = DateTime.Today.AddDays(3);
    }

    private void Agendar_Click(object sender, RoutedEventArgs e)
    {
        if (DpData.SelectedDate is null)
        {
            MessageBox.Show("Escolha uma data.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DataFollowUpEscolhida = DpData.SelectedDate.Value;
        ObservacaoEscolhida   = string.IsNullOrWhiteSpace(TxtObservacao.Text) ? null : TxtObservacao.Text.Trim();
        Confirmado = true;
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
