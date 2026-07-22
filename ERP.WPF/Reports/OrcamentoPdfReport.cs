using System;
using ERP.Domain.Entities;
using ERP.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.WPF.Reports;

public class OrcamentoPdfReport : IDocument
{
    private readonly Orcamento _orcamento;
    private readonly ReciboConfig _config;

    // S17 FIX: antes recebia um DadosEmpresaDto próprio (sem campo de logo
    // nenhum) — trocado por ReciboConfig, o mesmo tipo que ComprasPdfReport/
    // VendasPdfReport já usam, reaproveitando PdfReportBase.CabecalhoEmpresa
    // (que já lida com logo corretamente, inclusive travando altura/aspect
    // ratio) em vez de desenhar cabeçalho do zero sem logo nenhum.
    public OrcamentoPdfReport(Orcamento orcamento, ReciboConfig config)
    {
        _orcamento = orcamento;
        _config = config;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Element(c => PdfReportBase.CabecalhoEmpresa(c, _config,
                    $"ORÇAMENTO Nº {_orcamento.Numero}"));
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Spacing(10);

            column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
            {
                c.Item().Text("DADOS DO CLIENTE").SemiBold().FontSize(12);
                c.Item().Text($"Nome: {_orcamento.CustomerName ?? "Consumidor Final"}");
                c.Item().Text($"Emitido em: {_orcamento.DataEmissao:dd/MM/yyyy}    Válido até: {_orcamento.DataValidade:dd/MM/yyyy}");
                if (!string.IsNullOrWhiteSpace(_orcamento.Observacao))
                    c.Item().Text($"Observação: {_orcamento.Observacao}").FontSize(9).Italic();
            });

            column.Item().Element(ComposeTable);

            column.Item().AlignRight().Text($"Total Geral: {_orcamento.ValorTotal:C}").FontSize(16).Bold().FontColor(Colors.Green.Darken2);
        });
    }

    void ComposeTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();    
                columns.ConstantColumn(60);  
                columns.ConstantColumn(80);  
                columns.ConstantColumn(80);  
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("DESCRIÇÃO");
                header.Cell().Element(CellStyle).AlignRight().Text("QTD");
                header.Cell().Element(CellStyle).AlignRight().Text("V. UNIT");
                header.Cell().Element(CellStyle).AlignRight().Text("TOTAL");

                static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
            });

            if (_orcamento.Itens != null)
            {
                foreach (var item in _orcamento.Itens)
                {
                    table.Cell().Element(CellStyle).Text(item.ProductName);
                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.Quantity:N2}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{item.UnitPrice:C}");
                    table.Cell().Element(CellStyle).AlignRight().Text($"{(item.Quantity * item.UnitPrice):C}");

                    static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
                }
            }
        });
    }

    void ComposeFooter(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Text("Condições de Pagamento: À vista, Pix ou Cartão em até 6x sem juros.").FontSize(9);
            // S17 FIX: validade era um texto fixo "5 dias" — agora reflete a
            // validade real escolhida na tela de salvar (7/15/30 dias).
            column.Item().Text($"Válido até {_orcamento.DataValidade:dd/MM/yyyy}. (Sujeito a alteração de preços)").FontSize(9).Italic();
            column.Item().AlignCenter().PaddingTop(15).Text(x => { x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages(); });
        });
    }
}