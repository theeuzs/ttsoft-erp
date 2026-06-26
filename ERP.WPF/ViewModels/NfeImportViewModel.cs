using ERP.WPF.Commands;
using System;
using System.Linq; // 👈 Adicionado para o .Any() funcionar
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;

namespace ERP.WPF.ViewModels;

public class NfeImportViewModel : BaseViewModel
{
    private readonly INfeImportService _nfeImportService;
    private NfeImportDto? _notaFiscal;

    // Essa é a "gaveta" que vai guardar a nota lida e avisar a tela para se atualizar
    public NfeImportDto? NotaFiscal
    {
        get => _notaFiscal;
        set
        {
            _notaFiscal = value;
            OnPropertyChanged(nameof(NotaFiscal));
            OnPropertyChanged(nameof(Itens)); // Atualiza a lista de produtos na tela
        }
    }

    // Atalho para o DataGrid ler os itens facilmente
    public System.Collections.IEnumerable? Itens => NotaFiscal?.Itens;

    // ID do PedidoCompra gerado na última importação
    private Guid? _pedidoCompraId;
    public Guid? PedidoCompraId
    {
        get => _pedidoCompraId;
        private set { _pedidoCompraId = value; OnPropertyChanged(); }
    }

    // Comandos dos botões
    public ICommand SelecionarXmlCommand { get; }
    public ICommand SalvarEstoqueCommand { get; }

    public NfeImportViewModel(INfeImportService nfeImportService)
    {
        _nfeImportService = nfeImportService;
        
        // Criei um comando simples nativo do WPF para não dependermos de pacotes extras
        SelecionarXmlCommand = new AsyncRelayCommand(ExecutarSelecionarXml);
        SalvarEstoqueCommand = new AsyncRelayCommand(ExecutarSalvarEstoque);
    }

    private async Task ExecutarSelecionarXml(object? obj)
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Arquivos XML (*.xml)|*.xml|Todos os arquivos (*.*)|*.*",
            Title = "Selecione o arquivo XML da Nota Fiscal"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                // 🔥 A MÁGICA ACONTECE AQUI: O motor lê o arquivo e joga na nossa gaveta!
                NotaFiscal = await _nfeImportService.LerXmlNfeAsync(openFileDialog.FileName);
            }
            catch (System.IO.FileNotFoundException ex)
            {
                MessageBox.Show($"Arquivo não encontrado:\n{ex.FileName}",
                    "Arquivo não encontrado", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Assinatura") || ex.Message.Contains("assinatura") || ex.Message.Contains("certificado") || ex.Message.Contains("adulterado"))
            {
                MessageBox.Show(
                    $"⚠️ Esta nota fiscal não passou na verificação de segurança.\n\n" +
                    $"Motivo: {ex.Message}\n\n" +
                    $"O arquivo pode ter sido modificado após a emissão. " +
                    $"Solicite o XML original diretamente ao fornecedor.",
                    "Nota Fiscal inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("SEFAZ") || ex.Message.Contains("rejeitada"))
            {
                MessageBox.Show(
                    $"⚠️ Nota fiscal rejeitada pela SEFAZ.\n\n{ex.Message}\n\n" +
                    $"Não é possível importar uma nota não autorizada.",
                    "Rejeitada pela SEFAZ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao ler o arquivo XML:\n\n{ex.Message}",
                    "Erro de Leitura", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // 👇 AGORA SIM! A função está segura DENTRO da classe NfeImportViewModel
    private async Task ExecutarSalvarEstoque(object? obj)
    {
        if (NotaFiscal == null || !NotaFiscal.Itens.Any()) 
        {
            MessageBox.Show("Leia um arquivo XML primeiro!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        try
        {
            // 👇 Chama a função nova que devolve a dupla de resultados
            var resultado = await _nfeImportService.SalvarEAtualizarEstoqueAsync(NotaFiscal);

            PedidoCompraId = resultado.PedidoCompraId;

            if (resultado.Novos > 0 || resultado.Atualizados > 0)
            {
                MessageBox.Show(
                    $"{resultado.Novos} produto(s) cadastrado(s).\n" +
                    $"{resultado.Atualizados} produto(s) com estoque atualizado.\n\n" +
                    $"✅ Pedido de Compra {resultado.PedidoCompraId.ToString()[..8].ToUpper()} gerado automaticamente!\n" +
                    $"Contas a pagar criadas conforme duplicatas da nota.",
                    "Importação concluída", MessageBoxButton.OK, MessageBoxImage.Information);

                NotaFiscal = null;
            }
            else
            {
                MessageBox.Show("Nenhum produto foi processado.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar no banco: {ex.Message}", "Erro Crítico", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

// ─── CLASSE AUXILIAR (Sempre no final e do lado de fora) ─────────────────────
public class RelayCommandAction : ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommandAction(Action<object?> execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged { add { } remove { } }
}