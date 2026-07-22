// ── ERP.WPF/ViewModels/ConciliacaoBancariaViewModel.cs ────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Conciliação Bancária — Categoria A (evidência: VHSYS e GestãoClick têm isso
/// como recurso de destaque, importação de extrato OFX). Injeção via construtor
/// desde o início (Parte 0 do roadmap).
/// </summary>
public class ConciliacaoBancariaViewModel : BaseViewModel
{
    private readonly IContaBancariaService _service;

    public ObservableCollection<ContaBancariaDto> Contas { get; } = new();
    public ObservableCollection<SugestaoConciliacaoDto> Sugestoes { get; } = new();

    private ContaBancariaDto? _contaSelecionada;
    public ContaBancariaDto? ContaSelecionada
    {
        get => _contaSelecionada;
        set { SetProperty(ref _contaSelecionada, value); Sugestoes.Clear(); AtualizarComandos(); }
    }

    public int TotalLinhas         => Sugestoes.Count;
    public int TotalComSugestao    => System.Linq.Enumerable.Count(Sugestoes, s => s.MovimentoSugeridoId.HasValue);
    public int TotalSemSugestao    => TotalLinhas - TotalComSugestao;

    public ICommand ImportarArquivoCommand { get; }
    public ICommand ConfirmarCommand       { get; } // param: SugestaoConciliacaoDto
    public ICommand CriarNovoCommand       { get; } // param: SugestaoConciliacaoDto

    public ConciliacaoBancariaViewModel(IContaBancariaService service)
    {
        _service = service;

        ImportarArquivoCommand = new RelayCommand(async _ => await ImportarArquivoAsync(), _ => ContaSelecionada != null);
        ConfirmarCommand       = new RelayCommand(async p => await ConfirmarAsync(p as SugestaoConciliacaoDto));
        CriarNovoCommand       = new RelayCommand(async p => await CriarNovoAsync(p as SugestaoConciliacaoDto));

        _ = CarregarContasAsync();
    }

    private void AtualizarComandos() => (ImportarArquivoCommand as RelayCommand)?.RaiseCanExecuteChanged();

    public async Task CarregarContasAsync()
    {
        var contas = await _service.ObterContasAtivasAsync();
        Contas.Clear();
        foreach (var c in contas) Contas.Add(c);
    }

    private async Task ImportarArquivoAsync()
    {
        if (ContaSelecionada is null) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Extrato bancário (*.ofx)|*.ofx|Todos os arquivos (*.*)|*.*",
            Title  = "Selecione o arquivo OFX do extrato bancário"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var conteudo = await File.ReadAllTextAsync(dialog.FileName);
            var sugestoes = await _service.ProcessarExtratoOfxAsync(ContaSelecionada.Id, conteudo);

            Sugestoes.Clear();
            foreach (var s in sugestoes) Sugestoes.Add(s);

            NotificarContadores();

            if (Sugestoes.Count == 0)
                MessageBox.Show(
                    "Não encontrei nenhuma transação nesse arquivo. Confirme que é um extrato OFX válido.",
                    "Nenhuma transação encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não consegui ler esse arquivo:\n{ex.Message}", "Erro ao importar",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ConfirmarAsync(SugestaoConciliacaoDto? sugestao)
    {
        if (sugestao?.MovimentoSugeridoId is null) return;

        await _service.ConfirmarConciliacaoAsync(sugestao.MovimentoSugeridoId.Value, sugestao.FitId);
        Sugestoes.Remove(sugestao);
        NotificarContadores();
    }

    private async Task CriarNovoAsync(SugestaoConciliacaoDto? sugestao)
    {
        if (sugestao is null || ContaSelecionada is null) return;

        var res = MessageBox.Show(
            $"Criar um lançamento novo pra essa linha do extrato?\n\n" +
            $"{sugestao.Data:dd/MM/yyyy} — {sugestao.Descricao} — {sugestao.Valor:C2}\n\n" +
            "Use isso quando o dinheiro se moveu no banco mas nunca foi lançado no sistema " +
            "(ex: taxa bancária, juros, ou um lançamento que passou batido).",
            "Confirmar novo lançamento", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (res != MessageBoxResult.Yes) return;

        await _service.CriarEConciliarAsync(ContaSelecionada.Id, sugestao);
        Sugestoes.Remove(sugestao);
        NotificarContadores();
    }

    private void NotificarContadores()
    {
        OnPropertyChanged(nameof(TotalLinhas));
        OnPropertyChanged(nameof(TotalComSugestao));
        OnPropertyChanged(nameof(TotalSemSugestao));
    }
}