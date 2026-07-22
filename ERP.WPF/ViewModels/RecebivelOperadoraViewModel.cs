// ── ERP.WPF/ViewModels/RecebivelOperadoraViewModel.cs ─────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>Um recebível pendente, com seleção pra liquidação em lote.</summary>
public class RecebivelSelecionavel : BaseViewModel
{
    public RecebivelOperadoraDto Dto { get; }

    private bool _isSelecionado;
    public bool IsSelecionado { get => _isSelecionado; set => SetProperty(ref _isSelecionado, value); }

    public RecebivelSelecionavel(RecebivelOperadoraDto dto) => Dto = dto;
}

/// <summary>
/// Recebíveis de Operadora — fecha o ciclo do cartão: dinheiro que a operadora
/// deve, ainda não liquidado. Injeção via construtor desde o início.
/// </summary>
public class RecebivelOperadoraViewModel : BaseViewModel
{
    private readonly IRecebivelOperadoraService _service;
    private readonly IMotorFinanceiroService _motorFinanceiro;

    public ObservableCollection<RecebivelSelecionavel> Pendentes { get; } = new();

    public decimal TotalBrutoSelecionado   => Pendentes.Where(r => r.IsSelecionado).Sum(r => r.Dto.ValorBruto);
    public decimal TotalLiquidoSelecionado => Pendentes.Where(r => r.IsSelecionado).Sum(r => r.Dto.ValorLiquido);
    public int     QuantidadeSelecionada   => Pendentes.Count(r => r.IsSelecionado);

    private decimal _valorRealDepositado;
    public decimal ValorRealDepositado { get => _valorRealDepositado; set => SetProperty(ref _valorRealDepositado, value); }

    private DateTime _dataLiquidacao = DateTime.Today;
    public DateTime DataLiquidacao { get => _dataLiquidacao; set => SetProperty(ref _dataLiquidacao, value); }

    public ICommand LiquidarCommand { get; }
    public ICommand AtualizarCommand { get; }

    public RecebivelOperadoraViewModel(IRecebivelOperadoraService service, IMotorFinanceiroService motorFinanceiro)
    {
        _service         = service;
        _motorFinanceiro = motorFinanceiro;

        LiquidarCommand  = new RelayCommand(async _ => await LiquidarAsync(), _ => QuantidadeSelecionada > 0);
        AtualizarCommand = new RelayCommand(async _ => await CarregarAsync());

        _ = CarregarAsync();
    }

    public async Task CarregarAsync()
    {
        var pendentes = await _service.ObterPendentesAsync();

        Pendentes.Clear();
        foreach (var dto in pendentes)
        {
            var item = new RecebivelSelecionavel(dto);
            item.PropertyChanged += (_, __) => NotificarTotais();
            Pendentes.Add(item);
        }

        NotificarTotais();
    }

    private void NotificarTotais()
    {
        OnPropertyChanged(nameof(TotalBrutoSelecionado));
        OnPropertyChanged(nameof(TotalLiquidoSelecionado));
        OnPropertyChanged(nameof(QuantidadeSelecionada));
        (LiquidarCommand as RelayCommand)?.RaiseCanExecuteChanged();

        // Sugestão de ponto de partida — o usuário ajusta pro valor real do extrato.
        ValorRealDepositado = TotalLiquidoSelecionado;
    }

    private async Task LiquidarAsync()
    {
        var selecionados = Pendentes.Where(r => r.IsSelecionado).Select(r => r.Dto.Id).ToList();
        if (selecionados.Count == 0) return;

        var res = MessageBox.Show(
            $"Liquidar {selecionados.Count} recebível(is), totalizando R$ {ValorRealDepositado:N2} depositado?",
            "Confirmar Liquidação", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        try
        {
            // Passa pelo Motor Financeiro, não pelo service de recebível direto —
            // um único ponto de entrada pra tudo que mexe em dinheiro.
            await _motorFinanceiro.RegistrarLiquidacaoOperadoraAsync(selecionados, ValorRealDepositado, DataLiquidacao);
            await CarregarAsync();
            MessageBox.Show("Recebíveis liquidados! O valor entrou na Conta Bancária.", "Sucesso");
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Não foi possível liquidar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}