using ERP.WPF.Commands;
using ERP.WPF.Helpers;
using System.Data.SqlClient;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class SetupViewModel : INotifyPropertyChanged
{
    // ── Propriedades do formulário ────────────────────────────────────────

    private string _servidor = @"localhost\SQLEXPRESS";
    public string Servidor
    {
        get => _servidor;
        set { _servidor = value; OnPropertyChanged(); AtualizarPreview(); }
    }

    private string _banco = "ERPMateriais";
    public string Banco
    {
        get => _banco;
        set { _banco = value; OnPropertyChanged(); AtualizarPreview(); }
    }

    private string _usuario = "sa";
    public string Usuario
    {
        get => _usuario;
        set { _usuario = value; OnPropertyChanged(); AtualizarPreview(); }
    }

    private string _senha = string.Empty;
    public string Senha
    {
        get => _senha;
        set { _senha = value; OnPropertyChanged(); AtualizarPreview(); }
    }

    private string _statusMensagem = "Preencha os dados e clique em Testar Conexão.";
    public string StatusMensagem
    {
        get => _statusMensagem;
        set { _statusMensagem = value; OnPropertyChanged(); }
    }

    private bool _conexaoOk = false;
    public bool ConexaoOk
    {
        get => _conexaoOk;
        set { _conexaoOk = value; OnPropertyChanged(); }
    }

    private bool _testando = false;
    public bool Testando
    {
        get => _testando;
        set { _testando = value; OnPropertyChanged(); }
    }

    private string _preview = string.Empty;
    public string Preview
    {
        get => _preview;
        private set { _preview = value; OnPropertyChanged(); }
    }

    // ── Eventos ───────────────────────────────────────────────────────────

    /// <summary>Disparado quando o usuário salva com sucesso — o App.xaml.cs escuta isso.</summary>
    public event Action? ConfiguracaoSalva;

    // ── Comandos ──────────────────────────────────────────────────────────

    public ICommand TestarConexaoCommand { get; }
    public ICommand SalvarCommand { get; }

    public SetupViewModel()
    {
        TestarConexaoCommand = new RelayCommand(async _ => await TestarConexaoAsync(), _ => !Testando);
        SalvarCommand        = new RelayCommand(_ => Salvar(), _ => ConexaoOk);
        AtualizarPreview();
    }

    // ── Lógica ────────────────────────────────────────────────────────────

    private void AtualizarPreview()
    {
        // Mostra um preview da connection string (com senha mascarada) para o usuário confirmar
        Preview = $"Server={Servidor};Database={Banco};User Id={Usuario};Password=***;TrustServerCertificate=True;";
        ConexaoOk = false; // qualquer mudança invalida o teste anterior
        StatusMensagem = "Clique em Testar Conexão para validar.";
    }

    private string MontarConnectionString()
        => $"Server={Servidor};Database={Banco};User Id={Usuario};Password={Senha};TrustServerCertificate=True;";

    private async Task TestarConexaoAsync()
    {
        Testando = true;
        ConexaoOk = false;
        StatusMensagem = "Conectando ao banco de dados...";

        try
        {
            await Task.Run(async () =>
            {
                using var conn = new SqlConnection(MontarConnectionString());
                await conn.OpenAsync(); // Lança exceção se falhar
            });

            ConexaoOk = true;
            StatusMensagem = "✔ Conexão bem-sucedida! Clique em Salvar para continuar.";
        }
        catch (Exception ex)
        {
            ConexaoOk = false;
            StatusMensagem = $"✘ Falha: {ex.Message}";
        }
        finally
        {
            Testando = false;
        }
    }

    private void Salvar()
    {
        try
        {
            SecureConfigService.Salvar(MontarConnectionString());
            ConfiguracaoSalva?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar configuração:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
