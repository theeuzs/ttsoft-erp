using ERP.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ERP.WPF.Views;

public partial class LoginView : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        // Se o ViewModel disser que logou, a tela fecha e devolve "True" pro sistema abrir o Menu
        _viewModel.OnLoginResult += (s, sucesso) =>
        {
            if (sucesso)
            {
                this.DialogResult = true;
                this.Close();
            }
        };
    }

    // Passa a senha digitada pro ViewModel em tempo real
    private void TxtSenha_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            vm.Senha = ((PasswordBox)sender).Password;
            // Força o botão a checar se pode habilitar
            CommandManager.InvalidateRequerySuggested(); 
        }
    }

    // Botão do "X" no canto superior
    private void BtnSair_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }
}