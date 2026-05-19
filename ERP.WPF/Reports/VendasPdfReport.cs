using ERP.Application.DTOs;
using ERP.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP.WPF.Reports;

public class VendasPdfReport : IDocument
{
    private readonly ReciboConfig _config;
    private readonly DateTime _inicio;
    private readonly DateTime _fim;
    private readonly string _vendedor;
    private readonly IReadOnlyList<SalesReportItemDto> _vendas;

    public VendasPdfReport(
        ReciboConfig config,
        DateTime inicio, DateTime fim,
        string vendedor,
        IEnumerable<SalesReportItemDto> vendas)
    {
        _config = config;
        _inicio = inicio;
        _fim = fim;
        _vendedor = vendedor;
        _vendas = vendas.ToList();
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Relatório de Vendas - {_config.NomeFantasia}",
        Author = "TTSoft ERP",
        CreationDate = DateTimeOffset.Now
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

            page.Header().Element(c => PdfReportBase.CabecalhoEmpresa(c, _config,
                $"Relatório de Vendas — {_inicio:dd/MM/yyyy} a {_fim:dd/MM/yyyy}" +
                (_vendedor != "Todos" ? $" | Vendedor: {_vendedor}" : "")));

            page.Content().Column(col =>
            {
                col.Spacing(0);

                // ── Resumo ─────────────────────────────────────────────────
                col.Item().PaddingVertical(8).Row(row =>
                {
                    CartaoResumo(row, "Total de Vendas", _vendas.Count.ToString(), Colors.Blue.Darken1);
                    row.ConstantItem(12).Column(_ => { });
                    CartaoResumo(row, "Receita Total",
                        $"R$ {_vendas.Sum(v => v.ValorTotal):N2}", Colors.Green.Darken1);
                    row.ConstantItem(12).Column(_ => { });
                    CartaoResumo(row, "Ticket Médio",
                        _vendas.Count > 0
                            ? $"R$ {_vendas.Average(v => v.ValorTotal):N2}"
                            : "R$ 0,00",
                        Colors.Orange.Darken1);
                });

                // ── Tabela ─────────────────────────────────────────────────
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(50);   // Nº
                        c.ConstantColumn(85);   // Data
                        c.RelativeColumn(3);    // Cliente
                        c.RelativeColumn(2);    // Vendedor
                        c.ConstantColumn(80);   // Forma Pgto
                        c.ConstantColumn(90);   // Valor
                        c.ConstantColumn(70);   // Status
                    });

                    // Cabeçalho
                    table.Header(h =>
                    {
                        CelulaHeader(h.Cell(), "Nº");
                        CelulaHeader(h.Cell(), "Data");
                        CelulaHeader(h.Cell(), "Cliente");
                        CelulaHeader(h.Cell(), "Vendedor");
                        CelulaHeader(h.Cell(), "Pagamento");
                        CelulaHeader(h.Cell(), "Valor");
                        CelulaHeader(h.Cell(), "Status");
                    });

                    // Linhas
                    bool par = false;
                    foreach (var v in _vendas)
                    {
                        var bg = par ? Colors.Grey.Lighten4 : Colors.White;
                        par = !par;

                        CelulaTexto(table.Cell().Background(bg), v.NumeroRecibo ?? "-");
                        CelulaTexto(table.Cell().Background(bg), v.DataVenda.ToString("dd/MM/yy HH:mm"));
                        CelulaTexto(table.Cell().Background(bg), v.ClienteNome ?? "Consumidor Final");
                        CelulaTexto(table.Cell().Background(bg), v.VendedorNome ?? "-");
                        CelulaTexto(table.Cell().Background(bg), v.FormaPagamento ?? "-");
                        table.Cell().Background(bg).AlignRight().Padding(4)
                             .Text($"R$ {v.ValorTotal:N2}").Bold().FontColor(Colors.Green.Darken2);
                        CelulaTexto(table.Cell().Background(bg), "-");
                    }
                });
            });

            page.Footer().AlignRight()
                .Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                    t.Span("Tecnologia por TTSoft   |   Página ");
                    t.CurrentPageNumber();
                    t.Span(" de ");
                    t.TotalPages();
                });
        });
    }

    private static void CartaoResumo(RowDescriptor row, string label, string valor, string cor)
    {
        row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2)
           .Padding(10).Column(c =>
           {
               c.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Medium);
               c.Item().Text(valor).Bold().FontSize(14).FontColor(cor);
           });
    }

    private static void CelulaHeader(IContainer cell, string texto)
    {
        cell.Background(Colors.Blue.Darken2).Padding(6)
            .Text(texto).Bold().FontColor(Colors.White).FontSize(9);
    }

    private static void CelulaTexto(IContainer cell, string texto)
    {
        cell.Padding(4).Text(texto).FontSize(9);
    }
}
