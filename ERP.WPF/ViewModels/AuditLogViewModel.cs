using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class AuditLogViewModel : BaseViewModel
{
    public ObservableCollection<AuditLogDto> Logs { get; } = new();

    private DateTime _dataInicio = DateTime.Today.AddDays(-7);
    public DateTime DataInicio { get => _dataInicio; set => SetProperty(ref _dataInicio, value); }

    private DateTime _dataFim = DateTime.Today;
    public DateTime DataFim { get => _dataFim; set => SetProperty(ref _dataFim, value); }

    private string _busca = string.Empty;
    public string Busca { get => _busca; set => SetProperty(ref _busca, value); }

    public ICommand FiltrarCommand { get; }

    public AuditLogViewModel()
    {
        FiltrarCommand = new RelayCommand(async _ => await CarregarLogsAsync());
        _ = CarregarLogsAsync();
    }

    private async Task CarregarLogsAsync()
    {
        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var resultados = await service.SearchAsync(DataInicio, DataFim, 
                string.IsNullOrWhiteSpace(Busca) ? null : Busca);

            Logs.Clear();
            foreach (var log in resultados)
                Logs.Add(log);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao buscar os logs: {ex.Message}", "Erro", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}
