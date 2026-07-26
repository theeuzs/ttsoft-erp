// ── ERP.WPF/ViewModels/ContaHistoricoViewModel.cs ───────────────────────────
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Linha do tempo de uma conta a receber — reutilizável a partir de qualquer
/// tela que tenha um contaId em mãos (ContasClienteView, histórico do cliente).
/// </summary>
public class ContaHistoricoViewModel : BaseViewModel
{
    public string Titulo { get; }
    public ObservableCollection<ContaReceberEvento> Eventos { get; } = new();

    public ContaHistoricoViewModel(Guid contaId, string titulo)
    {
        Titulo = titulo;
        _ = CarregarAsync(contaId);
    }

    private async Task CarregarAsync(Guid contaId)
    {
        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IContaReceberService>();
            var eventos = await service.GetEventosAsync(contaId);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Eventos.Clear();
                foreach (var e in eventos) Eventos.Add(e);
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar histórico: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}