using ERP.WPF.ViewModels;
using System.Windows;

namespace ERP.WPF.Views;

public partial class TrocarSenhaView : Window
{
    public TrocarSenhaView()
    {
        InitializeComponent();
    }

    private void PbSenhaAtual_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is TrocarSenhaViewModel vm)
            vm.SenhaAtual = PbSenhaAtual.Password;
    }

    private void PbNovaSenha_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is TrocarSenhaViewModel vm)
            vm.NovaSenha = PbNovaSenha.Password;
    }

    private void PbConfirmar_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is TrocarSenhaViewModel vm)
            vm.ConfirmarNovaSenha = PbConfirmar.Password;
    }

    /// <summary>
    /// Conecta o evento de resultado do ViewModel ao fechamento do Window.
    /// Chamar logo após instanciar e setar o DataContext, antes de ShowDialog().
    /// </summary>
    public void ConectarResultado(TrocarSenhaViewModel vm)
    {
        vm.OnTrocaResult += (s, sucesso) =>
        {
            DialogResult = sucesso;
            Close();
        };
    }
}
