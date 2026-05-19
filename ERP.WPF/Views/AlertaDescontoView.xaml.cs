using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.Persistence.Context;

namespace ERP.WPF.Views;

public partial class AlertaDescontoView : Window
{
    // Variável que vai guardar quanto o gerente que logou pode dar de desconto
    public decimal LimiteLiberado { get; private set; }

    public AlertaDescontoView()
    {
        InitializeComponent();
        CarregarGerentes();
    }

    private void CarregarGerentes()
    {
        using (var scope = App.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gerentes = dbContext.Users.Include(u => u.Role)
                // Qualquer usuário com cargo de Supervisor ou superior pode autorizar
                // (MaxSangriaValue > 0 exclui o cargo Vendedor automaticamente)
                .Where(u => u.Role != null && u.Role.MaxSangriaValue > 0)
                .ToList();
            CmbGerentes.ItemsSource = gerentes;
        }
    }

    private void BtnAutorizar_Click(object sender, RoutedEventArgs e)
    {
        var gerenteSelecionado = CmbGerentes.SelectedItem as ERP.Domain.Entities.User;
        if (gerenteSelecionado == null) return;

        if (BCrypt.Net.BCrypt.Verify(TxtSenhaGerente.Password, gerenteSelecionado.PasswordHash))
        {
            // Pega o limite do gerente que acabou de colocar a senha!
            LimiteLiberado = gerenteSelecionado.Role?.MaxDiscountPercentage ?? 0;
            this.DialogResult = true; 
        }
        else
        {
            MessageBox.Show("Senha incorreta!", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        this.DialogResult = false;
    }
}