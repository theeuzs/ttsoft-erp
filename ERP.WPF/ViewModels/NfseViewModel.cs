// ERP.WPF/ViewModels/NfseViewModel.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.WPF.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Item 11 do roadmap fiscal (P1) — UI de emissão de NFS-e. O backend
/// (INfseEmissionService, entidade NfseEmitida) já existia pronto; faltava
/// só a tela — ninguém chamava o serviço a partir de nenhum lugar.
/// </summary>
public class NfseViewModel : BaseViewModel
{
    private readonly ICustomerService _customerService;

    // ── Tomador ──────────────────────────────────────────────────────────
    private string _buscaCliente = string.Empty;
    public string BuscaCliente
    {
        get => _buscaCliente;
        set { SetProperty(ref _buscaCliente, value); _ = BuscarClientesAsync(value); }
    }

    private CustomerDto? _clienteSelecionado;
    public CustomerDto? ClienteSelecionado
    {
        get => _clienteSelecionado;
        set
        {
            SetProperty(ref _clienteSelecionado, value);
            if (value != null)
            {
                TomadorNome = value.Name;
                TomadorCpfCnpj = value.Document;
                TomadorEmail = value.Email;
                OnPropertyChanged(nameof(TomadorNome));
                OnPropertyChanged(nameof(TomadorCpfCnpj));
                OnPropertyChanged(nameof(TomadorEmail));
            }
        }
    }

    public ObservableCollection<CustomerDto> ClientesSugestao { get; } = new();

    public string TomadorNome { get; set; } = string.Empty;
    public string? TomadorCpfCnpj { get; set; }
    public string? TomadorEmail { get; set; }
    public string? TomadorEndereco { get; set; }

    // ── Serviço ──────────────────────────────────────────────────────────
    public string DescricaoServico { get; set; } = string.Empty;
    public string? CodigoServico { get; set; }
    public string? CodigoCnae { get; set; }
    public string? CodigoMunicipio { get; set; }
    public decimal ValorServico { get; set; }
    public decimal AliquotaISS { get; set; } = 2m;

    public decimal ValorIssCalculado => Math.Round(ValorServico * AliquotaISS / 100, 2);
    public decimal ValorLiquidoCalculado => ValorServico - ValorIssCalculado;

    // ── Histórico ────────────────────────────────────────────────────────
    public ObservableCollection<NfseEmitida> NotasEmitidas { get; } = new();

    private string _statusTexto = string.Empty;
    public string StatusTexto { get => _statusTexto; set => SetProperty(ref _statusTexto, value); }

    public ICommand EmitirCommand { get; }
    public ICommand CancelarNotaCommand { get; }
    public ICommand AbrirDanfseCommand { get; }
    public ICommand LimparCommand { get; }
    public ICommand AtualizarHistoricoCommand { get; }

    public NfseViewModel(ICustomerService customerService)
    {
        _customerService = customerService;

        EmitirCommand = new AsyncRelayCommand(async _ => await EmitirAsync());
        CancelarNotaCommand = new AsyncRelayCommand(async item => await CancelarAsync(item));
        AbrirDanfseCommand = new RelayCommand(item => { if (item is NfseEmitida n && !string.IsNullOrWhiteSpace(n.UrlDanfse)) Process.Start(new ProcessStartInfo { FileName = n.UrlDanfse, UseShellExecute = true }); });
        LimparCommand = new RelayCommand(_ => LimparFormulario());
        AtualizarHistoricoCommand = new AsyncRelayCommand(async _ => await CarregarHistoricoAsync());

        _ = CarregarHistoricoAsync();
    }

