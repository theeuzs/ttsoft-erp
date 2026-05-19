using ERP.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;

namespace ERP.WPF.Reports;

public class DrePdfReport : IDocument
{
    private readonly ReciboConfig _config;
    private readonly DateTime _dataInicio;
    private readonly DateTime _dataFim;
    private readonly decimal _receitaBruta;
    private readonly decimal _custoMercadorias;
    private readonly decimal _lucroBruto;
    private readonly decimal _despesasOperacionais;
    private readonly decimal _lucroLiquido;
    private readonly decimal _margemLucratividade;

    public DrePdfReport(
        ReciboConfig config,
        DateTime dataInicio, DateTime dataFim,
        decimal receitaBruta, decimal custoMercadorias,
        decimal lucroBruto, decimal despesasOperacionais,
        decimal lucroLiquido, decimal margemLucratividade)
    {
        _config = config;
        _dataInicio = dataInicio;
        _dataFim = dataFim;
        _receitaBruta = receitaBruta;
        _custoMercadorias = custoMercadorias;
        _lucroBruto = lucroBruto;
        _despesasOperacionais = despesasOperacionais;
        _lucroLiquido = lucroLiquido;
        _margemLucratividade = margemLucratividade;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"DRE - {_config.NomeFantasia}",
        Author = "TTSoft ERP",
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
                $"Demonstrativo de Resultado — {_dataInicio:dd/MM/yyyy} a {_dataFim:dd/MM/yyyy}"));

            page.Content().Column(col =>
            {
                col.Spacing(8);

                // ── RECEITA ────────────────────────────────────────────────
                col.Item().PaddingTop(12).Text("RECEITAS").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);
                LinhaValor(col, "Receita Bruta de Vendas", _receitaBruta, positivo: true);

                col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                // ── CUSTOS ─────────────────────────────────────────────────
                col.Item().PaddingTop(8).Text("CUSTOS").Bold().FontSize(12).FontColor(Colors.Red.Darken2);
                LinhaValor(col, "Custo das Mercadorias Vendidas (CMV)", _custoMercadorias, positivo: false);

                col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Darken1);

                // ── LUCRO BRUTO ────────────────────────────────────────────
                LinhaDestaque(col, "LUCRO BRUTO", _lucroBruto);

                col.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                // ── DESPESAS ───────────────────────────────────────────────
                col.Item().PaddingTop(8).Text("DESPESAS OPERACIONAIS").Bold().FontSize(12).FontColor(Colors.Orange.Darken2);
                LinhaValor(col, "Contas a Pagar no Período", _despesasOperacionais, positivo: false);

                col.Item().PaddingTop(4).LineHorizontal(1.5f).LineColor(Colors.Grey.Darken2);

                // ── RESULTADO FINAL ────────────────────────────────────────
                col.Item().PaddingTop(4).Background(
                    _lucroLiquido >= 0 ? Colors.Green.Lighten4 : Colors.Red.Lighten4
                ).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text("LUCRO LÍQUIDO FINAL").Bold().FontSize(13);
                    row.ConstantItem(200).AlignRight()
                       .Text($"R$ {_lucroLiquido:N2}").Bold().FontSize(13)
                       .FontColor(_lucroLiquido >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                });

                col.Item().PaddingTop(16)
                   .Background(Colors.Blue.Lighten4).Padding(10)
                   .Text($"Margem de Lucratividade: {_margemLucratividade:N1}%")
                   .Bold().FontColor(Colors.Blue.Darken2);
            });

            page.Footer().Element((c) => PdfReportBase.Rodape(c, 1, 1));
        });
    }

    private static void LinhaValor(ColumnDescriptor col, string label, decimal valor, bool positivo)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken2);
            row.ConstantItem(180).AlignRight()
               .Text($"R$ {valor:N2}")
               .FontColor(positivo ? Colors.Green.Darken1 : Colors.Red.Darken1);
        });
    }

    private static void LinhaDestaque(ColumnDescriptor col, string label, decimal valor)
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(label).Bold();
            row.ConstantItem(180).AlignRight()
               .Text($"R$ {valor:N2}").Bold()
               .FontColor(valor >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
        });
    }
}
