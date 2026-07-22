// ── ERP.WPF/ViewModels/VendaSuspensaListViewModel.cs ──────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Lista de vendas suspensas — modal, disparado de dentro do PDV. Mostra as
/// de TODOS os operadores (fila de balcão é um problema de loja, não de
/// pessoa), com indicação de quem está editando cada uma agora.
/// </summary>
public class VendaSuspensaListViewModel : BaseViewModel
{
    private readonly IVendaSuspensaService _service;
    private readonly Guid _usuarioId;
    private readonly string _nomeUsuario;

    public ObservableCollection<VendaSuspensaResumoDto> Pendentes { get; } = new();

    public ICommand RetomarCommand   { get; } // param: VendaSuspensaResumoDto
    public ICommand DescartarCommand { get; } // param: VendaSuspensaResumoDto
    public ICommand AtualizarCommand { get; }
    public ICommand FecharCommand    { get; }

    /// <summary>Disparado quando o usuário retoma com sucesso — o PDV carrega os itens no carrinho.</summary>
    public Action<VendaSuspensaDetalheDto>? OnRetomarConfirmado { get; set; }
    public Action? OnFechar { get; set; }

    public VendaSuspensaListViewModel(IVendaSuspensaService service, Guid usuarioId, string nomeUsuario)
    {
        _service     = service;
        _usuarioId   = usuarioId;
        _nomeUsuario = nomeUsuario;

        RetomarCommand   = new RelayCommand(async p => await RetomarAsync(p as VendaSuspensaResumoDto));
        DescartarCommand = new RelayCommand(async p => await DescartarAsync(p as VendaSuspensaResumoDto));
        AtualizarCommand = new RelayCommand(async _ => await CarregarAsync());
        FecharCommand    = new RelayCommand(_ => OnFechar?.Invoke());

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        var pendentes = await _service.ObterPendentesAsync();
        Pendentes.Clear();
        foreach (var p in pendentes) Pendentes.Add(p);
    }

    private async Task RetomarAsync(VendaSuspensaResumoDto? resumo)
    {
        if (resumo is null) return;

        try
        {
            var detalhe = await _service.IniciarEdicaoAsync(resumo.Id, _usuarioId, _nomeUsuario);
            OnRetomarConfirmado?.Invoke(detalhe);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Não foi possível retomar", MessageBoxButton.OK, MessageBoxImage.Warning);
            await CarregarAsync();
        }
    }

    private async Task DescartarAsync(VendaSuspensaResumoDto? resumo)
    {
        if (resumo is null) return;

        var res = MessageBox.Show(
            $"Descartar a venda suspensa de {resumo.ClienteNome} ({resumo.TotalAproximado:C2})?\n\n" +
            "Os itens não voltam pro carrinho — isso é definitivo.",
            "Confirmar Descarte", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (res != MessageBoxResult.Yes) return;

        await _service.DescartarAsync(resumo.Id);
        await CarregarAsync();
    }
}
