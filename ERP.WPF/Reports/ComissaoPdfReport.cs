using ERP.WPF.Helpers;
using ERP.WPF.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP.WPF.Reports;

public class ComissaoPdfReport : IDocument
{
    private readonly ReciboConfig _config;
    private readonly DateTime _dataInicio;
    private readonly DateTime _dataFim;
    private readonly decimal _percentual;
    private readonly decimal _totalVendido;
    private readonly decimal _totalComissao;
    private readonly IReadOnlyList<ComissaoItem> _itens;

    public ComissaoPdfReport(
        ReciboConfig config,
        DateTime dataInicio, DateTime dataFim,
        decimal percentual, decimal totalVendido, decimal totalComissao,
        IEnumerable<ComissaoItem> itens)
    {
        _config        = config;
        _dataInicio    = dataInicio;
        _dataFim       = dataFim;
        _percentual    = percentual;
        _totalVendido  = totalVendido;
        _totalComissao = totalComissao;
        _itens         = itens.ToList();
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title        = $"Comissões — {_config.NomeFantasia}",
        Author       = "TTSoft ERP",
        CreationDate = DateTimeOffset.Now
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

            page.Header().Element(c => PdfReportBase.CabecalhoEmpresa(c, _config,
                $"Fechamento de Comissões — {_dataInicio:dd/MM/yyyy} a {_dataFim:dd/MM/yyyy}"));

            page.Content().Column(col =>
            {
                col.Spacing(10);

                // ── Info do relatório ─────────────────────────────────────
                col.Item().PaddingTop(8).Background(Colors.Amber.Lighten4).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text($"Percentual de Comissão: {_percentual:N1}%").Bold();
                    row.ConstantItem(200).AlignRight()
                       .Text($"Emitido em {DateTime.Now:dd/MM/yyyy HH:mm}")
                       .FontSize(10).FontColor(Colors.Grey.Medium);
                });

                col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                // ── Tabela de vendedores ──────────────────────────────────
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);    // Vendedor
                        c.ConstantColumn(100);  // Qtd Vendas
                        c.ConstantColumn(130);  // Total Vendido
                        c.ConstantColumn(130);  // Comissão
                    });

                    // Header
                    CelulaHeader(table, "Vendedor");
                    CelulaHeader(table, "Qtd. Vendas");
                    CelulaHeader(table, "Total Vendido");
                    CelulaHeader(table, "Valor Comissão");

                    bool par = false;
                    foreach (var item in _itens)
                    {
                        var bg = par ? Colors.Grey.Lighten4 : Colors.White;
                        par = !par;

                        table.Cell().Background(bg).Padding(8).Text(item.Vendedor).Bold();
                        table.Cell().Background(bg).AlignCenter().Padding(8).Text(item.QtdVendas.ToString());
                        table.Cell().Background(bg).AlignRight().Padding(8)
                             .Text($"R$ {item.TotalVendido:N2}").FontColor(Colors.Blue.Darken1);
                        table.Cell().Background(bg).AlignRight().Padding(8)
                             .Text($"R$ {item.ValorComissao:N2}").Bold().FontColor(Colors.Green.Darken2);
                    }
                });

                // ── Totais ────────────────────────────────────────────────
                col.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().Background(Colors.Blue.Lighten4).Padding(12).Column(c =>
                    {
                        c.Item().Text("TOTAL VENDIDO (EQUIPE)").FontSize(10).FontColor(Colors.Blue.Darken2);
                        c.Item().Text($"R$ {_totalVendido:N2}").Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                    });

                    row.ConstantItem(16).Column(_ => { });

                    row.RelativeItem().Background(Colors.Green.Lighten4).Padding(12).Column(c =>
                    {
                        c.Item().Text($"TOTAL A PAGAR ({_percentual:N1}%)").FontSize(10).FontColor(Colors.Green.Darken2);
                        c.Item().Text($"R$ {_totalComissao:N2}").Bold().FontSize(16).FontColor(Colors.Green.Darken2);
                    });
                });
            });

            page.Footer().Element((c) => PdfReportBase.Rodape(c, 1, 1));
        });
    }

    private static void CelulaHeader(TableDescriptor table, string texto)
    {
        table.Cell().Background(Colors.Blue.Darken2).Padding(8)
             .Text(texto).Bold().FontColor(Colors.White).FontSize(10);
    }
}
