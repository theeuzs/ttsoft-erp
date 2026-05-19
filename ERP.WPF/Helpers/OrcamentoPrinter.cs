using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Printing;

namespace ERP.WPF.Helpers;

/// <summary>
/// Imprime/visualiza orçamentos em formato A4 profissional.
/// Diferente do ReciboPrinter (cupom 80mm térmica), este gera documento formal A4.
/// </summary>
public static class OrcamentoPrinter
{
    private static readonly Color CorPrimaria    = (Color)ColorConverter.ConvertFromString("#1E293B");
    private static readonly Color CorAcento      = (Color)ColorConverter.ConvertFromString("#1E62A6");
    private static readonly Color CorLinhaAltern = (Color)ColorConverter.ConvertFromString("#F8FAFC");
    private static readonly Color CorBorda       = (Color)ColorConverter.ConvertFromString("#E2E8F0");
    private static readonly Color CorVerde       = (Color)ColorConverter.ConvertFromString("#059669");

    public record OrcamentoParaImprimir(
        string   Numero,
        DateTime DataEmissao,
        DateTime DataValidade,
        string   ClienteNome,
        string?  ClienteTelefone,
        string?  ClienteEmail,
        string?  ClienteEndereco,
        string   VendedorNome,
        string?  Observacoes,
        string?  CondicoesPagamento,
        List<ItemOrcamento> Itens,
        decimal  Desconto = 0);

    public record ItemOrcamento(
        string  Descricao,
        decimal Quantidade,
        string  Unidade,
        decimal PrecoUnitario,
        decimal DescontoPercent = 0);

    // ── Pontos de entrada ─────────────────────────────────────────────────────

