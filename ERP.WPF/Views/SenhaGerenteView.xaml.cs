using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Linq;
using System.Windows;

namespace ERP.WPF.Views
{
    public partial class SenhaGerenteView : Window
    {
        // ── Resultado público ─────────────────────────────────────────────────
        public bool   Autorizado       { get; private set; } = false;
        public string AutorizadorNome  { get; private set; } = string.Empty;
        public Guid   AutorizadorId    { get; private set; }

        // Contexto da operação para log de auditoria
        public string Contexto { get; set; } = "operação restrita";

        private User? _usuarioSelecionado;

        public SenhaGerenteView()
        {
            InitializeComponent();
            CarregarUsuariosAutorizadores();
        }

        private void CarregarUsuariosAutorizadores()
        {
            try
            {
                using var scope = App.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Carrega apenas usuários com autoridade (MaxSangriaValue > 0 = Supervisor ou superior)
                var usuarios = db.Users
                    .Include(u => u.Role)
                    .Where(u => u.IsActive
                             && u.Role != null
                             && u.Role.MaxSangriaValue > 0)
                    .OrderBy(u => u.Name)
                    .ToList();

                CmbUsuario.ItemsSource   = usuarios;
                CmbUsuario.DisplayMemberPath = "Name";

                // Pré-seleciona o usuário logado se tiver autoridade
                var idAtual = State.AppSession.UserId;
                var atual   = usuarios.FirstOrDefault(u => u.Id == idAtual);
                if (atual != null)
                    CmbUsuario.SelectedItem = atual;
                else if (usuarios.Count == 1)
                    CmbUsuario.SelectedItem = usuarios[0];

                TxtContexto.Text = $"Autorizar: {Contexto}";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao carregar usuários autorizadores na SenhaGerenteView");
            }
        }

        private void CmbUsuario_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _usuarioSelecionado = CmbUsuario.SelectedItem as User;
            TxtSenha.IsEnabled  = _usuarioSelecionado != null;
            TxtSenha.Clear();
            TxtErro.Visibility  = Visibility.Collapsed;

            if (TxtSenha.IsEnabled)
                TxtSenha.Focus();
        }

        private void BtnAutorizar_Click(object sender, RoutedEventArgs e)
        {
            TxtErro.Visibility = Visibility.Collapsed;

            if (_usuarioSelecionado == null)
            {
                MostrarErro("Selecione o usuário autorizador.");
                return;
            }

            var senha = TxtSenha.Password;
            if (string.IsNullOrWhiteSpace(senha))
            {
                MostrarErro("Digite a senha.");
                TxtSenha.Focus();
                return;
            }

            try
            {
                if (!BCrypt.Net.BCrypt.Verify(senha, _usuarioSelecionado.PasswordHash))
                {
                    MostrarErro("Senha incorreta. Tente novamente.");
                    Log.Warning(
                        "Autorização negada — senha incorreta. Usuário tentante: {Tentante}, Autorizador tentado: {Autorizador}, Contexto: {Ctx}",
                        State.AppSession.UserName, _usuarioSelecionado.Name, Contexto);
                    TxtSenha.Clear();
                    TxtSenha.Focus();
                    return;
                }

                // ── Autorizado ────────────────────────────────────────────────
                Autorizado      = true;
                AutorizadorNome = _usuarioSelecionado.Name;
                AutorizadorId   = _usuarioSelecionado.Id;

                Log.Information(
                    "Autorização concedida. Operador: {Operador} | Autorizador: {Autorizador} | Contexto: {Ctx}",
                    State.AppSession.UserName, AutorizadorNome, Contexto);

                this.Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Erro ao verificar senha na SenhaGerenteView");
                MostrarErro("Erro ao verificar credenciais. Tente novamente.");
                TxtSenha.Clear();
                TxtSenha.Focus();
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Autorizado = false;
            Log.Information(
                "Autorização cancelada pelo operador {Operador}. Contexto: {Ctx}",
                State.AppSession.UserName, Contexto);
            this.Close();
        }

        private void MostrarErro(string msg)
        {
            TxtErro.Text       = msg;
            TxtErro.Visibility = Visibility.Visible;
        }
    }
}
