// ERP.WPF/ViewModels/InventarioViewModel.cs
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class InventarioItem
{
    public Guid   ProductId      { get; set; }
    public string Nome           { get; set; } = string.Empty;
    public string SKU            { get; set; } = string.Empty;
    public string Categoria      { get; set; } = string.Empty;
    public decimal EstoqueSistema { get; set; }

    private decimal _estoqueContado;
    public decimal EstoqueContado
    {
        get => _estoqueContado;
        set { _estoqueContado = value; Diferenca = value - EstoqueSistema; }
    }

    public decimal Diferenca { get; set; }
    public bool    Conferido  { get; set; }

    public string CorDiferenca  => Diferenca == 0 ? "#16A34A" : Diferenca > 0 ? "#3B82F6" : "#DC2626";
    public string TextoDiferenca=> Diferenca == 0 ? "✅ Ok"   : Diferenca > 0 ? $"+{Diferenca:N2}" : $"{Diferenca:N2}";
}

public class InventarioViewModel : BaseViewModel
{
    public ObservableCollection<InventarioItem> Itens { get; } = new();

    private int _totalConferidos;
    public int TotalConferidos  { get => _totalConferidos;  set => SetProperty(ref _totalConferidos,  value); }

    private int _totalDivergentes;
    public int TotalDivergentes { get => _totalDivergentes; set => SetProperty(ref _totalDivergentes, value); }

    private int _totalPendentes;
    public int TotalPendentes   { get => _totalPendentes;   set => SetProperty(ref _totalPendentes,   value); }

    private string _filtroBusca = string.Empty;
    public string FiltroBusca
    {
        get => _filtroBusca;
        set { SetProperty(ref _filtroBusca, value); AplicarFiltro(); }
    }

    private ObservableCollection<InventarioItem> _todosItens = new();

    public ICommand CarregarCommand { get; }
    public ICommand AplicarCommand  { get; }
    public ICommand ConferirCommand { get; }

    public InventarioViewModel()
    {
        CarregarCommand = new RelayCommand(async _ => await CarregarAsync());
        AplicarCommand  = new RelayCommand(async _ => await AplicarAjustesAsync(),
            _ => Itens.Any(i => i.Diferenca != 0 && i.Conferido));
        ConferirCommand = new RelayCommand(p => Conferir(p as InventarioItem));

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        IsBusy = true;
        try
        {
            var service  = App.Services.GetRequiredService<IInventarioService>();
            var produtos = await service.ObterProdutosAsync();

            _todosItens.Clear();
            foreach (var p in produtos)
                _todosItens.Add(new InventarioItem
                {
                    ProductId       = p.ProductId,
                    Nome            = p.Nome,
                    SKU             = p.SKU,
                    Categoria       = p.Categoria,
                    EstoqueSistema  = p.EstoqueSistema,
                    EstoqueContado  = p.EstoqueSistema,
                    Diferenca       = 0,
                    Conferido       = false,
                });

            AplicarFiltro();
            AtualizarResumo();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar inventário:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void Conferir(InventarioItem? item)
    {
        if (item is null) return;
        item.Conferido = true;
        AtualizarResumo();
        CommandManager.InvalidateRequerySuggested();
    }

    private void AplicarFiltro()
    {
        var filtrados = string.IsNullOrWhiteSpace(FiltroBusca)
            ? _todosItens
            : _todosItens.Where(p =>
                p.Nome.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase) ||
                p.SKU.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase) ||
                p.Categoria.Contains(FiltroBusca, StringComparison.OrdinalIgnoreCase));

        Itens.Clear();
        foreach (var i in filtrados) Itens.Add(i);
    }

    private void AtualizarResumo()
    {
        TotalConferidos  = _todosItens.Count(i => i.Conferido);
        TotalDivergentes = _todosItens.Count(i => i.Conferido && i.Diferenca != 0);
        TotalPendentes   = _todosItens.Count(i => !i.Conferido);
    }

    private async Task AplicarAjustesAsync()
    {
        var divergentes = Itens.Where(i => i.Diferenca != 0 && i.Conferido).ToList();

        if (!divergentes.Any())
        {
            MessageBox.Show("Nenhuma divergência para ajustar.", "Inventário",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Aplicar {divergentes.Count} ajuste(s) de estoque?\n\n" +
            "O estoque do sistema será atualizado para os valores contados.",
            "Confirmar Ajuste", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var service = App.Services.GetRequiredService<IInventarioService>();

            var ajustes = divergentes.Select(i => (i.ProductId, i.EstoqueContado));
            await service.AplicarAjustesAsync(ajustes);

            MessageBox.Show($"✅ {divergentes.Count} ajuste(s) aplicados com sucesso!", "Inventário",
                MessageBoxButton.OK, MessageBoxImage.Information);

            await CarregarAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao aplicar ajustes:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}
