using ERP.Domain.Enums;
using System;
using System.Windows;

namespace ERP.WPF.Views;

public partial class RegistrarContatoView : Window
{
    public StatusFollowUp StatusEscolhido      { get; private set; }
    public string?        ObservacaoEscolhida  { get; private set; }
    public string?        MotivoPerdaEscolhido { get; private set; }
    public int?           ProximoFollowUpEmDiasEscolhido { get; private set; }
    public bool           Confirmado           { get; private set; } = false;

    public RegistrarContatoView(string numeroOrcamento, string clienteNome)
    {
        InitializeComponent();
        TxtOrcamentoInfo.Text = $"{numeroOrcamento} — {clienteNome}";

        // S17 FIX: setar IsChecked=True direto no XAML disparava o evento Checked
        // DURANTE o parsing, antes de PainelReagendar/PainelMotivoPerda (declarados
        // depois no XAML) existirem — NullReferenceException. Setar aqui, depois
        // do InitializeComponent, garante que tudo já foi construído.
        RbContatado.IsChecked = true;
        AtualizarCamposVisiveis(this, null!);
    }

    private void AtualizarCamposVisiveis(object sender, RoutedEventArgs e)
    {
        // Só faz sentido reagendar quando o resultado foi "apenas contatei" —
        // Convertido/Perdido são estados finais, não tem follow-up depois.
        PainelReagendar.Visibility = RbContatado.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        PainelMotivoPerda.Visibility = RbPerdido.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Confirmar_Click(object sender, RoutedEventArgs e)
    {
        if (RbPerdido.IsChecked == true && string.IsNullOrWhiteSpace(TxtMotivoPerda.Text))
        {
            MessageBox.Show("Informe o motivo da perda.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusEscolhido = RbConvertido.IsChecked == true ? StatusFollowUp.Convertido
                         : RbPerdido.IsChecked == true    ? StatusFollowUp.Perdido
                         : StatusFollowUp.Contatado;

        ObservacaoEscolhida  = string.IsNullOrWhiteSpace(TxtObservacao.Text) ? null : TxtObservacao.Text.Trim();
        MotivoPerdaEscolhido = RbPerdido.IsChecked == true && !string.IsNullOrWhiteSpace(TxtMotivoPerda.Text)
            ? TxtMotivoPerda.Text.Trim() : null;

        if (RbContatado.IsChecked == true
            && int.TryParse(TxtProximoFollowUpDias.Text, out var dias)
            && dias > 0)
        {
            ProximoFollowUpEmDiasEscolhido = dias;
        }

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
