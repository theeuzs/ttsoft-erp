// ERP.WPF/ViewModels/ExportarNotasViewModel.cs
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Item 8 do roadmap fiscal — exportar XML/PDF de um período inteiro num
/// ZIP só, pro contador. Não depende de nenhum endpoint novo da Focus — só
/// baixa os arquivos que a Focus já hospeda (mesmas URLs dos botões PDF/XML
/// da tela F10) e empacota localmente.
/// </summary>
public class ExportarNotasViewModel : BaseViewModel
{
    public DateTime DataInicio { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime DataFim { get; set; } = DateTime.Today;

    public bool IncluirXml { get; set; } = true;
    public bool IncluirPdf { get; set; } = true;

    private string _pastaDestino = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    public string PastaDestino { get => _pastaDestino; set => SetProperty(ref _pastaDestino, value); }

    private string _statusTexto = string.Empty;
    public string StatusTexto { get => _statusTexto; set => SetProperty(ref _statusTexto, value); }

    private bool _exportando;
    public bool Exportando { get => _exportando; set => SetProperty(ref _exportando, value); }

    public event Action? OnConcluido;

    public ICommand EscolherPastaCommand { get; }
    public ICommand ExportarCommand { get; }

    public ExportarNotasViewModel()
    {
        EscolherPastaCommand = new RelayCommand(_ => EscolherPasta());
        ExportarCommand = new AsyncRelayCommand(async _ => await ExportarAsync());
    }

    private void EscolherPasta()
    {
        // Microsoft.Win32.OpenFolderDialog é nativo do WPF desde o .NET 8 —
        // evita precisar de UseWindowsForms, que quebra a resolução de tipo
        // (UserControl, KeyEventArgs, Color) no projeto inteiro por ambiguidade
        // entre System.Windows.Forms e System.Windows.
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Escolha onde salvar o ZIP",
            InitialDirectory = PastaDestino,
        };
        if (dialog.ShowDialog() == true)
            PastaDestino = dialog.FolderName;
    }

    private async Task ExportarAsync()
    {
        if (!IncluirXml && !IncluirPdf)
        {
            MessageBox.Show("Escolha pelo menos XML ou PDF pra exportar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Exportando = true;
        StatusTexto = "Buscando notas do período...";

        try
        {
            using var scope = App.Services.CreateScope();
            var saleService = scope.ServiceProvider.GetRequiredService<ISaleService>();
            var ctx = scope.ServiceProvider.GetRequiredService<ERP.Persistence.Context.AppDbContext>();

            var vendas = (await saleService.GetAllAsync(DataInicio.Date, DataFim.Date.AddDays(1).AddTicks(-1)))
                .Where(v => !string.IsNullOrEmpty(v.NfceUrlDanfe) || !string.IsNullOrEmpty(v.NfceReferencia))
                .ToList();

            if (!vendas.Any())
            {
                MessageBox.Show("Nenhuma nota encontrada nesse período.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var idsVendas = vendas.Select(v => v.Id).ToList();
            var xmlPorVenda = await ctx.NotasFiscais
                .Where(n => n.VendaId.HasValue && idsVendas.Contains(n.VendaId.Value) && n.XmlUrl != null)
                .ToDictionaryAsync(n => n.VendaId!.Value, n => n.XmlUrl!);

            string nomeArquivoZip = $"NotasFiscais_{DataInicio:yyyyMMdd}_a_{DataFim:yyyyMMdd}.zip";
            string caminhoZip = Path.Combine(PastaDestino, nomeArquivoZip);

            int totalArquivos = 0, arquivosBaixados = 0, semXml = 0, semPdf = 0;

            using var http = new HttpClient();
            using var zipStream = new FileStream(caminhoZip, FileMode.Create);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Create);

            for (int i = 0; i < vendas.Count; i++)
            {
                var v = vendas[i];
                string nomeBase = $"{v.SaleNumber ?? v.Id.ToString()[..8]}_{v.NfceNumero ?? ""}".TrimEnd('_');

                if (IncluirPdf && !string.IsNullOrWhiteSpace(v.NfceUrlDanfe))
                {
                    totalArquivos++;
                    StatusTexto = $"Baixando {i + 1}/{vendas.Count} — {nomeBase}.pdf";
                    if (await BaixarEAdicionarAoZipAsync(http, zip, v.NfceUrlDanfe, $"{nomeBase}.pdf"))
                        arquivosBaixados++;
                }
                else if (IncluirPdf) semPdf++;

                if (IncluirXml)
                {
                    if (xmlPorVenda.TryGetValue(v.Id, out var urlXml) && !string.IsNullOrWhiteSpace(urlXml))
                    {
                        totalArquivos++;
                        StatusTexto = $"Baixando {i + 1}/{vendas.Count} — {nomeBase}.xml";
                        if (await BaixarEAdicionarAoZipAsync(http, zip, urlXml, $"{nomeBase}.xml"))
                            arquivosBaixados++;
                    }
                    else semXml++;
                }
            }

            zip.Dispose();
            zipStream.Dispose();

            string aviso = "";
            if (semXml > 0) aviso += $"\n⚠️ {semXml} nota(s) sem XML disponível (emitidas antes da migração da configuração fiscal pro banco).";
            if (semPdf > 0) aviso += $"\n⚠️ {semPdf} nota(s) sem PDF disponível.";

            MessageBox.Show(
                $"✅ Exportação concluída!\n\n{arquivosBaixados} de {totalArquivos} arquivos baixados.\nSalvo em: {caminhoZip}{aviso}",
                "Exportar Notas Fiscais", MessageBoxButton.OK, MessageBoxImage.Information);

            OnConcluido?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao exportar:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Exportando = false;
            StatusTexto = string.Empty;
        }
    }

    private static async Task<bool> BaixarEAdicionarAoZipAsync(HttpClient http, ZipArchive zip, string url, string nomeNoZip)
    {
        try
        {
            var bytes = await http.GetByteArrayAsync(url);
            var entry = zip.CreateEntry(nomeNoZip, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            await entryStream.WriteAsync(bytes, 0, bytes.Length);
            return true;
        }
        catch
        {
            // Best-effort por arquivo — um PDF/XML que falhou não deve
            // interromper a exportação dos outros.
            return false;
        }
    }
}