using ERP.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ERP.WPF.Reports;

/// <summary>
/// Métodos utilitários compartilhados por todos os relatórios PDF.
/// </summary>
public static class PdfReportBase
{
    // ── Cabeçalho padrão com dados da empresa ────────────────────────────
    public static void CabecalhoEmpresa(IContainer container, ReciboConfig config, string tituloRelatorio)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // Logo (se existir)
                if (!string.IsNullOrWhiteSpace(config.CaminhoLogo) && File.Exists(config.CaminhoLogo))
                {
                    // FIX APLICADO: Altura máxima travada em 60 e FitArea para evitar crash de aspect ratio
                    row.ConstantItem(80).Height(60).Image(config.CaminhoLogo).FitArea();
                    row.ConstantItem(12).Column(_ => { });
                }

                row.RelativeItem().Column(txt =>
                {
                    txt.Item().Text(config.NomeFantasia).Bold().FontSize(16).FontColor(Colors.Grey.Darken3);
                    txt.Item().Text(config.RazaoSocial).FontSize(11).FontColor(Colors.Grey.Medium);
                    txt.Item().Text(config.Endereco).FontSize(10).FontColor(Colors.Grey.Medium);
                    txt.Item().Text(config.Telefone).FontSize(10).FontColor(Colors.Grey.Medium);
                });
            });

            col.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

            col.Item().Row(row =>
            {
                row.RelativeItem().Text(tituloRelatorio).Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                row.ConstantItem(200).AlignRight()
                   .Text($"Emitido em {DateTime.Now:dd/MM/yyyy HH:mm}")
                   .FontSize(10).FontColor(Colors.Grey.Medium);
            });

            col.Item().PaddingBottom(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
        });
    }

    // ── Rodapé padrão ─────────────────────────────────────────────────────
    public static void Rodape(IContainer container, int paginaAtual, int totalPaginas)
    {
        container.Row(row =>
        {
            row.RelativeItem().Text("Tecnologia por ConstruTTor - TTSoft").FontSize(8).FontColor(Colors.Grey.Lighten1);
            row.ConstantItem(100).AlignRight()
               .Text($"Página {paginaAtual} de {totalPaginas}").FontSize(8).FontColor(Colors.Grey.Lighten1);
        });
    }

    // ── Salvar, abrir e exibir mensagem de sucesso ────────────────────────
    public static void SalvarEAbrir(IDocument documento, string nomeBase)
    {
        try
        {
            string pasta = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ERP_Relatorios");
            Directory.CreateDirectory(pasta);

            string caminho = Path.Combine(pasta, $"{nomeBase}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            documento.GeneratePdf(caminho);

            // Abre o PDF com o visualizador padrão do Windows
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao gerar PDF:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}