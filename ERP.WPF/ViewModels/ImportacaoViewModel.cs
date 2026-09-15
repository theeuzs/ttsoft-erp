using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Importador de dados de sistema anterior (CSV de produtos/clientes, com
/// saldo de fiado) — apontado como prioridade nº1 de feature nova em várias
/// auditorias/roadmaps: sem isso, implantação de cliente novo exige digitar
/// o catálogo inteiro na mão. Fluxo em 2 passos: escolher arquivo → prévia
/// (mostra o que vai acontecer, sem gravar nada) → confirmar (grava de
/// verdade). Nunca grava sem o usuário ver a prévia primeiro.
/// </summary>
public class ImportacaoViewModel : BaseViewModel
{
    private readonly IImportacaoService _importacaoService;

    public ImportacaoViewModel(IImportacaoService importacaoService)
    {
        _importacaoService = importacaoService;
        SelecionarArquivoCommand = new AsyncRelayCommand(_ => SelecionarArquivoAsync());
        ConfirmarImportacaoCommand = new AsyncRelayCommand(_ => ConfirmarImportacaoAsync(), _ => Previa?.PodeImportar == true);
        TipoSelecionado = TipoImportacao.Produtos;
    }

    private TipoImportacao _tipoSelecionado;
    public TipoImportacao TipoSelecionado
    {
        get => _tipoSelecionado;
        set { SetProperty(ref _tipoSelecionado, value); Previa = null; Resultado = null; }
    }

    private string? _nomeArquivo;
    public string? NomeArquivo { get => _nomeArquivo; set => SetProperty(ref _nomeArquivo, value); }

    private PreviaImportacaoDto? _previa;
    public PreviaImportacaoDto? Previa
    {
        get => _previa;
        set
        {
            SetProperty(ref _previa, value);
            OnPropertyChanged(nameof(TemPrevia));
            OnPropertyChanged(nameof(LinhasPreview));
        }
    }

    public bool TemPrevia => Previa != null;

    // Mostra só as primeiras 50 linhas na tela — arquivo pode ter milhares,
    // não faz sentido renderizar tudo, a validação já rodou em cima de todas.
    public ObservableCollection<LinhaImportacao>? LinhasPreview =>
        Previa == null ? null : new ObservableCollection<LinhaImportacao>(Previa.Linhas.Take(50));

    private ResultadoImportacaoDto? _resultado;
    public ResultadoImportacaoDto? Resultado { get => _resultado; set => SetProperty(ref _resultado, value); }

    private bool _processando;
    public bool Processando { get => _processando; set => SetProperty(ref _processando, value); }

    public ICommand SelecionarArquivoCommand { get; }
    public ICommand ConfirmarImportacaoCommand { get; }

    private async Task SelecionarArquivoAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Arquivos CSV (*.csv)|*.csv|Todos os arquivos (*.*)|*.*",
            Title = "Selecione o arquivo exportado do sistema anterior"
        };

        if (dialog.ShowDialog() != true) return;

        Processando = true;
        Resultado = null;
        try
        {
            var bytes = await File.ReadAllBytesAsync(dialog.FileName);
            NomeArquivo = System.IO.Path.GetFileName(dialog.FileName);
            Previa = await _importacaoService.PreVisualizarAsync(bytes, TipoSelecionado);

            if (Previa.CamposObrigatoriosFaltando.Count > 0)
            {
                var campos = string.Join(", ", Previa.CamposObrigatoriosFaltando);
                MessageBox.Show(
                    $"Não encontrei a coluna de \"{campos}\" no arquivo.\n\n" +
                    $"Colunas encontradas no arquivo: {string.Join(", ", Previa.ColunasDetectadasNoArquivo)}\n\n" +
                    "Renomeie a coluna correspondente na planilha e tente de novo.",
                    "Coluna obrigatória não encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não consegui ler o arquivo:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { Processando = false; }
    }

    private async Task ConfirmarImportacaoAsync()
    {
        if (Previa == null) return;

        var confirmar = MessageBox.Show(
            $"Vai importar {Previa.TotalNovosRegistros} registro(s) novo(s) e " +
            $"atualizar {Previa.TotalAtualizacoes} já existente(s).\n\n" +
            $"{Previa.TotalComErro} linha(s) com erro serão puladas.\n\n" +
            "Confirma?",
            "Confirmar importação", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmar != MessageBoxResult.Yes) return;

        Processando = true;
        try
        {
            Resultado = await _importacaoService.ConfirmarImportacaoAsync(Previa);
            Previa = null;
            NomeArquivo = null;
            MessageBox.Show(
                $"Importação concluída.\n\nCriados: {Resultado.Criados}\nAtualizados: {Resultado.Atualizados}\nCom erro: {Resultado.ComErro}",
                "Importação concluída", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Falha ao importar:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { Processando = false; }
    }
}