    public static void Imprimir(OrcamentoParaImprimir orc)
    {
        var dlg = new System.Windows.Controls.PrintDialog();
        if (dlg.ShowDialog() != true) return;
        var doc = GerarFlowDocument(orc, dlg.PrintableAreaWidth);
        dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,
                          $"Orçamento {orc.Numero}");
    }

    public static void Visualizar(OrcamentoParaImprimir orc)
    {
        var w = new Window
        {
            Title  = $"Orçamento {orc.Numero} — {orc.ClienteNome}",
            Width  = 840, Height = 900,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Colors.LightGray)
        };
        w.Content = new FlowDocumentReader { Document = GerarFlowDocument(orc, 793) };
        w.ShowDialog();
    }

    // ── Documento ─────────────────────────────────────────────────────────────

    private static FlowDocument GerarFlowDocument(OrcamentoParaImprimir orc, double largura)
    {
        var cfg = ConfiguracaoService.Carregar();
        var doc = new FlowDocument
        {
            FontFamily  = new FontFamily("Segoe UI"),
            FontSize    = 11,
            PagePadding = new Thickness(60, 50, 60, 50),
            ColumnWidth = double.MaxValue,
            PageWidth   = Math.Max(largura, 793)
        };

        doc.Blocks.Add(Cabecalho(cfg, orc));
        doc.Blocks.Add(Divisoria());
        doc.Blocks.Add(DadosCliente(orc));
        doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 10, 0, 6) });
        doc.Blocks.Add(TabelaItens(orc));
        doc.Blocks.Add(BlocoTotais(orc));
        doc.Blocks.Add(CondicoesObs(orc));
        doc.Blocks.Add(Assinatura(orc));
        return doc;
    }

    private static Block Cabecalho(ReciboConfig cfg, OrcamentoParaImprimir orc)
    {
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(190) });
        var rg = new TableRowGroup();
        var row = new TableRow();

        // Esquerda — logo + empresa
        var esq = new TableCell { Padding = new Thickness(0, 0, 16, 0) };
        var sec = new Section();
        if (File.Exists(cfg.CaminhoLogo))
        {
            try
            {
                var img = new Image
                {
                    Source = new BitmapImage(new Uri(cfg.CaminhoLogo, UriKind.Absolute)),
                    Height = 50, Stretch = System.Windows.Media.Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                sec.Blocks.Add(new BlockUIContainer(img) { Margin = new Thickness(0, 0, 0, 6) });
            }
            catch { }
        }
        sec.Blocks.Add(P(cfg.NomeFantasia, 18, FontWeights.Black, CorPrimaria));
        sec.Blocks.Add(P(cfg.RazaoSocial,  10, FontWeights.Normal, Colors.Gray));
        sec.Blocks.Add(P(cfg.Endereco,     10, FontWeights.Normal, Colors.Gray));
        sec.Blocks.Add(P(cfg.Telefone,     10, FontWeights.Normal, Colors.Gray));
        esq.Blocks.Add(sec);
        row.Cells.Add(esq);

        // Direita — título ORÇAMENTO + número
        var dir = new TableCell { Padding = new Thickness(0) };
        var sec2 = new Section();
        var pT = new Paragraph { TextAlignment = TextAlignment.Right };
        pT.Inlines.Add(new Run("ORÇAMENTO") { FontSize = 22, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(CorAcento) });
        sec2.Blocks.Add(pT);
        var pN = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
        pN.Inlines.Add(new Run($"Nº {orc.Numero}") { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(CorPrimaria) });
        sec2.Blocks.Add(pN);
        dir.Blocks.Add(sec2);
        row.Cells.Add(dir);

        rg.Rows.Add(row);
        table.RowGroups.Add(rg);
        return table;
    }

    private static Block Divisoria()
    {
        var p = new Paragraph { Margin = new Thickness(0, 12, 0, 12) };
        p.Inlines.Add(new Run(new string('─', 120)) { Foreground = new SolidColorBrush(CorBorda), FontSize = 8 });
        return p;
    }

    private static Block DadosCliente(OrcamentoParaImprimir orc)
    {
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(210) });
        var rg = new TableRowGroup();
        var row = new TableRow();

        // Cliente
        var cCli = new TableCell
        {
            Padding = new Thickness(12),
            Background = new SolidColorBrush(CorLinhaAltern),
            BorderBrush = new SolidColorBrush(CorBorda),
            BorderThickness = new Thickness(1)
        };
        cCli.Blocks.Add(P("CLIENTE", 9, FontWeights.Bold, Colors.Gray));
        cCli.Blocks.Add(P(orc.ClienteNome, 13, FontWeights.Bold, CorPrimaria));
        if (!string.IsNullOrEmpty(orc.ClienteTelefone)) cCli.Blocks.Add(P($"📞 {orc.ClienteTelefone}", 10, FontWeights.Normal, Colors.Gray));
        if (!string.IsNullOrEmpty(orc.ClienteEmail))    cCli.Blocks.Add(P($"✉ {orc.ClienteEmail}", 10, FontWeights.Normal, Colors.Gray));
        if (!string.IsNullOrEmpty(orc.ClienteEndereco)) cCli.Blocks.Add(P($"📍 {orc.ClienteEndereco}", 10, FontWeights.Normal, Colors.Gray));
        row.Cells.Add(cCli);

        // Datas
        var cDat = new TableCell
        {
            Padding = new Thickness(12),
            Background = new SolidColorBrush(CorLinhaAltern),
            BorderBrush = new SolidColorBrush(CorBorda),
            BorderThickness = new Thickness(0, 1, 1, 1)
        };
        void Linha(string lbl, string val)
        {
            var p = new Paragraph { Margin = new Thickness(0, 2, 0, 2), TextAlignment = TextAlignment.Right };
            p.Inlines.Add(new Run($"{lbl}  ") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.Gray) });
            p.Inlines.Add(new Run(val) { FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(CorPrimaria) });
            cDat.Blocks.Add(p);
        }
        Linha("EMISSÃO:",  orc.DataEmissao.ToString("dd/MM/yyyy"));
        Linha("VALIDADE:", orc.DataValidade.ToString("dd/MM/yyyy"));
        Linha("VENDEDOR:", orc.VendedorNome);
        row.Cells.Add(cDat);

        rg.Rows.Add(row);
        table.RowGroups.Add(rg);
        return table;
    }

    private static Block TabelaItens(OrcamentoParaImprimir orc)
    {
        var table = new Table
        {
            CellSpacing    = 0,
            BorderBrush    = new SolidColorBrush(CorBorda),
            BorderThickness = new Thickness(1)
        };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(55) });
        table.Columns.Add(new TableColumn { Width = new GridLength(38) });
        table.Columns.Add(new TableColumn { Width = new GridLength(90) });
        table.Columns.Add(new TableColumn { Width = new GridLength(55) });
        table.Columns.Add(new TableColumn { Width = new GridLength(95) });

        // Cabeçalho
        var rgH = new TableRowGroup();
        var rH  = new TableRow { Background = new SolidColorBrush(CorAcento) };
        foreach (var (h, align) in new[] {
            ("DESCRIÇÃO", TextAlignment.Left),
            ("QTD", TextAlignment.Right),
            ("UN", TextAlignment.Right),
            ("PREÇO UNIT.", TextAlignment.Right),
            ("DESC.", TextAlignment.Right),
            ("TOTAL", TextAlignment.Right) })
        {
            var c = new TableCell { Padding = new Thickness(8, 6, 8, 6) };
            var p = new Paragraph { TextAlignment = align };
            p.Inlines.Add(new Run(h) { Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 10 });
            c.Blocks.Add(p);
            rH.Cells.Add(c);
        }
        rgH.Rows.Add(rH);
        table.RowGroups.Add(rgH);

        // Itens
        var rgI = new TableRowGroup();
        for (int i = 0; i < orc.Itens.Count; i++)
        {
            var item  = orc.Itens[i];
            var total = item.Quantidade * item.PrecoUnitario * (1 - item.DescontoPercent / 100m);
            var bg    = i % 2 == 0 ? Colors.White : CorLinhaAltern;
            var r     = new TableRow { Background = new SolidColorBrush(bg) };

            TableCell Cel(string txt, TextAlignment al = TextAlignment.Right)
            {
                var c = new TableCell
                {
                    Padding = new Thickness(8, 5, 8, 5),
                    BorderBrush = new SolidColorBrush(CorBorda),
                    BorderThickness = new Thickness(0, 0, 0, 1)
                };
                var p2 = new Paragraph { TextAlignment = al };
                p2.Inlines.Add(new Run(txt) { FontSize = 11 });
                c.Blocks.Add(p2);
                return c;
            }

            r.Cells.Add(Cel(item.Descricao, TextAlignment.Left));
            r.Cells.Add(Cel(item.Quantidade.ToString("F2")));
            r.Cells.Add(Cel(item.Unidade));
            r.Cells.Add(Cel(item.PrecoUnitario.ToString("C")));
            r.Cells.Add(Cel(item.DescontoPercent > 0 ? $"{item.DescontoPercent:F1}%" : "—"));
            r.Cells.Add(Cel(total.ToString("C")));

            rgI.Rows.Add(r);
        }
        table.RowGroups.Add(rgI);
        return table;
    }

    private static Block BlocoTotais(OrcamentoParaImprimir orc)
    {
        var subtotal = orc.Itens.Sum(i => i.Quantidade * i.PrecoUnitario * (1 - i.DescontoPercent / 100m));
        var total    = subtotal - orc.Desconto;

        var outer = new Table { CellSpacing = 0, Margin = new Thickness(0) };
        outer.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        outer.Columns.Add(new TableColumn { Width = new GridLength(290) });
        var rg = new TableRowGroup();
        var row = new TableRow();
        row.Cells.Add(new TableCell { Blocks = { new Paragraph() } });

        var cTot = new TableCell
        {
            Padding = new Thickness(12),
            Background = new SolidColorBrush(CorLinhaAltern),
            BorderBrush = new SolidColorBrush(CorBorda),
            BorderThickness = new Thickness(1, 0, 1, 1)
        };

        void LinhaTot(string lbl, string val, bool destaque = false)
        {
            var p = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 2, 0, 2) };
            p.Inlines.Add(new Run($"{lbl}  ") { FontSize = destaque ? 11 : 10, FontWeight = destaque ? FontWeights.Black : FontWeights.Normal, Foreground = new SolidColorBrush(destaque ? CorPrimaria : Colors.Gray) });
            p.Inlines.Add(new Run(val) { FontSize = destaque ? 15 : 11, FontWeight = destaque ? FontWeights.Black : FontWeights.Normal, Foreground = new SolidColorBrush(destaque ? CorVerde : CorPrimaria) });
            cTot.Blocks.Add(p);
        }

        LinhaTot("Subtotal:", subtotal.ToString("C"));
        if (orc.Desconto > 0) LinhaTot("Desconto:", $"- {orc.Desconto:C}");
        var sep = new Paragraph { TextAlignment = TextAlignment.Right };
        sep.Inlines.Add(new Run(new string('─', 38)) { FontSize = 8, Foreground = new SolidColorBrush(CorBorda) });
        cTot.Blocks.Add(sep);
        LinhaTot("TOTAL:", total.ToString("C"), destaque: true);

        row.Cells.Add(cTot);
        rg.Rows.Add(row);
        outer.RowGroups.Add(rg);
        return outer;
    }

    private static Block CondicoesObs(OrcamentoParaImprimir orc)
    {
        var sec = new Section { Margin = new Thickness(0, 16, 0, 0) };
        if (!string.IsNullOrEmpty(orc.CondicoesPagamento))
        {
            sec.Blocks.Add(P("CONDIÇÕES DE PAGAMENTO", 9, FontWeights.Bold, Colors.Gray));
            sec.Blocks.Add(P(orc.CondicoesPagamento, 11, FontWeights.Normal, CorPrimaria));
            sec.Blocks.Add(new Paragraph { Margin = new Thickness(0, 6, 0, 6) });
        }
        if (!string.IsNullOrEmpty(orc.Observacoes))
        {
            sec.Blocks.Add(P("OBSERVAÇÕES", 9, FontWeights.Bold, Colors.Gray));
            sec.Blocks.Add(P(orc.Observacoes, 11, FontWeights.Normal, CorPrimaria));
            sec.Blocks.Add(new Paragraph { Margin = new Thickness(0, 6, 0, 6) });
        }
        sec.Blocks.Add(P($"Orçamento válido até {orc.DataValidade:dd/MM/yyyy}. Preços sujeitos a alteração após esta data.", 9, FontWeights.Normal, Colors.Gray));
        return sec;
    }

    private static Block Assinatura(OrcamentoParaImprimir orc)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 36, 0, 0) };
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        table.Columns.Add(new TableColumn { Width = new GridLength(40) });
        table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });
        var rg = new TableRowGroup();
        var row = new TableRow();

        TableCell CelAss(string label)
        {
            var c = new TableCell { Padding = new Thickness(0, 24, 0, 0), BorderBrush = new SolidColorBrush(CorPrimaria), BorderThickness = new Thickness(0, 1, 0, 0) };
            var p = new Paragraph { TextAlignment = TextAlignment.Center };
            p.Inlines.Add(new Run(label) { FontSize = 9, Foreground = new SolidColorBrush(Colors.Gray) });
            c.Blocks.Add(p);
            return c;
        }

        row.Cells.Add(CelAss($"Vendedor: {orc.VendedorNome}"));
        row.Cells.Add(new TableCell { Blocks = { new Paragraph() } });
        row.Cells.Add(CelAss($"Cliente: {orc.ClienteNome}"));
        rg.Rows.Add(row);
        table.RowGroups.Add(rg);
        return table;
    }

    private static Paragraph P(string txt, double size, FontWeight weight, Color cor)
    {
        var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        p.Inlines.Add(new Run(txt) { FontSize = size, FontWeight = weight, Foreground = new SolidColorBrush(cor) });
        return p;
    }
}
