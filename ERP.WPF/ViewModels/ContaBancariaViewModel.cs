// ── ERP.WPF/ViewModels/ContaBancariaViewModel.cs ──────────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Tela de Contas Bancárias, Caixa Geral e Saldo Consolidado (roadmap item 1.5).
/// Injeção via construtor desde o início — Parte 0 do roadmap: código novo não repete
/// o padrão de Service Locator usado em ViewModels mais antigos.
/// </summary>
public class ContaBancariaViewModel : BaseViewModel
{
    private readonly IContaBancariaService _service;

    public ObservableCollection<ContaBancariaDto> Contas { get; } = new();
    public ObservableCollection<MovimentoContaBancariaDto> Extrato { get; } = new();

    private ContaBancariaDto? _contaSelecionada;
    public ContaBancariaDto? ContaSelecionada
    {
        get => _contaSelecionada;
        set { SetProperty(ref _contaSelecionada, value); _ = CarregarExtratoAsync(); AtualizarComandos(); }
    }

    // ── Formulário: nova conta ────────────────────────────────────────────────
    private string _novoApelido = string.Empty;
    public string NovoApelido { get => _novoApelido; set => SetProperty(ref _novoApelido, value); }

    private string _novoBanco = string.Empty;
    public string NovoBanco { get => _novoBanco; set => SetProperty(ref _novoBanco, value); }

    private string _novaAgencia = string.Empty;
    public string NovaAgencia { get => _novaAgencia; set => SetProperty(ref _novaAgencia, value); }

    private string _novoNumeroConta = string.Empty;
    public string NovoNumeroConta { get => _novoNumeroConta; set => SetProperty(ref _novoNumeroConta, value); }

    private decimal _novoSaldoInicial;
    public decimal NovoSaldoInicial { get => _novoSaldoInicial; set => SetProperty(ref _novoSaldoInicial, value); }

    // ── Formulário: lançamento ────────────────────────────────────────────────
    private decimal _valorLancamento;
    public decimal ValorLancamento { get => _valorLancamento; set => SetProperty(ref _valorLancamento, value); }

    private string _descricaoLancamento = string.Empty;
    public string DescricaoLancamento { get => _descricaoLancamento; set => SetProperty(ref _descricaoLancamento, value); }

    // ── Saldo consolidado ────────────────────────────────────────────────────
    private decimal _saldoTotalCaixas;
    public decimal SaldoTotalCaixas { get => _saldoTotalCaixas; set => SetProperty(ref _saldoTotalCaixas, value); }

    private decimal _saldoTotalContas;
    public decimal SaldoTotalContas { get => _saldoTotalContas; set => SetProperty(ref _saldoTotalContas, value); }

    private decimal _saldoConsolidado;
    public decimal SaldoConsolidado { get => _saldoConsolidado; set => SetProperty(ref _saldoConsolidado, value); }

    public ICommand CriarContaCommand      { get; }
    public ICommand LancarEntradaCommand   { get; }
    public ICommand LancarSaidaCommand     { get; }
    public ICommand InativarContaCommand   { get; }
    public ICommand DefinirPadraoCommand   { get; }
    public ICommand AtualizarPosicaoCommand { get; }

    public ContaBancariaViewModel(IContaBancariaService service)
    {
        _service = service;

        CriarContaCommand       = new RelayCommand(async _ => await CriarContaAsync(), _ => PodeCriarConta());
        LancarEntradaCommand    = new RelayCommand(async _ => await LancarAsync(TipoMovimentoContaBancaria.Entrada), _ => PodeLancar());
        LancarSaidaCommand      = new RelayCommand(async _ => await LancarAsync(TipoMovimentoContaBancaria.Saida), _ => PodeLancar());
        InativarContaCommand    = new RelayCommand(async _ => await InativarContaAsync(), _ => ContaSelecionada != null);
        DefinirPadraoCommand    = new RelayCommand(async _ => await DefinirPadraoAsync(), _ => ContaSelecionada != null && !ContaSelecionada.ContaPadrao);
        AtualizarPosicaoCommand = new RelayCommand(async _ => await CarregarPosicaoAsync());

        // S17 FIX: as duas chamadas usam o mesmo DbContext por baixo (escopo do
        // service injetado) — disparar as duas ao mesmo tempo como fire-and-forget
        // gera "A second operation was started on this context instance..." porque
        // o EF Core não permite duas operações concorrentes na mesma instância.
        // Roda em sequência, não em paralelo.
        _ = CarregarInicialAsync();
    }

