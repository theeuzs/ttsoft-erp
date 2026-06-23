using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using ERP.WPF.State;
using System;
using System.Linq;
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
        
        // O botão de login só libera se tiver algo digitado no usuário
        LoginCommand = new AsyncRelayCommand(_ => RealizarLoginAsync(), _ => !string.IsNullOrWhiteSpace(Usuario));
        
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

    public ICommand LoginCommand { get; }

    private async Task RealizarLoginAsync()
    {
        IsBusy = true;
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
            IsBusy = false;
        }
    }
}