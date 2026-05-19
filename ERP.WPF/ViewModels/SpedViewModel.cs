using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using ERP.WPF.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class SpedViewModel : BaseViewModel
{
    // ── Período ───────────────────────────────────────────────────────────────
    private DateTime _dataInicio = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime DataInicio { get => _dataInicio; set => SetProperty(ref _dataInicio, value); }

    private DateTime _dataFim = DateTime.Today;
    public DateTime DataFim { get => _dataFim; set => SetProperty(ref _dataFim, value); }

    // ── Tipo de arquivo ───────────────────────────────────────────────────────
    private bool _gerarEfdIcms = true;
    public bool GerarEfdIcms
    {
        get => _gerarEfdIcms;
        set { SetProperty(ref _gerarEfdIcms, value); if (value) GerarEfdContrib = false; }
    }

    private bool _gerarEfdContrib;
    public bool GerarEfdContrib
    {
        get => _gerarEfdContrib;
        set { SetProperty(ref _gerarEfdContrib, value); if (value) GerarEfdIcms = false; }
    }

    // ── Campos da empresa (pré-preenchidos da config) ─────────────────────────
    private string _razaoSocial     = string.Empty;
    private string _cnpj            = string.Empty;
    private string _ie              = string.Empty;
    private string _codigoMunicipio = "4106902";
    private string _endereco        = string.Empty;
    private string _contabNome      = string.Empty;
    private string _contabCpf       = string.Empty;
    private string _contabCrc       = string.Empty;
    private string _contabEmail     = string.Empty;
    private string _contabFone      = string.Empty;

    public string RazaoSocial     { get => _razaoSocial;     set => SetProperty(ref _razaoSocial, value); }
    public string CNPJ            { get => _cnpj;            set => SetProperty(ref _cnpj, value); }
    public string IE              { get => _ie;              set => SetProperty(ref _ie, value); }
    public string CodigoMunicipio { get => _codigoMunicipio; set => SetProperty(ref _codigoMunicipio, value); }
    public string Endereco        { get => _endereco;        set => SetProperty(ref _endereco, value); }
    public string ContabNome      { get => _contabNome;      set => SetProperty(ref _contabNome, value); }
    public string ContabCpf       { get => _contabCpf;       set => SetProperty(ref _contabCpf, value); }
    public string ContabCrc       { get => _contabCrc;       set => SetProperty(ref _contabCrc, value); }
    public string ContabEmail     { get => _contabEmail;     set => SetProperty(ref _contabEmail, value); }
    public string ContabFone      { get => _contabFone;      set => SetProperty(ref _contabFone, value); }

    // ── Status ────────────────────────────────────────────────────────────────
    private string _status = "Aguardando geração...";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    // ── Comandos ──────────────────────────────────────────────────────────────
    public ICommand GerarCommand { get; }

    public SpedViewModel()
    {
        GerarCommand = new AsyncRelayCommand(GerarAsync);
        CarregarConfigEmpresa();
    }

    private void CarregarConfigEmpresa()
    {
        var cfg = ConfiguracaoService.Carregar();
        RazaoSocial = cfg.RazaoSocial;
        // CNPJ/IE/Endereço vêm da config se existirem
        Endereco    = cfg.Endereco;
    }

    private async Task GerarAsync(object? _)
    {
        if (string.IsNullOrWhiteSpace(RazaoSocial) || string.IsNullOrWhiteSpace(CNPJ))
        {
            MessageBox.Show("Preencha Razão Social e CNPJ antes de gerar.",
                "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var tipo      = GerarEfdIcms ? "EFD-ICMS" : "EFD-Contrib";
        var cnpjLimpo = new string(CNPJ.Where(char.IsDigit).ToArray());
        var nomeArq   = $"{tipo}_{cnpjLimpo}_{DataInicio:yyyyMM}.txt";

        var dialog = new SaveFileDialog
        {
            Title      = $"Salvar {tipo}",
            FileName   = nomeArq,
            DefaultExt = ".txt",
            Filter     = "Arquivo SPED (*.txt)|*.txt"
        };

        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        Status = $"Gerando {tipo}...";

        try
        {
            var service = App.Services.GetRequiredService<ISpedService>();

            var parametros = new SpedParametros
            {
                DataInicio      = DataInicio,
                DataFim         = DataFim,
                RazaoSocial     = RazaoSocial,
                CNPJ            = CNPJ,
                IE              = IE,
                CodigoMunicipio = CodigoMunicipio,
                Endereco        = Endereco,
                IndPerfil       = "C",
                ContabNome      = ContabNome,
                ContabCpf       = ContabCpf,
                ContabCrc       = ContabCrc,
                ContabEmail     = ContabEmail,
                ContabFone      = ContabFone
            };

            string conteudo = GerarEfdIcms
                ? await service.GerarEfdIcmsAsync(parametros)
                : await service.GerarEfdContribuicoesAsync(parametros);

            await System.IO.File.WriteAllTextAsync(dialog.FileName,
                conteudo, System.Text.Encoding.UTF8);

            var linhas = conteudo.Split('\n').Length;
            Status = $"✅ {tipo} gerado com sucesso — {linhas} registros em {nomeArq}";

            if (MessageBox.Show($"{tipo} gerado com sucesso!\n\n{linhas} registros.\nDeseja abrir o arquivo?",
                "Concluído", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dialog.FileName,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Status = $"❌ Erro: {ex.Message}";
            MessageBox.Show($"Erro ao gerar SPED:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}