    private async Task CarregarInicialAsync()
    {
        await CarregarContasAsync();
        await CarregarPosicaoAsync();
    }

    private bool PodeCriarConta() => !string.IsNullOrWhiteSpace(NovoApelido);
    private bool PodeLancar()     => ContaSelecionada != null && ValorLancamento > 0;

    private void AtualizarComandos()
    {
        (CriarContaCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LancarEntradaCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LancarSaidaCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (InativarContaCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DefinirPadraoCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public async Task CarregarContasAsync()
    {
        var contas = await _service.ObterContasAtivasAsync();
        Contas.Clear();
        foreach (var c in contas) Contas.Add(c);
    }

    private async Task CarregarExtratoAsync()
    {
        Extrato.Clear();
        if (ContaSelecionada is null) return;

        var movimentos = await _service.ObterExtratoAsync(ContaSelecionada.Id);
        foreach (var m in movimentos) Extrato.Add(m);
    }

    public async Task CarregarPosicaoAsync()
    {
        var posicao = await _service.ObterPosicaoFinanceiraAsync();
        SaldoTotalCaixas  = posicao.SaldoTotalCaixasAbertos;
        SaldoTotalContas  = posicao.SaldoTotalContasBancarias;
        SaldoConsolidado  = posicao.SaldoConsolidado;
    }

    private async Task CriarContaAsync()
    {
        try
        {
            await _service.CriarContaAsync(new CriarContaBancariaDto
            {
                Apelido      = NovoApelido,
                Banco        = NovoBanco,
                Agencia      = NovaAgencia,
                NumeroConta  = NovoNumeroConta,
                SaldoInicial = NovoSaldoInicial
            });

            NovoApelido = NovoBanco = NovaAgencia = NovoNumeroConta = string.Empty;
            NovoSaldoInicial = 0;

            await CarregarContasAsync();
            await CarregarPosicaoAsync();
            MessageBox.Show("Conta bancária cadastrada com sucesso!", "ConstruTTor");
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Não foi possível cadastrar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LancarAsync(TipoMovimentoContaBancaria tipo)
    {
        if (ContaSelecionada is null) return;

        try
        {
            await _service.RegistrarMovimentoAsync(
                ContaSelecionada.Id, ValorLancamento, DescricaoLancamento, tipo);

            ValorLancamento = 0;
            DescricaoLancamento = string.Empty;

            await CarregarContasAsync();
            await CarregarExtratoAsync();
            await CarregarPosicaoAsync();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Não foi possível lançar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task InativarContaAsync()
    {
        if (ContaSelecionada is null) return;

        var res = MessageBox.Show(
            $"Inativar a conta '{ContaSelecionada.Apelido}'? Ela deixa de aparecer na lista, mas o histórico é mantido.",
            "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (res != MessageBoxResult.Yes) return;

        await _service.InativarContaAsync(ContaSelecionada.Id);
        ContaSelecionada = null;
        await CarregarContasAsync();
        await CarregarPosicaoAsync();
    }

    /// <summary>
    /// Marca a conta selecionada como a que recebe automaticamente as vendas em
    /// PIX/Cartão do PDV. Só uma conta pode ser padrão por vez.
    /// </summary>
    private async Task DefinirPadraoAsync()
    {
        if (ContaSelecionada is null) return;

        await _service.DefinirComoContaPadraoAsync(ContaSelecionada.Id);
        await CarregarContasAsync();

        // Re-seleciona pra refletir o ContaPadrao=true atualizado nos botões.
        ContaSelecionada = Contas.FirstOrDefault(c => c.Id == ContaSelecionada?.Id);

        MessageBox.Show(
            $"'{ContaSelecionada?.Apelido}' agora recebe automaticamente as vendas em PIX e Cartão do PDV.",
            "Conta padrão definida", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}