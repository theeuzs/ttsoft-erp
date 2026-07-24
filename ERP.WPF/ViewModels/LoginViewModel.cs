using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using ERP.WPF.State;
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

                // S10 FIX: Obtém JWT da API para uso no ChatService (melhor esforço).
                // Se a API estiver indisponível, o chat fica offline mas o sistema funciona.
                _ = ObterJwtDaApiAsync(cnpjCliente, this.Usuario, this.Senha);

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

    // S10 FIX: Obtém JWT da API após auth local — usado pelo ChatService para
    // autenticar no ERPChatHub. Fire-and-forget: falha não impede o login.
    private static async Task ObterJwtDaApiAsync(string cnpj, string usuario, string senha)
    {
        var apiUrl = AppSession.ApiBaseUrl;
        if (string.IsNullOrEmpty(apiUrl)) return;

        try
        {
            using var http = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            http.DefaultRequestHeaders.Add("X-Tenant-CNPJ", cnpj);

            var body = new { username = usuario, password = senha };
            var resp = await http.PostAsJsonAsync($"{apiUrl}/api/auth/login", body);

            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                var jwt  = json.GetProperty("accessToken").GetString();
                if (!string.IsNullOrEmpty(jwt))
                    AppSession.JwtToken = jwt;
            }
        }
        catch { /* Chat offline — não impede o login */ }
    }
}