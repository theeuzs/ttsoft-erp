using ERP.Application.DTOs;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Diálogo de escolha de origem do pagamento de uma despesa: caixa físico
/// (comportamento original) ou uma Conta Bancária específica (item 1.5).
/// </summary>
public class PagarDespesaDialogViewModel : BaseViewModel
{
    public string  Descricao      { get; }
    public decimal Valor          { get; }
    public string  ValorFormatado => Valor.ToString("C2");

    public ObservableCollection<ContaBancariaDto> ContasBancarias { get; } = new();

    private bool _usarConta;
    public bool UsarConta
    {
        get => _usarConta;
        set { SetProperty(ref _usarConta, value); if (!value) ContaSelecionada = null; }
    }

    private ContaBancariaDto? _contaSelecionada;
    public ContaBancariaDto? ContaSelecionada
    {
        get => _contaSelecionada;
        set => SetProperty(ref _contaSelecionada, value);
    }

    public Action? OnConfirmado { get; set; }
    public Action? OnCancelado  { get; set; }

    public ICommand ConfirmarCommand { get; }
    public ICommand CancelarCommand  { get; }

    public PagarDespesaDialogViewModel(string descricao, decimal valor, IReadOnlyList<ContaBancariaDto> contas)
    {
        Descricao = descricao;
        Valor     = valor;
        foreach (var c in contas) ContasBancarias.Add(c);

        ConfirmarCommand = new RelayCommand(_ => OnConfirmado?.Invoke(), _ => PodeConfirmar());
        CancelarCommand  = new RelayCommand(_ => OnCancelado?.Invoke());
    }

    private bool PodeConfirmar() => !UsarConta || ContaSelecionada != null;
}
