// ERP.WPF/ViewModels/NfeHistoricoViewModel.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Tela "NF-e (Emissão)" — o botão existia há muito tempo, sempre caindo
/// no placeholder genérico "Em desenvolvimento..." (não tinha nenhum case
/// pra "nfe" no roteamento). Repropósito pedido pelo usuário (20/08):
/// histórico amplo de qualquer NF-e A4 (avulsa ou ligada a venda, se
/// algum dia existir), pra não depender só da lista lateral pequena da
/// tela de Nota Avulsa.
/// </summary>
public class NfeHistoricoViewModel : BaseViewModel
{
    public ObservableCollection<NfeA4HistoricoDto> Notas { get; } = new();

    public string[] StatusFiltro { get; } = { "Todos", "Autorizada", "Processando", "Cancelada", "Rejeitada", "Rascunho" };
    private string _statusSelecionado = "Todos";
    public string StatusSelecionado
    {
        get => _statusSelecionado;
        set { SetProperty(ref _statusSelecionado, value); _ = CarregarAsync(); }
    }

    private DateTime? _dataInicio = DateTime.Today.AddDays(-30);
    public DateTime? DataInicio
    {
        get => _dataInicio;
        set { SetProperty(ref _dataInicio, value); _ = CarregarAsync(); }
    }

    private DateTime? _dataFim = DateTime.Today;
    public DateTime? DataFim
    {
        get => _dataFim;
        set { SetProperty(ref _dataFim, value); _ = CarregarAsync(); }
    }

    public ICommand AtualizarCommand { get; }
    public ICommand VerDanfeCommand { get; }
    public ICommand CancelarCommand { get; }
    public ICommand CopiarParaNotaAvulsaCommand { get; }

    public NfeHistoricoViewModel()
    {
        AtualizarCommand = new ERP.WPF.Commands.AsyncRelayCommand(async _ => await CarregarAsync());
        VerDanfeCommand  = new ERP.WPF.Commands.RelayCommand(item =>
        {
            if (item is NfeA4HistoricoDto n && !string.IsNullOrWhiteSpace(n.UrlDanfe))
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(n.UrlDanfe) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show($"Não consegui abrir o link: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        });
        CancelarCommand = new ERP.WPF.Commands.AsyncRelayCommand(async item =>
        {
            if (item is not NfeA4HistoricoDto n) return;
            if (n.Status != "Autorizada")
            {
                MessageBox.Show($"Só é possível cancelar uma nota Autorizada — essa está \"{n.Status}\".", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var justificativa = Microsoft.VisualBasic.Interaction.InputBox(
                "Justificativa do cancelamento (mínimo 15 caracteres — exigido pela SEFAZ):", "Cancelar Nota Fiscal", "");
            if (string.IsNullOrWhiteSpace(justificativa)) return;

            try
            {
                var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
                var resultado = await service.CancelarAsync(n.Id, justificativa);
                MessageBox.Show(resultado.Sucesso ? $"✅ {resultado.Mensagem}" : $"❌ Falha ao cancelar:\n{resultado.Mensagem}",
                    resultado.Sucesso ? "Nota Cancelada" : "Erro", MessageBoxButton.OK,
                    resultado.Sucesso ? MessageBoxImage.Information : MessageBoxImage.Error);
                if (resultado.Sucesso) await CarregarAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao cancelar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
        CopiarParaNotaAvulsaCommand = new ERP.WPF.Commands.AsyncRelayCommand(async item =>
        {
            if (item is not NfeA4HistoricoDto n) return;
            try
            {
                var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
                await service.CopiarComoRascunhoAsync(n.Id);
                MessageBox.Show("Cópia criada como rascunho — abra a tela de Nota Avulsa pra editar e emitir.",
                    "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao copiar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            IsBusy = true;
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            string? status = StatusSelecionado == "Todos" ? null : StatusSelecionado;
            var lista = await service.ListarHistoricoAsync(DataInicio, DataFim, status);

            Notas.Clear();
            foreach (var n in lista) Notas.Add(n);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "NfeHistoricoViewModel: falha ao carregar histórico");
            MessageBox.Show($"Erro ao carregar histórico: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}
