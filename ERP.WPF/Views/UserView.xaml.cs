using ERP.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class UserView : UserControl
{
    public UserView()
    {
        InitializeComponent();
    }

    private void TxtSenha_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserViewModel vm)
        {
            vm.Senha = ((PasswordBox)sender).Password;
        }
    }
}