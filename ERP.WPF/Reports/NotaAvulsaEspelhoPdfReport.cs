// ERP.WPF/Reports/NotaAvulsaEspelhoPdfReport.cs
using ERP.Application.DTOs;
using ERP.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Linq;

namespace ERP.WPF.Reports;

/// <summary>
/// Item 1.4 do plano premium — "Espelho de Conferência" pra nota avulsa em
/// rascunho. Não imita o DANFE de propósito (confunde e pode ser tomado por
/// documento fiscal de verdade — a SEFAZ é quem gera DANFE, não a gente antes
/// de autorizar). Marca d'água diagonal deixa isso impossível de confundir.
/// </summary>
public class NotaAvulsaEspelhoPdfReport : IDocument
{
    private readonly NotaFiscalAvulsaDto _nota;
    private readonly ConferenciaFiscalDto _conferencia;
    private readonly ReciboConfig _config;

    public NotaAvulsaEspelhoPdfReport(NotaFiscalAvulsaDto nota, ConferenciaFiscalDto conferencia, ReciboConfig config)
    {
        _nota = nota;
        _conferencia = conferencia;
        _config = config;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

            page.Foreground().Element(ComposeMarcaDagua);

            page.Header().Element(c => PdfReportBase.CabecalhoEmpresa(c, _config,
                "ESPELHO DE CONFERÊNCIA — NÃO É DOCUMENTO FISCAL"));
            page.Content().Element(ComposeContent);
            page.Footer().Element(c => PdfReportBase.Rodape(c, 1, 1));
        });
    }

    void ComposeMarcaDagua(IContainer container)
    {
        container.AlignCenter().AlignMiddle()
            .Rotate(-30)
            .Text("RASCUNHO — SEM VALOR FISCAL")
            .FontSize(42).Bold().FontColor(Colors.Red.Lighten3);
    }

    void ComposeContent(IContainer container)
    {
        container.PaddingVertical(1, Unit.Centimetre).Column(column =>
        {
            column.Spacing(10);

            column.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
            {
                c.Item().Text("DESTINATÁRIO").SemiBold().FontSize(12);
                c.Item().Text($"Nome: {_nota.DestinatarioNome}");
                if (!string.IsNullOrWhiteSpace(_nota.DestinatarioDocumento))
                    c.Item().Text($"CPF/CNPJ: {_nota.DestinatarioDocumento}");
                var endereco = string.Join(", ", new[] {
                    _nota.DestinatarioLogradouro, _nota.DestinatarioNumero, _nota.DestinatarioBairro,
                    _nota.DestinatarioMunicipio, _nota.DestinatarioUf, _nota.DestinatarioCep
                }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(endereco))
                    c.Item().Text($"Endereço: {endereco}");
                c.Item().Text($"Natureza da Operação: {_nota.NaturezaOperacao}    Tipo: {(_nota.TipoOperacaoEntradaSaida == "E" ? "Entrada" : "Saída")}");
            });

            column.Item().Element(ComposeTabelaItens);

            column.Item().Element(ComposeBlocoImpostos);

            column.Item().AlignRight().Text($"Total dos Produtos: {_conferencia.ValorTotalProdutos:C}")
                .FontSize(14).Bold().FontColor(Colors.Grey.Darken3);
            column.Item().AlignRight().Text($"Total de Impostos (ICMS + ST): {_conferencia.ValorTotalImpostos:C}")
                .FontSize(11).FontColor(Colors.Grey.Medium);
        });
    }

    void ComposeTabelaItens(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.ConstantColumn(70);
                columns.ConstantColumn(50);
                columns.ConstantColumn(60);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
            });

            table.Header(header =>
            {
                header.Cell().Element(CellStyle).Text("PRODUTO");
                header.Cell().Element(CellStyle).Text("NCM");
                header.Cell().Element(CellStyle).Text("CFOP");
                header.Cell().Element(CellStyle).AlignRight().Text("QTD");
                header.Cell().Element(CellStyle).AlignRight().Text("V. UNIT");
                header.Cell().Element(CellStyle).AlignRight().Text("TOTAL");

                static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black);
            });

            foreach (var item in _nota.Itens)
            {
                table.Cell().Element(CellStyle).Text(item.ProductName);
                table.Cell().Element(CellStyle).Text("—"); // NCM vem do cadastro do produto, não do item da nota
                table.Cell().Element(CellStyle).Text(item.Cfop);
                table.Cell().Element(CellStyle).AlignRight().Text($"{item.Quantidade:N2}");
                table.Cell().Element(CellStyle).AlignRight().Text($"{item.ValorUnitario:C}");
                table.Cell().Element(CellStyle).AlignRight().Text($"{(item.Quantidade * item.ValorUnitario):C}");

                static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
            }
        });
    }

    void ComposeBlocoImpostos(IContainer container)
    {
        container.Background(Colors.Blue.Lighten5).Padding(10).Column(c =>
        {
            c.Item().Text("IMPOSTOS CALCULADOS (conferência interna — sujeito a validação da SEFAZ na transmissão)")
                .SemiBold().FontSize(10).FontColor(Colors.Blue.Darken2);
            c.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(90);
                    columns.ConstantColumn(90);
                });
                table.Header(header =>
                {
                    header.Cell().Text("Item").SemiBold().FontSize(9);
                    header.Cell().AlignRight().Text("ICMS").SemiBold().FontSize(9);
                    header.Cell().AlignRight().Text("ICMS-ST").SemiBold().FontSize(9);
                });
                foreach (var item in _conferencia.Itens)
                {
                    table.Cell().Text(item.ProductName).FontSize(9);
                    table.Cell().AlignRight().Text($"{item.Tributos.ValorIcms:C}").FontSize(9);
                    table.Cell().AlignRight().Text($"{item.Tributos.ValorIcmsSt:C}").FontSize(9);
                }
            });
        });
    }
}
