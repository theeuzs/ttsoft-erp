using ERP.Domain.Entities;
using ERP.Infrastructure.Services;
using ERP.WPF.Commands;
using ERP.WPF.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class FilialViewModel : BaseViewModel
{
    private readonly ITransferenciaService _service;

    public ObservableCollection<Branch>                 Filiais       { get; } = new();
    public ObservableCollection<TransferenciaEstoque>   Transferencias { get; } = new();

    // ── Transferência em aberto ───────────────────────────────────────────────
    private Branch? _filialOrigem;
    public Branch? FilialOrigem
    {
        get => _filialOrigem;
        set => SetProperty(ref _filialOrigem, value);
    }

    private Branch? _filialDestino;
    public Branch? FilialDestino
    {
        get => _filialDestino;
        set => SetProperty(ref _filialDestino, value);
    }

    private string _observacao = string.Empty;
    public string Observacao
    {
        get => _observacao;
        set => SetProperty(ref _observacao, value);
    }

    private string _itensTexto = string.Empty;
    public string ItensTexto
    {
        get => _itensTexto;
        set => SetProperty(ref _itensTexto, value);
    }

    public ICommand CarregarCommand     { get; }
    public ICommand CriarTransfCommand  { get; }
    public ICommand ConfirmarCommand    { get; }
    public ICommand CancelarCommand     { get; }

    public FilialViewModel(ITransferenciaService service)
    {
        _service = service;

        CarregarCommand    = new AsyncRelayCommand(_ => CarregarAsync());
        CriarTransfCommand = new AsyncRelayCommand(_ => CriarTransferenciaAsync(),
            _ => FilialOrigem != null && FilialDestino != null && FilialOrigem.Id != FilialDestino?.Id);
        ConfirmarCommand   = new AsyncRelayCommand(
            p => ConfirmarAsync(p as TransferenciaEstoque),
            p => (p as TransferenciaEstoque)?.Status == StatusTransferencia.Rascunho);
        CancelarCommand    = new AsyncRelayCommand(
            p => CancelarAsync(p as TransferenciaEstoque),
            p => { var t = p as TransferenciaEstoque;
                   return t?.Status == StatusTransferencia.Rascunho || t?.Status == StatusTransferencia.Enviada; });

        CarregarAsync().SafeFireAndForgetSilentAsync("filiais");
    }

    private async Task CarregarAsync()
    {
        IsBusy = true;
        try
        {
            var filiais = (await _service.GetFilialAsync()).ToList();
            Filiais.Clear();
            foreach (var f in filiais) Filiais.Add(f);

            if (FilialOrigem != null)
            {
                var transf = (await _service.GetByFilialAsync(FilialOrigem.Id)).ToList();
                Transferencias.Clear();
                foreach (var t in transf) Transferencias.Add(t);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar filiais: {ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private async Task CriarTransferenciaAsync()
    {
        if (FilialOrigem == null || FilialDestino == null) return;

        IsBusy = true;
        try
        {
            // Parse de itens no formato simples: "ProductId:Qtd" por linha
            var itens = ItensTexto
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(linha => linha.Trim().Split(':'))
                .Where(p => p.Length == 2 && Guid.TryParse(p[0], out _) && decimal.TryParse(p[1], out _))
                .Select(p => (Guid.Parse(p[0]), decimal.Parse(p[1])))
                .ToList();

            if (!itens.Any())
            {
                MessageBox.Show("Adicione os itens no formato:\n{ID_Produto}:{Quantidade}",
                    "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _service.CriarAsync(new CriarTransferenciaDto
            {
                OrigemId     = FilialOrigem.Id,
                DestinoId    = FilialDestino.Id,
                OperadorNome = State.AppSession.UserName,
                Observacao   = Observacao,
                Itens        = itens
            });

            MessageBox.Show("✅ Transferência criada! Confirme para mover o estoque.",
                "TTSoft ERP", MessageBoxButton.OK, MessageBoxImage.Information);

            Observacao   = string.Empty;
            ItensTexto   = string.Empty;
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private async Task ConfirmarAsync(TransferenciaEstoque? t)
    {
        if (t == null) return;
        IsBusy = true;
        try
        {
            await _service.ConfirmarAsync(t.Id, State.AppSession.UserName);
            MessageBox.Show("✅ Transferência confirmada! Estoques atualizados.",
                "TTSoft ERP", MessageBoxButton.OK, MessageBoxImage.Information);
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private async Task CancelarAsync(TransferenciaEstoque? t)
    {
        if (t == null) return;
        IsBusy = true;
        try
        {
            await _service.CancelarAsync(t.Id, "Cancelado pelo operador");
            await CarregarAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}