    private async Task BuscarClientesAsync(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Length < 2) { ClientesSugestao.Clear(); return; }
        try
        {
            var resultado = await _customerService.SearchAsync(termo);
            ClientesSugestao.Clear();
            foreach (var c in resultado.Take(8)) ClientesSugestao.Add(c);
        }
        catch { ClientesSugestao.Clear(); }
    }

    private async Task CarregarHistoricoAsync()
    {
        try
        {
            var ctx = App.Services.GetRequiredService<ERP.Persistence.Context.AppDbContext>();
            var lista = await ctx.NfseEmitidas.AsNoTracking()
                .OrderByDescending(n => n.DataEmissao)
                .Take(100)
                .ToListAsync();
            NotasEmitidas.Clear();
            foreach (var n in lista) NotasEmitidas.Add(n);
        }
        catch { /* best-effort */ }
    }

    private async Task EmitirAsync()
    {
        if (string.IsNullOrWhiteSpace(TomadorNome))
        {
            MessageBox.Show("Nome do tomador é obrigatório.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(DescricaoServico))
        {
            MessageBox.Show("Descrição do serviço é obrigatória.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (ValorServico <= 0)
        {
            MessageBox.Show("Valor do serviço precisa ser maior que zero.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirmacao = MessageBox.Show(
            $"Emitir NFS-e pra {TomadorNome} — R$ {ValorServico:N2} (ISS {AliquotaISS:N1}% = R$ {ValorIssCalculado:N2})?\n\nIsso transmite pra prefeitura de verdade.",
            "Confirmar Emissão", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmacao != MessageBoxResult.Yes) return;

        try
        {
            StatusTexto = "Emitindo NFS-e...";

            var configProvider = App.Services.GetRequiredService<IFiscalConfigurationProvider>();
            var config = await configProvider.ObterConfiguracaoAsync();
            if (string.IsNullOrWhiteSpace(config.TokenFocusNfe))
            {
                MessageBox.Show("Token da Focus NFe não configurado — vá em Configurações → Empresa e Fiscal.",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nfseService = App.Services.GetRequiredService<INfseEmissionService>();
            var dto = new EmitirNfseDto
            {
                ClienteId        = ClienteSelecionado?.Id,
                TomadorNome      = TomadorNome,
                TomadorCpfCnpj   = TomadorCpfCnpj,
                TomadorEmail     = TomadorEmail,
                TomadorEndereco  = TomadorEndereco,
                DescricaoServico = DescricaoServico,
                CodigoServico    = CodigoServico,
                CodigoCnae       = CodigoCnae,
                ValorServico     = ValorServico,
                AliquotaISS      = AliquotaISS,
                CodigoMunicipio  = CodigoMunicipio,
            };

            var (sucesso, mensagem, nfse) = await nfseService.EmitirAsync(dto, config.TokenFocusNfe, config.UsarAmbienteProducao);

            if (sucesso)
            {
                MessageBox.Show($"✅ NFS-e emitida!\n{mensagem}", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                LimparFormulario();
                await CarregarHistoricoAsync();
            }
            else
            {
                MessageBox.Show($"❌ Falha ao emitir: {mensagem}", "Erro Fiscal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro inesperado: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { StatusTexto = string.Empty; }
    }

    private async Task CancelarAsync(object? item)
    {
        if (item is not NfseEmitida nota) return;

        if (nota.Status != StatusNfse.Autorizada)
        {
            MessageBox.Show("Só é possível cancelar uma NFS-e autorizada.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string motivo = Microsoft.VisualBasic.Interaction.InputBox("Motivo do cancelamento:", "Cancelar NFS-e", "");
        if (string.IsNullOrWhiteSpace(motivo)) return;

        try
        {
            var configProvider = App.Services.GetRequiredService<IFiscalConfigurationProvider>();
            var config = await configProvider.ObterConfiguracaoAsync();
            var nfseService = App.Services.GetRequiredService<INfseEmissionService>();

            var (sucesso, mensagem) = await nfseService.CancelarAsync(nota.ReferenciaNfse, motivo, config.TokenFocusNfe, config.UsarAmbienteProducao);

            if (sucesso)
            {
                MessageBox.Show("✅ NFS-e cancelada.", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
                await CarregarHistoricoAsync();
            }
            else
            {
                MessageBox.Show($"❌ Falha ao cancelar: {mensagem}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao cancelar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LimparFormulario()
    {
        ClienteSelecionado = null;
        BuscaCliente = string.Empty;
        TomadorNome = string.Empty;
        TomadorCpfCnpj = null;
        TomadorEmail = null;
        TomadorEndereco = null;
        DescricaoServico = string.Empty;
        CodigoServico = null;
        CodigoCnae = null;
        CodigoMunicipio = null;
        ValorServico = 0;
        AliquotaISS = 2m;
        OnPropertyChanged(string.Empty);
    }
}
