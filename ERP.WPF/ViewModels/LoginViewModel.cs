using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using ERP.WPF.State;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    
    // Evento para avisar a tela que o login deu certo e ela pode fechar
    public event EventHandler<bool> OnLoginResult;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        
        // MUDANÇA: O botão agora só libera se tiver usuário E não estiver processando (IsBusy = false)
        LoginCommand = new AsyncRelayCommand(
            _ => RealizarLoginAsync(), 
            _ => !string.IsNullOrWhiteSpace(Usuario) && !IsBusy);
        
        // MÁGICA: Assim que a tela abre, ele garante que o usuário "admin" exista no banco!
        _ = _authService.EnsureDefaultAdminCreatedAsync();
    }

    private string _usuario = string.Empty;
    public string Usuario 
    { 
        get => _usuario; 
        set 
        { 
            SetProperty(ref _usuario, value); 
            CommandManager.InvalidateRequerySuggested(); 
        } 
    }
    
    // A senha não usa Binding automático por segurança do WPF, ela vem do Code-Behind
    public string Senha { get; set; } = string.Empty;

    private string _mensagemErro = string.Empty;
    public string MensagemErro 
    { 
        get => _mensagemErro; 
        set { SetProperty(ref _mensagemErro, value); OnPropertyChanged(nameof(TemErro)); } 
    }
    
    public bool TemErro => !string.IsNullOrEmpty(MensagemErro);

    // MUDANÇA: Sobrescrevemos (ou ocultamos) o IsBusy do BaseViewModel para forçar
    // o CommandManager a reavaliar o botão (ativar/desativar) instantaneamente.
    public new bool IsBusy
    {
        get => base.IsBusy;
        set
        {
            base.IsBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public ICommand LoginCommand { get; }

    private async Task RealizarLoginAsync()
    {
        IsBusy = true; // Inicia a animação do Spinner e desativa o botão
        MensagemErro = string.Empty;

        try
        {
            // ========================================================
            // 🔒 TRAVA DE LICENÇA (PHONE HOME) DINÂMICA
            // ========================================================
            string cnpjCliente = "";
            try
            {
                // Lê o arquivinho licenca.json que está na mesma pasta do sistema
                string caminhoArquivo = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "licenca.json");
                string conteudoJson = System.IO.File.ReadAllText(caminhoArquivo);
                
                // Extrai só o CNPJ lá de dentro
                var config = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(conteudoJson);
                if (config != null && config.ContainsKey("Cnpj"))
                {
                    cnpjCliente = config["Cnpj"];
                }

                // S{N} FIX — achado testando Fase C: guarda o CNPJ na sessão
                // pra SenhaGerenteView conseguir logar como o autorizador
                // mais tarde (antes só existia essa variável local, perdida
                // assim que o login terminava).
                ERP.WPF.State.AppSession.TenantCnpj = cnpjCliente;
            }
            catch
            {
                System.Windows.MessageBox.Show("Arquivo de licença (licenca.json) não encontrado ou corrompido!", "Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                IsBusy = false;
                return;
            }

            // 👇 MUDANÇA: Agora recebe a Tupla (IsValid e DataVencimento)
            var resultadoLicenca = await ERP.WPF.Security.LicenseManager.VerificarLicencaAsync(cnpjCliente);
            
            if (!resultadoLicenca.IsValid) // 👇 Verifica se é válido
            {
                // 👇 PEGA O CÓDIGO DA MÁQUINA PARA MOSTRAR NA TELA 👇
                string codigoDestaMaquina = ERP.WPF.Security.MachineFingerprint.GetMachineId();

                System.Windows.MessageBox.Show(
                    $"SISTEMA NÃO AUTORIZADO!\n\nSua licença expirou ou esta máquina não está registrada.\n\n" +
                    $"Tire uma foto desta tela e envie para o suporte TTSoft:\n" +
                    $"CNPJ: {cnpjCliente}\n" +
                    $"MÁQUINA: {codigoDestaMaquina}", 
                    "Acesso Negado", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
                
                IsBusy = false;
                return; // Chuta o usuário daqui e aborta o login!
            }
            // ========================================================

            // Deriva o TenantId do CNPJ — mesma lógica do TenantHelper.FromCnpj da API.
            // Inline aqui para não criar dependência de ERP.Api no WPF.
            var cnpjDigits = new string(cnpjCliente.Where(char.IsDigit).ToArray());
            using var sha      = System.Security.Cryptography.SHA256.Create();
            var hashBytes      = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(cnpjDigits));
            var guidBytes      = new byte[16];
            System.Array.Copy(hashBytes, guidBytes, 16);
            var tenantId = new Guid(guidBytes);

            // Se passou da trava, faz o login normal no banco de dados local...
            var dto      = new LoginDto { Username = this.Usuario, Password = this.Senha };
            var resultado = await _authService.LoginAsync(dto, tenantId);

            if (resultado.Sucedeu && resultado.Usuario is { } user)
            {
                // 2.5 — MustChangePassword: bloqueia entrada no sistema até a senha
                // ser trocada. Antes desta correção, o WPF ignorava completamente
                // a flag e dava acesso total mesmo com a senha padrão "admin123"
                // — bypass total da política que a API já enforçava via middleware.
                if (resultado.MustChangePassword)
                {
                    var trocarVm  = new TrocarSenhaViewModel(_authService, user.Id);
                    var trocarWin = new ERP.WPF.Views.TrocarSenhaView { DataContext = trocarVm };
                    trocarWin.ConectarResultado(trocarVm);

                    var trocou = trocarWin.ShowDialog();
                    if (trocou != true)
                    {
                        MensagemErro = "Você precisa trocar a senha para continuar.";
                        return; // fecha o modal sem trocar = não entra no sistema
                    }
                    // Senha trocada com sucesso — segue o login normalmente
                    // com os dados de 'user' já obtidos no LoginAsync original.
                }

                AppSession.Login(
                    user.Id,
                    user.Name,
                    user.RoleName,
                    user.Permissions ?? new System.Collections.Generic.List<string>(),
                    user.MaxDiscountPercentage,
                    user.MaxSangriaValue);

                ERP.Persistence.Context.AppDbContext.SetCurrentUser(user.Id, user.Name);
                ERP.WPF.State.AppSession.DataVencimentoLicenca = resultadoLicenca.DataVencimento;

                // S10 FIX (original): Obtém JWT da API para uso no ChatService (melhor esforço).
                // S23 FIX (17/08): esse mesmo AppSession.JwtToken virou crítico pra
                // Vendas/Histórico desde a Fase B (HttpSaleService), mas continuava
                // fire-and-forget com falha silenciosa — login "funcionava" sem
                // avisar nada, e só quebrava na hora de fechar uma venda (401
                // confuso). Agora espera o resultado, com retry pra tolerar cold
                // start do Azure App Service, e avisa claramente se não conseguir.
                bool jwtObtido = await ObterJwtDaApiComRetryAsync(cnpjCliente, this.Usuario, this.Senha);

                if (!jwtObtido)
                {
                    System.Windows.MessageBox.Show(
                        "Login local concluído, mas não foi possível conectar à API (o servidor pode estar iniciando).\n\n" +
                        "Vendas e Histórico não vão funcionar até isso ser resolvido — tente novamente em alguns segundos ou reabra o sistema.",
                        "Aviso de Conexão", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }

                // S{N} FIX — achado testando Fase C: PdvViewModel é Singleton,
                // então VerificarCaixaAbertoAsync (dentro dele) só rodava na
                // primeira vez que o app abria. Login de um SEGUNDO usuário,
                // na mesma sessão do app (troca de turno sem fechar o
                // sistema), deixava a tela do PDV com o estado de caixa do
                // usuário ANTERIOR. Chama de novo aqui, pra cada login.
                // Best-effort: se falhar (API fora do ar), não trava o
                // login — o próprio método já loga o erro internamente.
                try
                {
                    var pdv = ERP.WPF.App.Services.GetRequiredService<PdvViewModel>();
                    await pdv.RefrescarEstadoDeLoginAsync();
                }
                catch { /* best-effort — não impede o login de completar */ }

                OnLoginResult?.Invoke(this, true);
            }
            else
            {
                MensagemErro = resultado.Mensagem ?? "Usuário ou senha incorretos.";
            }
        }
        catch (Exception ex)
        {
            MensagemErro = $"Erro de conexão: {ex.Message}";
        }
        finally
        {
            IsBusy = false; // Finaliza a animação do Spinner e reativa o botão
        }
    }

    // S23 FIX (17/08): retry pra tolerar cold start do Azure App Service —
    // sem isso, um login logo após o servidor acordar (ou logo após um
    // deploy/restart) tinha boa chance de nunca conseguir o token.
    private static async Task<bool> ObterJwtDaApiComRetryAsync(string cnpj, string usuario, string senha)
    {
        const int tentativas = 2;
        for (int i = 1; i <= tentativas; i++)
        {
            if (await ObterJwtDaApiAsync(cnpj, usuario, senha))
                return true;

            if (i < tentativas)
                await Task.Delay(TimeSpan.FromSeconds(2));
        }
        return false;
    }

    // S10 FIX: Obtém JWT da API após auth local — usado pelo ChatService para
    // autenticar no ERPChatHub, e (S23) pelo HttpSaleService pra Vendas/Histórico.
    private static async Task<bool> ObterJwtDaApiAsync(string cnpj, string usuario, string senha)
    {
        var apiUrl = AppSession.ApiBaseUrl;
        if (string.IsNullOrEmpty(apiUrl)) return false;

        try
        {
            using var http = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(12) // S23: era 8s — curto demais pra cold start
            };
            http.DefaultRequestHeaders.Add("X-Tenant-CNPJ", cnpj);

            var body = new { username = usuario, password = senha };
            var resp = await http.PostAsJsonAsync($"{apiUrl}/api/auth/login", body);

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                var jwt  = json.GetProperty("accessToken").GetString();
                if (!string.IsNullOrEmpty(jwt))
                {
                    AppSession.JwtToken = jwt;
                    return true;
                }
            }
        }
        catch { /* tentativa falhou — o retry cuida disso */ }

        return false;
    }
}