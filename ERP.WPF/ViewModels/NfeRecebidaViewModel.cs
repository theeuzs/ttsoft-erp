// ERP.WPF/ViewModels/NfeRecebidaViewModel.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Item MD-e do roadmap fiscal — notas de fornecedor descobertas
/// automaticamente, sem precisar pedir o XML por e-mail.
/// </summary>
public class NfeRecebidaViewModel : BaseViewModel
{
    public ObservableCollection<NfeRecebidaDto> Notas { get; } = new();

    private string _statusTexto = string.Empty;
    public string StatusTexto { get => _statusTexto; set => SetProperty(ref _statusTexto, value); }

    public ICommand BuscarNovasCommand { get; }
    public ICommand DarCienciaCommand { get; }
    public ICommand ConfirmarCommand { get; }
    public ICommand DesconhecerCommand { get; }
    public ICommand BaixarXmlCommand { get; }

    public NfeRecebidaViewModel()
    {
        BuscarNovasCommand = new AsyncRelayCommand(async _ => await BuscarNovasAsync());
        DarCienciaCommand  = new AsyncRelayCommand(async item => await ManifestarAsync(item, "ciencia"));
        ConfirmarCommand   = new AsyncRelayCommand(async item => await ManifestarAsync(item, "confirmacao"));
        DesconhecerCommand = new AsyncRelayCommand(async item => await ManifestarAsync(item, "desconhecimento"));
        BaixarXmlCommand   = new AsyncRelayCommand(async item => await BaixarXmlAsync(item));

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        try
        {
            var service = App.Services.GetRequiredService<INfeRecebidaService>();
            var lista = await service.ListarAsync();
            Notas.Clear();
            foreach (var n in lista) Notas.Add(n);
        }
        catch { /* best-effort */ }
    }

    private async Task BuscarNovasAsync()
    {
        StatusTexto = "Consultando notas emitidas contra o CNPJ...";
        try
        {
            var service = App.Services.GetRequiredService<INfeRecebidaService>();
            var quantidade = await service.BuscarNovasAsync();
            await CarregarAsync();

            MessageBox.Show(quantidade > 0
                ? $"✅ {quantidade} nota(s) nova(s) ou atualizada(s) encontrada(s)."
                : "Nenhuma nota nova encontrada.",
                "Buscar Notas", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao buscar notas: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { StatusTexto = string.Empty; }
    }

    private async Task ManifestarAsync(object? item, string tipo)
    {
        if (item is not NfeRecebidaDto nota) return;

        string? justificativa = null;
        if (tipo == "nao_realizada")
        {
            justificativa = Microsoft.VisualBasic.Interaction.InputBox(
                "Justificativa (mínimo 15 caracteres):", "Operação Não Realizada", "");
            if (string.IsNullOrWhiteSpace(justificativa)) return;
        }

        try
        {
            var service = App.Services.GetRequiredService<INfeRecebidaService>();
            await service.ManifestarAsync(nota.Id, tipo, justificativa);
            await CarregarAsync();
            MessageBox.Show("✅ Manifestação registrada!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao manifestar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task BaixarXmlAsync(object? item)
    {
        if (item is not NfeRecebidaDto nota) return;

        try
        {
            var service = App.Services.GetRequiredService<INfeRecebidaService>();
            var caminho = await service.BaixarXmlParaImportacaoAsync(nota.Id);
            await CarregarAsync();

            var abrir = MessageBox.Show(
                $"✅ XML baixado!\n\n{caminho}\n\nQuer abrir a tela de Importar XML agora, com esse arquivo pronto?",
                "Sucesso", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (abrir == MessageBoxResult.Yes)
            {
                // Abre a pasta com o arquivo selecionado — o usuário arrasta
                // pra tela de Importar XML que já existe (NfeImportView),
                // sem precisar duplicar a lógica de entrada de estoque que
                // já está lá.
                Process.Start("explorer.exe", $"/select,\"{caminho}\"");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao baixar XML: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
