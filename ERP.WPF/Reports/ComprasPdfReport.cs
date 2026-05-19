using ERP.Application.DTOs;
using ERP.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP.WPF.Reports;

public class ComprasPdfReport : IDocument
{
    private readonly ReciboConfig _config;
    private readonly DateTime _dataInicio;
    private readonly DateTime _dataFim;
    private readonly IReadOnlyList<PedidoCompraDto> _pedidos;

    public ComprasPdfReport(
        ReciboConfig config,
        DateTime dataInicio, DateTime dataFim,
        IEnumerable<PedidoCompraDto> pedidos)
    {
        _config     = config;
        _dataInicio = dataInicio;
        _dataFim    = dataFim;
        _pedidos    = pedidos.ToList();
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title        = $"Relatório de Compras — {_config.NomeFantasia}",
        Author       = "TTSoft ERP",
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
                $"Relatório de Compras — {_dataInicio:dd/MM/yyyy} a {_dataFim:dd/MM/yyyy}"));

            page.Content().Column(col =>
            {
                col.Spacing(0);

                // ── Resumo ─────────────────────────────────────────────────
                col.Item().PaddingVertical(8).Row(row =>
                {
                    CartaoResumo(row, "Total de Pedidos", _pedidos.Count.ToString(), Colors.Blue.Darken1);
                    row.ConstantItem(12).Column(_ => { });
                    CartaoResumo(row, "Valor Total",
                        $"R$ {_pedidos.Sum(p => p.Total):N2}", Colors.Green.Darken1);
                    row.ConstantItem(12).Column(_ => { });
                    CartaoResumo(row, "Recebidos",
                        _pedidos.Count(p => p.StatusTexto == "Recebido").ToString(), Colors.Teal.Darken1);
                    row.ConstantItem(12).Column(_ => { });
                    CartaoResumo(row, "Pendentes",
                        _pedidos.Count(p => p.StatusTexto != "Recebido" && p.StatusTexto != "Cancelado").ToString(),
                        Colors.Orange.Darken1);
                });

                // ── Tabela ─────────────────────────────────────────────────
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(90);   // Nº
                        c.RelativeColumn(3);    // Fornecedor
                        c.ConstantColumn(90);   // Data
                        c.ConstantColumn(90);   // Previsão
                        c.ConstantColumn(90);   // Recebimento
                        c.ConstantColumn(110);  // Total
                        c.ConstantColumn(80);   // Status
                    });

                    CelulaHeader(table, "Nº Pedido");
                    CelulaHeader(table, "Fornecedor");
                    CelulaHeader(table, "Data");
                    CelulaHeader(table, "Previsão");
                    CelulaHeader(table, "Recebido em");
                    CelulaHeader(table, "Total");
                    CelulaHeader(table, "Status");

                    bool par = false;
                    foreach (var p in _pedidos)
                    {
                        var bg = par ? Colors.Grey.Lighten4 : Colors.White;
                        par = !par;

                        var corStatus = p.StatusTexto switch
                        {
                            "Recebido" => Colors.Green.Darken2,
                            "Cancelado" => Colors.Red.Darken2,
                            "Enviado" => Colors.Blue.Darken1,
                            _ => Colors.Grey.Darken1
                        };

                        table.Cell().Background(bg).Padding(5).Text(p.Numero).FontSize(9);
                        table.Cell().Background(bg).Padding(5).Text(p.FornecedorNome).FontSize(9);
                        table.Cell().Background(bg).Padding(5).Text(p.DataPedido.ToString("dd/MM/yyyy")).FontSize(9);
                        table.Cell().Background(bg).Padding(5)
                             .Text(p.DataPrevista.HasValue ? p.DataPrevista.Value.ToString("dd/MM/yyyy") : "-").FontSize(9);
                        table.Cell().Background(bg).Padding(5)
                             .Text(p.DataRecebimento.HasValue ? p.DataRecebimento.Value.ToString("dd/MM/yyyy") : "-").FontSize(9);
                        table.Cell().Background(bg).AlignRight().Padding(5)
                             .Text($"R$ {p.Total:N2}").Bold().FontSize(9).FontColor(Colors.Green.Darken2);
                        table.Cell().Background(bg).Padding(5)
                             .Text(p.StatusTexto).Bold().FontSize(9).FontColor(corStatus);
                    }
                });

                // ── Total ──────────────────────────────────────────────────
                col.Item().PaddingTop(8).AlignRight()
                   .Text($"TOTAL GERAL: R$ {_pedidos.Sum(p => p.Total):N2}")
                   .Bold().FontSize(13).FontColor(Colors.Green.Darken2);
            });

            page.Footer().AlignRight().Text(t =>
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
        row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(c =>
        {
            c.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Medium);
            c.Item().Text(valor).Bold().FontSize(14).FontColor(cor);
        });
    }

    private static void CelulaHeader(TableDescriptor table, string texto)
    {
        table.Cell().Background(Colors.Blue.Darken2).Padding(6)
             .Text(texto).Bold().FontColor(Colors.White).FontSize(9);
    }
}
