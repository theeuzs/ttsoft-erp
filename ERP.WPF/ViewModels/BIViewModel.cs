using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class BIViewModel : BaseViewModel
{
    private readonly IBIService _bi;

    public ObservableCollection<SazonalidadeDto>    Sazonalidade     { get; } = new();
    public ObservableCollection<AbcAvancadoDto>     AbcAvancado      { get; } = new();
    public ObservableCollection<RankingVendedorDto> RankingVendedores { get; } = new();
    public ObservableCollection<PrevisaoDemandaDto> PrevisaoDemanda  { get; } = new();

    private DateTime _dataInicio = DateTime.Today.AddDays(-30);
    public DateTime DataInicio
    {
        get => _dataInicio;
        set => SetProperty(ref _dataInicio, value);
    }

    private DateTime _dataFim = DateTime.Today;
    public DateTime DataFim
    {
        get => _dataFim;
        set => SetProperty(ref _dataFim, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand AtualizarCommand { get; }

    public BIViewModel(IBIService bi)
    {
        _bi = bi;
        AtualizarCommand = new AsyncRelayCommand(_ => CarregarTudoAsync());
        _ = CarregarTudoAsync();
    }

    private async Task CarregarTudoAsync()
    {
        IsBusy = true;
        StatusMessage = "Carregando...";
        try
        {
            var sazon   = await _bi.ObterSazonalidadeAsync(12);
            var abc     = await _bi.ObterAbcAvancadoAsync(DataInicio, DataFim);
            var ranking = await _bi.ObterRankingVendedoresAsync(DataInicio, DataFim);
            var prev    = await _bi.ObterPrevisaoDemandaAsync();

            Sazonalidade.Clear();
            foreach (var s in sazon)     Sazonalidade.Add(s);

            AbcAvancado.Clear();
            foreach (var a in abc)       AbcAvancado.Add(a);

            RankingVendedores.Clear();
            foreach (var r in ranking)   RankingVendedores.Add(r);

            PrevisaoDemanda.Clear();
            foreach (var p in prev)      PrevisaoDemanda.Add(p);

            StatusMessage = $"Atualizado em {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
            MessageBox.Show($"Erro ao carregar BI: {ex.Message}", "Erro",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}
