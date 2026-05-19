using ERP.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;

namespace ERP.WPF.Reports;

public class ResumoCaixaPdfReport : IDocument
{
    private readonly ReciboConfig _config;
    private readonly string _numeroCaixa;
    private readonly string _operador;
    private readonly DateTime _data;
    private readonly string _status;
    private readonly decimal _saldoInicial;
    private readonly decimal _vendasDinheiro;
    private readonly decimal _vendasPix;
    private readonly decimal _vendasCartaoDebito;
    private readonly decimal _vendasCartaoCredito;
    private readonly decimal _vendasAPrazo;
    private readonly decimal _vendasHaver;
    private readonly decimal _suprimentos;
    private readonly decimal _sangrias;
    private readonly decimal _totalMovimentado;
    private readonly decimal _totalEmEspecie;
    private readonly IReadOnlyList<string> _extrato;

    public ResumoCaixaPdfReport(
        ReciboConfig config,
        string numeroCaixa, string operador, DateTime data, string status,
        decimal saldoInicial, decimal vendasDinheiro, decimal vendasPix,
        decimal vendasCartaoDebito, decimal vendasCartaoCredito,
        decimal vendasAPrazo, decimal vendasHaver,
        decimal suprimentos, decimal sangrias,
        decimal totalMovimentado, decimal totalEmEspecie,
        IReadOnlyList<string> extrato)
    {
        _config             = config;
        _numeroCaixa        = numeroCaixa;
        _operador           = operador;
        _data               = data;
        _status             = status;
        _saldoInicial       = saldoInicial;
        _vendasDinheiro     = vendasDinheiro;
        _vendasPix          = vendasPix;
        _vendasCartaoDebito = vendasCartaoDebito;
        _vendasCartaoCredito= vendasCartaoCredito;
        _vendasAPrazo       = vendasAPrazo;
        _vendasHaver        = vendasHaver;
        _suprimentos        = suprimentos;
        _sangrias           = sangrias;
        _totalMovimentado   = totalMovimentado;
        _totalEmEspecie     = totalEmEspecie;
        _extrato            = extrato;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title    = $"Resumo de Caixa — {_config.NomeFantasia}",
        Author   = "TTSoft ERP",
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
                $"Resumo de Caixa {_numeroCaixa} — {_data:dd/MM/yyyy}"));

            page.Content().Column(col =>
            {
                col.Spacing(10);

                // ── Info do caixa ────────────────────────────────────────
                col.Item().PaddingTop(8).Row(row =>
                {
                    InfoBox(row, "Operador",    _operador);
                    InfoBox(row, "Data",        _data.ToString("dd/MM/yyyy"));
                    InfoBox(row, "Status",      _status);
                    InfoBox(row, "Saldo Inicial", $"R$ {_saldoInicial:N2}");
                });

                col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                // ── Vendas por forma ─────────────────────────────────────
                col.Item().Text("VENDAS POR FORMA DE PAGAMENTO")
                   .Bold().FontSize(12).FontColor(Colors.Blue.Darken2);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                    });

                    LinhaTabela(table, "Dinheiro",        _vendasDinheiro,     header: true);
                    LinhaTabela(table, "PIX",             _vendasPix);
                    LinhaTabela(table, "Cartão Débito",   _vendasCartaoDebito);
                    LinhaTabela(table, "Cartão Crédito",  _vendasCartaoCredito);
                    LinhaTabela(table, "A Prazo",         _vendasAPrazo);
                    LinhaTabela(table, "Haver",           _vendasHaver);
                });

                // ── Movimentações ────────────────────────────────────────
                col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                col.Item().Text("MOVIMENTAÇÕES").Bold().FontSize(12).FontColor(Colors.Blue.Darken2);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3);
                        c.RelativeColumn(2);
                    });
                    LinhaTabela(table, "Suprimentos",  _suprimentos,  header: true);
                    LinhaTabela(table, "Sangrias",      _sangrias);
                });

                // ── Totais ───────────────────────────────────────────────
                col.Item().PaddingTop(4).Background(Colors.Blue.Lighten4).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text("TOTAL MOVIMENTADO").Bold().FontSize(13);
                    row.ConstantItem(150).AlignRight()
                       .Text($"R$ {_totalMovimentado:N2}").Bold().FontSize(13)
                       .FontColor(Colors.Blue.Darken2);
                });

                col.Item().Background(Colors.Green.Lighten4).Padding(10).Row(row =>
                {
                    row.RelativeItem().Text("TOTAL EM ESPÉCIE (Conferência)").Bold().FontSize(13);
                    row.ConstantItem(150).AlignRight()
                       .Text($"R$ {_totalEmEspecie:N2}").Bold().FontSize(13)
                       .FontColor(Colors.Green.Darken2);
                });

                // ── Extrato ──────────────────────────────────────────────
                if (_extrato.Count > 0)
                {
                    col.Item().PaddingTop(8).Text("EXTRATO DETALHADO")
                       .Bold().FontSize(12).FontColor(Colors.Blue.Darken2);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => c.RelativeColumn());
                        bool par = false;
                        foreach (var linha in _extrato)
                        {
                            table.Cell()
                                 .Background(par ? Colors.Grey.Lighten4 : Colors.White)
                                 .Padding(4).Text(linha).FontSize(9);
                            par = !par;
                        }
                    });
                }
            });

            page.Footer().Element((c) => PdfReportBase.Rodape(c, 1, 1));
        });
    }

    private static void InfoBox(RowDescriptor row, string label, string value)
    {
        row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
        {
            c.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Medium);
            c.Item().Text(value).Bold().FontSize(11);
        });
        row.ConstantItem(4).Column(_ => {});
    }

    private static void LinhaTabela(TableDescriptor table, string label, decimal valor, bool header = false)
    {
        var bg = header ? Colors.Grey.Lighten3 : Colors.White;
        table.Cell().Background(bg).Padding(6).Text(label).FontSize(10);
        table.Cell().Background(bg).AlignRight().Padding(6)
             .Text($"R$ {valor:N2}").FontSize(10)
             .FontColor(valor > 0 ? Colors.Green.Darken2 : Colors.Grey.Medium);
    }
}
