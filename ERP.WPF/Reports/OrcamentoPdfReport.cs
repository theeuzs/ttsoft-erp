using System;
using ERP.Domain.Entities; // Ajuste para o namespace correto onde fica sua classe Orcamento
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.WPF.Reports;

// 👇 Colocamos o DTO aqui mesmo para facilitar
public class DadosEmpresaDto
{
    public string NomeFantasia { get; set; } = string.Empty;
    public string EnderecoCompleto { get; set; } = string.Empty;
    public string Contato { get; set; } = string.Empty;
}

public class OrcamentoPdfReport : IDocument
{
    private readonly Orcamento _orcamento;
    private readonly DadosEmpresaDto _empresa;

    public OrcamentoPdfReport(Orcamento orcamento, DadosEmpresaDto empresa)
    {
        _orcamento = orcamento;
        _empresa = empresa;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text(_empresa.NomeFantasia).FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text(_empresa.EnderecoCompleto);
                column.Item().Text(_empresa.Contato);
            });

            row.ConstantItem(120).AlignRight().Column(column =>
            {
                column.Item().Text("ORÇAMENTO").FontSize(16).SemiBold();
                column.Item().Text($"Nº: {_orcamento.Numero}").FontSize(12).Bold().FontColor(Colors.Red.Medium);
                column.Item().Text($"Data: {DateTime.Now:dd/MM/yyyy}");
            });
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
            column.Item().Text("Validade deste orçamento: 5 dias. (Sujeito a alteração de preços)").FontSize(9).Italic();
            column.Item().AlignCenter().PaddingTop(15).Text(x => { x.Span("Página "); x.CurrentPageNumber(); x.Span(" de "); x.TotalPages(); });
        });
    }
}