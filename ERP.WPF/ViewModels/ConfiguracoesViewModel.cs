using ERP.WPF.Commands;
using ERP.WPF.Helpers;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class ConfiguracoesViewModel : BaseViewModel
{
    private string _caminhoLogo = string.Empty;
    public string CaminhoLogo { get => _caminhoLogo; set => SetProperty(ref _caminhoLogo, value); }
    public ICommand RemoverLogoCommand { get; }
    public ICommand SelecionarLogoCommand { get; }
    
    
    private string _razaoSocial = string.Empty;
    public string RazaoSocial { get => _razaoSocial; set => SetProperty(ref _razaoSocial, value); }

    private string _nomeFantasia = string.Empty;
    public string NomeFantasia { get => _nomeFantasia; set => SetProperty(ref _nomeFantasia, value); }

    private string _chavePix = string.Empty;
    public string ChavePix { get => _chavePix; set => SetProperty(ref _chavePix, value); }

    private string _cidadePix = string.Empty;
    public string CidadePix { get => _cidadePix; set => SetProperty(ref _cidadePix, value); }

    private string _telefone = string.Empty;
    public string Telefone { get => _telefone; set => SetProperty(ref _telefone, value); }

    private string _endereco = string.Empty;
    public string Endereco { get => _endereco; set => SetProperty(ref _endereco, value); }

    private string _rodapeLinha1 = string.Empty;
    public string RodapeLinha1 { get => _rodapeLinha1; set => SetProperty(ref _rodapeLinha1, value); }

    private string _rodapeLinha2 = string.Empty;
    public string RodapeLinha2 { get => _rodapeLinha2; set => SetProperty(ref _rodapeLinha2, value); }

    private string _rodapeLinha3 = string.Empty;
    public string RodapeLinha3 { get => _rodapeLinha3; set => SetProperty(ref _rodapeLinha3, value); }
    

    // 👇 NOVAS VARIÁVEIS DA SEFAZ 👇
    private string _cnpjEmpresa = string.Empty;
    /// <summary>CNPJ da própria empresa — item MD-e do roadmap fiscal precisa
    /// disso pra consultar "notas emitidas contra qual CNPJ"; antes só existia
    /// hardcoded no DTO de emissão, nunca configurável.</summary>
    public string CnpjEmpresa { get => _cnpjEmpresa; set => SetProperty(ref _cnpjEmpresa, value); }

    private string _pixApiToken = string.Empty;
    /// <summary>Código morto da auditoria ativado — token do provedor de Pix
    /// pra confirmação automática (PixPollingService).</summary>
    public string PixApiToken { get => _pixApiToken; set => SetProperty(ref _pixApiToken, value); }

    public string[] ProvedoresPix { get; } = { "openpix", "gerencianet" };
    private string _pixProvedor = "openpix";
    public string PixProvedor { get => _pixProvedor; set => SetProperty(ref _pixProvedor, value); }

    private string _balancaComPort = "COM1";
    /// <summary>Código morto da auditoria ativado — porta serial da balança.</summary>
    public string BalancaComPort { get => _balancaComPort; set => SetProperty(ref _balancaComPort, value); }

    private string _tokenFocusNfe = string.Empty;
    public string TokenFocusNfe 
    { 
        get => _tokenFocusNfe; 
        set 
        {
            SetProperty(ref _tokenFocusNfe, value);
            OnPropertyChanged(nameof(TokenMascarado)); // Atualiza a máscara em tempo real!
        } 
    }

    // 🕵️‍♂️ O GERADOR DA MÁSCARA (Ex: FWT5********0vCR)
    public string TokenMascarado
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_tokenFocusNfe)) return string.Empty;
            if (_tokenFocusNfe.Length <= 8) return new string('*', _tokenFocusNfe.Length);
            return $"{_tokenFocusNfe.Substring(0, 4)}********{_tokenFocusNfe.Substring(_tokenFocusNfe.Length - 4)}";
        }
    }

    // Controle do Botão de Mostrar/Ocultar
    private bool _isTokenVisivel = false;
    public bool IsTokenVisivel { get => _isTokenVisivel; set => SetProperty(ref _isTokenVisivel, value); }
    public ICommand ToggleTokenCommand { get; }

    private bool _usarAmbienteProducao;
    public bool UsarAmbienteProducao { get => _usarAmbienteProducao; set => SetProperty(ref _usarAmbienteProducao, value); }

    public ICommand SalvarCommand { get; }
    public ICommand BackupManualCommand { get; } = new RelayCommand(async _ => await BackupService.RealizarBackupManualAsync());

    /// <summary>Fundação da Etapa 2 fiscal — migra o token que hoje está só
    /// no arquivo local (config_recibo.json) pra TenantFiscalConfiguration,
    /// que a API também consegue ler. Usa os providers que já existem
    /// (JsonFiscalConfigurationProvider e DatabaseFiscalConfigurationProvider),
    /// só transfere de um pro outro.</summary>
    public ICommand MigrarParaBancoCommand { get; }

    public ConfiguracoesViewModel()
    {
        var config = ConfiguracaoService.Carregar();
        
        CaminhoLogo = config.CaminhoLogo ?? string.Empty; 
        RazaoSocial = config.RazaoSocial ?? string.Empty;
        NomeFantasia = config.NomeFantasia ?? string.Empty;
        Telefone = config.Telefone ?? string.Empty;
        Endereco = config.Endereco ?? string.Empty;
        RodapeLinha1 = config.RodapeLinha1 ?? string.Empty;
        RodapeLinha2 = config.RodapeLinha2 ?? string.Empty;
        RodapeLinha3 = config.RodapeLinha3 ?? string.Empty;
        ChavePix  = config.ChavePix  ?? string.Empty;
        CidadePix = config.CidadePix ?? string.Empty;
        
        // 👇 CARREGA OS DADOS DA SEFAZ 👇
        TokenFocusNfe = config.TokenFocusNfe ?? string.Empty;
        UsarAmbienteProducao = config.UsarAmbienteProducao;
        BalancaComPort = config.BalancaComPort ?? "COM1";
        PixApiToken = config.PixApiToken ?? string.Empty;
        PixProvedor = config.PixProvedor ?? "openpix";

        SelecionarLogoCommand = new RelayCommand(_ => SelecionarLogo());
        RemoverLogoCommand = new RelayCommand(_ => CaminhoLogo = string.Empty);
        ToggleTokenCommand = new RelayCommand(_ => IsTokenVisivel = !IsTokenVisivel); // Alterna o olhinho
        SalvarCommand = new RelayCommand(_ => Salvar());
        MigrarParaBancoCommand = new RelayCommand(async _ => await MigrarParaBancoAsync());
        
    }

    private async Task MigrarParaBancoAsync()
    {
        // Salva primeiro no arquivo local (garante que o que está na tela —
        // possivelmente ainda não salvo — é o que vai pro banco).
        Salvar();

        var confirmacao = MessageBox.Show(
            $"Isso vai copiar o token da Focus NFe atual (terminando em ...{(TokenFocusNfe.Length > 4 ? TokenFocusNfe[^4..] : TokenFocusNfe)}) " +
            $"e o ambiente ({(UsarAmbienteProducao ? "Produção" : "Homologação")}) pro banco de dados, " +
            "pra que a API (e não só esse computador) consiga emitir nota fiscal.\n\nContinuar?",
            "Migrar Configuração Fiscal", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmacao != MessageBoxResult.Yes) return;

        try
        {
            var ctx    = ERP.WPF.App.Services.GetRequiredService<ERP.Persistence.Context.AppDbContext>();
            var tenant = ERP.WPF.App.Services.GetRequiredService<ERP.Application.Interfaces.IRequestTenant>();
            var dbProvider = new ERP.Infrastructure.Services.DatabaseFiscalConfigurationProvider(ctx, tenant);

            await dbProvider.SalvarConfiguracaoAsync(new ERP.Application.Interfaces.FiscalConfiguration
            {
                TokenFocusNfe        = TokenFocusNfe,
                UsarAmbienteProducao = UsarAmbienteProducao,
                Cnpj                 = CnpjEmpresa
            });

            MessageBox.Show("✅ Configuração fiscal migrada pro banco com sucesso!", "TTSoft ERP",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"❌ Falha ao migrar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Salvar()
    {
        var config = new ReciboConfig
        {
            CaminhoLogo = this.CaminhoLogo,
            RazaoSocial = this.RazaoSocial,
            NomeFantasia = this.NomeFantasia,
            Telefone = this.Telefone,
            Endereco = this.Endereco,
            RodapeLinha1 = this.RodapeLinha1,
            RodapeLinha2 = this.RodapeLinha2,
            RodapeLinha3 = this.RodapeLinha3,
            ChavePix  = this.ChavePix,
            CidadePix = this.CidadePix,
            
            // 👇 SALVA OS DADOS DA SEFAZ 👇
            TokenFocusNfe = this.TokenFocusNfe,
            UsarAmbienteProducao = this.UsarAmbienteProducao,
            BalancaComPort = this.BalancaComPort,
            PixApiToken = this.PixApiToken,
            PixProvedor = this.PixProvedor
        };
        
        ConfiguracaoService.Salvar(config);
        
        MessageBox.Show("✅ Configurações salvas com segurança!", "TTSoft ERP", MessageBoxButton.OK, MessageBoxImage.Information);
        
        // Esconde o token novamente após salvar por segurança
        IsTokenVisivel = false; 
    }

    private void SelecionarLogo()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecione a Logo da Empresa",
            Filter = "Imagens|*.jpg;*.jpeg;*.png;*.bmp"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                string pastaDestino = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets");
                if (!System.IO.Directory.Exists(pastaDestino)) System.IO.Directory.CreateDirectory(pastaDestino);

                string extensao = System.IO.Path.GetExtension(openFileDialog.FileName);
                string arquivoDestino = System.IO.Path.Combine(pastaDestino, $"logo_cliente{extensao}");
                
                System.IO.File.Copy(openFileDialog.FileName, arquivoDestino, true);
                CaminhoLogo = arquivoDestino;
            }
            catch
            {
                CaminhoLogo = openFileDialog.FileName;
            }
        }
    }
}