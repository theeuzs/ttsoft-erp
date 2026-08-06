// ERP.WPF/ViewModels/LegacyImportViewModel.cs
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Item "código morto" da auditoria — o serviço (LegacyImportService) já
/// existia pronto, parseando arquivos "vendas*" de sistema anterior, mas
/// nunca teve tela nenhuma pra disparar. Essa view liga isso.
/// </summary>
public class LegacyImportViewModel : BaseViewModel
{
    private string _pastaOrigem = string.Empty;
    public string PastaOrigem { get => _pastaOrigem; set => SetProperty(ref _pastaOrigem, value); }

    private string _resultadoTexto = string.Empty;
    public string ResultadoTexto { get => _resultadoTexto; set => SetProperty(ref _resultadoTexto, value); }

    private bool _importando;
    public bool Importando { get => _importando; set => SetProperty(ref _importando, value); }

    public ICommand EscolherPastaCommand { get; }
    public ICommand ImportarCommand { get; }

    public LegacyImportViewModel()
    {
        EscolherPastaCommand = new RelayCommand(_ => EscolherPasta());
        ImportarCommand = new AsyncRelayCommand(async _ => await ImportarAsync());
    }

    private void EscolherPasta()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Escolha a pasta com os arquivos 'vendas*' do sistema antigo",
        };
        if (dialog.ShowDialog() == true)
            PastaOrigem = dialog.FolderName;
    }

    private async Task ImportarAsync()
    {
        if (string.IsNullOrWhiteSpace(PastaOrigem))
        {
            MessageBox.Show("Escolha a pasta com os arquivos primeiro.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirmacao = MessageBox.Show(
            "Isso vai criar vendas no histórico a partir dos arquivos do sistema antigo.\n\n" +
            "⚠️ Produtos do arquivo que não existirem no seu catálogo hoje vão ser lançados usando o " +
            "PRIMEIRO produto cadastrado, só como referência genérica (o serviço já funciona assim — " +
            "não é possível mapear produto que não existe). Confira o histórico depois da importação " +
            "e corrija manualmente os itens que precisarem.\n\n" +
            "Não dá pra desfazer automaticamente. Continuar?",
            "Confirmar Importação", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmacao != MessageBoxResult.Yes) return;

        Importando = true;
        ResultadoTexto = "Importando... isso pode demorar um pouco em pastas grandes.";
        try
        {
            var service = App.Services.GetRequiredService<ILegacyImportService>();
            var resultado = await service.ImportFromFolderAsync(PastaOrigem);
            ResultadoTexto = resultado;
            MessageBox.Show(resultado, "Importação Concluída", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ResultadoTexto = $"Erro: {ex.Message}";
            MessageBox.Show($"Erro ao importar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { Importando = false; }
    }
}
