using ERP.WPF.ViewModels;
using System.Windows;

namespace ERP.WPF.Views;

public partial class SetupView : Window
{
    private readonly SetupViewModel _vm;

    public SetupView()
    {
        InitializeComponent();
        _vm = new SetupViewModel();
        DataContext = _vm;

        // Quando o ViewModel sinalizar que salvou, fecha a janela com sucesso
        _vm.ConfiguracaoSalva += () =>
        {
            DialogResult = true;
            Close();
        };
    }

    // PasswordBox não suporta Binding direto por segurança do .NET
    // Então empurramos a senha pro ViewModel manualmente aqui
    private void SenhaBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.Senha = SenhaBox.Password;
    }
}
