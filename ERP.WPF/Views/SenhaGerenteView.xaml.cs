using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;

namespace ERP.WPF.Views
{
    public partial class SenhaGerenteView : Window
    {
        // ── Resultado público ─────────────────────────────────────────────────
        public bool   Autorizado       { get; private set; } = false;
        public string AutorizadorNome  { get; private set; } = string.Empty;
        public Guid   AutorizadorId    { get; private set; }

        // S{N} FIX — achado testando Fase C: token JWT de verdade do
        // autorizador (gerente/admin), obtido via /api/auth/login. Antes
        // esta tela só verificava a senha LOCALMENTE (BCrypt contra um hash
        // baixado do banco pro cliente) e devolvia um bool solto —
        // Autorizado=true nunca provava nada pra API. A chamada de
        // RegistrarMovimentoAsync que vinha depois continuava usando o
        // token de QUEM ESTAVA LOGADO (o Vendedor), sem a permissão
        // cash.sangria, e a API corretamente barrava com 403 mesmo com a
        // senha certa digitada aqui. Agora, "autorizar" É logar de verdade
        // como esse usuário — o token resultante tem as permissões reais
        // dele, e quem chama esta tela (ResumoCaixaViewModel) troca a sessão
        // por este token só durante a chamada que precisa da autorização.
        public string? TokenAutorizador { get; private set; }

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

                // S{N} FIX — projeta só Id/Name/Username. Antes trazia a
                // entidade User inteira (via .ToList() sem .Select()), o que
                // incluía PasswordHash de todo mundo com autoridade — hash
                // de senha não tem por que nunca chegar na memória do
                // cliente, ainda mais agora que a verificação nem usa mais
                // isso (ver BtnAutorizar_Click).
                var usuarios = db.Users
                    .Include(u => u.Role)
                    .Where(u => u.IsActive
                             && u.Role != null
                             && u.Role.MaxSangriaValue > 0)
                    .OrderBy(u => u.Name)
                    .Select(u => new User { Id = u.Id, Name = u.Name, Username = u.Username })
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

        private async void BtnAutorizar_Click(object sender, RoutedEventArgs e)
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

            BtnAutorizar.IsEnabled = false;
            try
            {
                // S{N} FIX — login de verdade contra a API, mesmo endpoint e
                // mesmo formato que o LoginViewModel já usa. Isso substitui
                // o BCrypt.Verify local: a senha só é validada pelo
                // servidor (fonte da verdade), e o resultado é um JWT real
                // com as permissões do autorizador — não um bool solto.
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                http.DefaultRequestHeaders.Add("X-Tenant-CNPJ", State.AppSession.TenantCnpj);

                var resp = await http.PostAsJsonAsync(
                    $"{State.AppSession.ApiBaseUrl}/api/auth/login",
                    new { username = _usuarioSelecionado.Username, password = senha });

                if (!resp.IsSuccessStatusCode)
                {
                    MostrarErro("Senha incorreta. Tente novamente.");
                    Log.Warning(
                        "Autorização negada — senha incorreta. Usuário tentante: {Tentante}, Autorizador tentado: {Autorizador}, Contexto: {Ctx}",
                        State.AppSession.UserName, _usuarioSelecionado.Name, Contexto);
                    TxtSenha.Clear();
                    TxtSenha.Focus();
                    return;
                }

                var json = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                TokenAutorizador = json.GetProperty("accessToken").GetString();

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
                Log.Error(ex, "Erro ao verificar credenciais na SenhaGerenteView");
                MostrarErro("Erro ao verificar credenciais. Verifique a conexão e tente novamente.");
                TxtSenha.Clear();
                TxtSenha.Focus();
            }
            finally
            {
                BtnAutorizar.IsEnabled = true;
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