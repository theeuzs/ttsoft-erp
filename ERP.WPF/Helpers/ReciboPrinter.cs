using System;
using Serilog;
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

public static class ReciboPrinter
{
    // ── Cores reaproveitadas do OrcamentoPrinter, pra consistência visual
    // entre os dois documentos A4 que o sistema gera. ──────────────────────
    private static readonly Color CorPrimaria    = (Color)ColorConverter.ConvertFromString("#1E293B");
    private static readonly Color CorAcento      = (Color)ColorConverter.ConvertFromString("#1E62A6");
    private static readonly Color CorLinhaAltern = (Color)ColorConverter.ConvertFromString("#F8FAFC");
    private static readonly Color CorBorda       = (Color)ColorConverter.ConvertFromString("#E2E8F0");
    private static readonly Color CorVerde       = (Color)ColorConverter.ConvertFromString("#059669");

    public static FrameworkElement GerarPainelDoRecibo(
    Guid idVenda,
    IEnumerable<ViewModels.CartItem> listaItens,
    decimal valorTotal,
    decimal desconto,
    string nomeCliente,
    string nomeVendedor,
    IEnumerable<(string Forma, decimal Valor)> pagamentos,
    decimal troco,
    string enderecoOuObservacao,
    string tipoDocumento = "VENDA",
    DateTime? dataVenda = null,
    string? numeroVenda = null,
    string? observacaoGeral = null) // ← Observação geral do pedido
{
        var pagina = new Border 
        { 
            Width = 280, 
            Background = Brushes.White,
            Padding = new Thickness(5, 5, 15, 5) 
        };

        var painel = new StackPanel();
        pagina.Child = painel;

        var config = ConfiguracaoService.Carregar();

        // Lógica da Logo
        if (!string.IsNullOrWhiteSpace(config.CaminhoLogo) && System.IO.File.Exists(config.CaminhoLogo))
        {
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; 
                bmp.UriSource = new Uri(config.CaminhoLogo, UriKind.Absolute);
                bmp.EndInit();

                var img = new Image 
                { 
                    Source = bmp, 
                    Width = 180, 
                    MaxHeight = 120,
                    Margin = new Thickness(0, 0, 0, 10),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Stretch = System.Windows.Media.Stretch.Uniform
                };
                painel.Children.Add(img);
            }
            catch (Exception ex) { Log.Warning(ex, "ReciboPrinter: falha ao carregar o logo da empresa"); }
        }

        AddTextoCentrado(painel, config.RazaoSocial, 16, true);
        AddTextoCentrado(painel, config.NomeFantasia, 16, true);
        AddTextoCentrado(painel, config.Telefone, 14, true);
        AddTextoCentrado(painel, config.Endereco, 12, true);

        DateTime dataParaExibir;
        if (dataVenda.HasValue)
        {
            // A data já vem salva no fuso correto do Brasil pelo DateTime.Now
            dataParaExibir = dataVenda.Value; 
        }
        else
        {
            dataParaExibir = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();
        }
        AddTextoCentrado(painel, $"Data/Hora: {dataParaExibir:dd/MM/yyyy HH:mm}", 12, false);

        string numeroExibicao = !string.IsNullOrWhiteSpace(numeroVenda) ? numeroVenda : idVenda.ToString().Substring(0, 8).ToUpper(); 
        
        AddTextoCentrado(painel, $"VENDA: {numeroExibicao}", 14, true);
        
        AddSeparador(painel);

        if (tipoDocumento == "ORÇAMENTO")
        {
            AddTextoCentrado(painel, "*** ORÇAMENTO ***", 18, true);
            AddSeparador(painel);
            
            if (!string.IsNullOrWhiteSpace(nomeCliente) && nomeCliente != "Consumidor Final")
            {
                AddTexto(painel, $"CLIENTE: {nomeCliente.ToUpper()}", 13, true);
            }
            AddTexto(painel, $"VENDEDOR: {(string.IsNullOrWhiteSpace(nomeVendedor) ? "NÃO IDENTIFICADO" : nomeVendedor.ToUpper())}", 13, true);
        }
        else
        {
            AddTexto(painel, $"VENDEDOR: {(string.IsNullOrWhiteSpace(nomeVendedor) ? "BALCÃO" : nomeVendedor.ToUpper())}", 13, true);
            if (!string.IsNullOrWhiteSpace(nomeCliente) && nomeCliente != "Consumidor Final")
            {
                AddTexto(painel, $"CLIENTE: {nomeCliente.ToUpper()}", 13, true);
            }
        }
        
        // ── Observação Geral do Pedido ──────────────────────────────────
        if (!string.IsNullOrWhiteSpace(observacaoGeral))
        {
            AddTexto(painel, "OBS. DO PEDIDO:", 13, true);
            AddTexto(painel, observacaoGeral.ToUpper(), 13, false);
            AddSeparador(painel);
        }

        int index = 1;
        decimal subtotalGeral = 0;
        foreach (var item in listaItens)
        {
            // 1. Imprime o Nome do Produto
            AddTexto(painel, $"{index}. {item.ProductName.ToUpper()}", 13, true);
            
            // 2. Imprime a Quantidade, Preço e Total
            // Se tem conversão de unidade, exibe o label correto (ex: "Folha(s)"), senão usa unidade padrão
            string unidadeLabel = !string.IsNullOrWhiteSpace(item.LabelUnidadeVenda)
                ? item.LabelUnidadeVenda
                : (!string.IsNullOrWhiteSpace(item.UnidadeEstoque) ? item.UnidadeEstoque : "UN");

            // Quantidade a exibir no recibo
            decimal qtdExibir = item.Quantity;
            string qtdFormatada = (qtdExibir % 1 == 0)
                ? qtdExibir.ToString("0")
                : qtdExibir.ToString("0.##");

            // SEMPRE usa item.Total (que vem de TotalItem salvo no banco = valor exato do carrinho)
            decimal totalExibir = item.Total;

            // S22 (17/08) — pra item de atacado ("barra cravada"), o preço
            // unitário exibido antes era um valor DERIVADO (Total/Quantidade,
            // arredondado pra 2 casas) — a linha "12 MT x 9,98 = 119,80" não
            // fecha (12×9,98=119,76), porque R$9,98 nunca foi o preço real por
            // unidade, é só uma aproximação fiscal do preço do pacote fechado
            // (ver S18). Depois de duas rodadas de feedback: nada de
            // "pacote"/"barra", e nem de decompor em linhas — só quantidade
            // total e valor total, igual qualquer outro item. O cliente
            // comprou 8 MT e pagou R$84,90; como o sistema chegou nesse valor
            // internamente é regra de precificação, não precisa aparecer aqui.
            //
            // Generalizado pra reimpressão/F5 (17/08): reimprimir (tecla Y no
            // PDV) e visualizar/reimprimir no Histórico de Vendas reconstroem
            // o item SEM WholesaleMinQuantity/WholesalePrice (só a venda ao
            // vivo tem esses dados) — então checar item.IsWholesaleActive não
            // funcionaria nesses casos. Em vez disso, detecta pela matemática:
            // se o preço unitário derivado (Total/Quantidade) não reproduz o
            // Total batendo exato, é sinal de que não existe um preço unitário
            // real por trás desse valor (atacado ou qualquer outro motivo) —
            // mesmo critério, sem precisar saber POR QUE não bate.
            decimal unitDerivado = qtdExibir > 0 ? Math.Round(totalExibir / qtdExibir, 2) : item.UnitPrice;
            bool multiplicacaoFecha = qtdExibir > 0 && Math.Round(unitDerivado * qtdExibir, 2) == Math.Round(totalExibir, 2);

            if (multiplicacaoFecha)
            {
                string calculo = $"{qtdFormatada} {unidadeLabel} x {unitDerivado:N2} = {totalExibir:N2}";
                AddLinhaDupla(painel, "", calculo, true, 13);
            }
            else
            {
                AddLinhaDupla(painel, $"{qtdFormatada} {unidadeLabel}", $"{totalExibir:N2}", true, 13);
            }
            
            // 3. A MÁGICA DA OBSERVAÇÃO ENTRA AQUI!
            if (!string.IsNullOrWhiteSpace(item.Observacao))
            {
                AddTexto(painel, $"    Obs: {item.Observacao.ToUpper()}", 12, true);
            }
            
            subtotalGeral += item.Total;
            index++;
        }
        AddSeparador(painel);

        AddLinhaDupla(painel, "SUBTOTAL:", subtotalGeral.ToString("N2"), true, 14);
        
        if (desconto > 0)
        {
            AddLinhaDupla(painel, "DESCONTO:", $"- {desconto:N2}", true, 14);
        }

        decimal totalFinal = subtotalGeral - desconto;
        AddLinhaDupla(painel, "TOTAL:", totalFinal.ToString("N2"), true, 16);
        AddSeparador(painel);

        if (tipoDocumento != "ORÇAMENTO")
        {
            AddTexto(painel, "PAGAMENTO:", 14, true);
            foreach (var pag in pagamentos)
            {
                string formaBonita = pag.Forma.ToUpper() switch
                {
                    "CARTAOCREDITO" => "CARTÃO DE CRÉDITO",
                    "CARTAODEBITO" => "CARTÃO DE DÉBITO",
                    "APRAZO" => "A PRAZO",
                    "HAVER" => "SALDO HAVER",
                    _ => pag.Forma.ToUpper() 
                };

                AddLinhaDupla(painel, formaBonita, pag.Valor.ToString("N2"), true, 14);
            }

            if (troco > 0)
            {
                AddLinhaDupla(painel, "TROCO:", troco.ToString("N2"), true, 14);
            }
            AddSeparador(painel);
        }

        // A MÁGICA DO CORTE: Separando o Imposto do Endereço
        if (!string.IsNullOrWhiteSpace(enderecoOuObservacao))
        {
            if (tipoDocumento == "ORÇAMENTO")
            {
                AddTextoCentrado(painel, enderecoOuObservacao.ToUpper(), 14, true);
                AddSeparador(painel);
            }
            else
            {
                string textoEndereco = enderecoOuObservacao;
                string textoImposto = "";

                // Procura onde começa o texto da lei
                int indexImposto = enderecoOuObservacao.ToUpper().IndexOf("TRIB. APROX");
                
                // Se achou a palavra, corta a string em duas partes
                if (indexImposto >= 0)
                {
                    textoEndereco = enderecoOuObservacao.Substring(0, indexImposto).Trim();
                    textoImposto = enderecoOuObservacao.Substring(indexImposto).Trim();
                }

                // 1. Imprime o endereço normal (se existir)
                if (!string.IsNullOrWhiteSpace(textoEndereco))
                {
                    AddTexto(painel, "ENDEREÇO DE ENTREGA:", 14, true);
                    AddTexto(painel, textoEndereco.ToUpper(), 14, true);
                }

                // 2. Imprime o imposto miudinho e centralizado (Tamanho 9, sem negrito)
                if (!string.IsNullOrWhiteSpace(textoImposto))
                {
                    AddTextoCentrado(painel, textoImposto.ToUpper(), 9, false);
                }

                AddSeparador(painel);
            }
        }

        if (!string.IsNullOrWhiteSpace(config.RodapeLinha1)) AddTextoCentrado(painel, config.RodapeLinha1, 12, true);
        if (!string.IsNullOrWhiteSpace(config.RodapeLinha2)) AddTextoCentrado(painel, config.RodapeLinha2, 12, false);
        if (!string.IsNullOrWhiteSpace(config.RodapeLinha3)) AddTextoCentrado(painel, config.RodapeLinha3, 12, true);

        AddSeparador(painel);
        AddTextoCentrado(painel, "Tecnologia por TTSoft", 10, true);
        AddTextoCentrado(painel, "CNPJ: 65.183.796/0001-00", 10, false);
        AddTextoCentrado(painel, "WhatsApp: (41) 99627-2846", 10, false);

        painel.Children.Add(new DefaultTextBlock { Height = 40 });

        return pagina; 
    }

    public static void Imprimir(
    Guid idVenda,
    IEnumerable<ViewModels.CartItem> listaItens,
    decimal valorTotal,
    decimal desconto,
    string nomeCliente,
    string nomeVendedor,
    IEnumerable<(string Forma, decimal Valor)> pagamentos,
    decimal troco,
    string enderecoOuObservacao,
    string tipoDocumento = "VENDA",
    DateTime? dataVenda = null,
    string? numeroVenda = null,
    string? observacaoGeral = null)
{
        var printDialog = new PrintDialog();

        LocalPrintServer ps = new LocalPrintServer();
        printDialog.PrintQueue = ps.DefaultPrintQueue;

        var pagina = GerarPainelDoRecibo(idVenda, listaItens, valorTotal, desconto, nomeCliente, nomeVendedor, pagamentos, troco, enderecoOuObservacao, tipoDocumento, dataVenda, numeroVenda, observacaoGeral);

        // ── FIX: Calcula a altura REAL do conteúdo antes de imprimir ──────────
        // PrintVisual cortava o recibo no item 21 porque usava a altura do papel
        // padrão da impressora (ex: A4 = 297mm). Agora forçamos o tamanho exato.
        pagina.Measure(new Size(280, double.PositiveInfinity));
        pagina.Arrange(new Rect(new Point(0, 0), pagina.DesiredSize));

        double alturaReal = pagina.DesiredSize.Height;

        // Seta o tamanho do papel dinamicamente com a altura real do conteúdo
        // Width = 80mm em DPI de tela (280px ≈ 74mm, adequado para impressora térmica 80mm)
        printDialog.PrintTicket.PageMediaSize = new PageMediaSize(
            PageMediaSizeName.Unknown, 280, alturaReal);

        printDialog.PrintVisual(pagina, $"Recibo_{idVenda}");
    }

    public static void Visualizar(
        Guid idVenda,
        IEnumerable<ViewModels.CartItem> listaItens,
        decimal valorTotal,
        decimal desconto,
        string nomeCliente,
        string nomeVendedor,
        IEnumerable<(string Forma, decimal Valor)> pagamentos,
        decimal troco,
        string enderecoOuObservacao,
        string tipoDocumento = "VENDA",
        DateTime? dataVenda = null,
        string? numeroVenda = null,
        string? observacaoGeral = null)
    {
        // 👇 CORREÇÃO: Repassando o numeroVenda pra cá! 👇
        var pagina = GerarPainelDoRecibo(idVenda, listaItens, valorTotal, desconto, nomeCliente, nomeVendedor, pagamentos, troco, enderecoOuObservacao, tipoDocumento, dataVenda, numeroVenda, observacaoGeral);

        var janelaPreview = new Window
        {
            Title = $"Visualização do Recibo #{(string.IsNullOrWhiteSpace(numeroVenda) ? idVenda.ToString().Substring(0, 8) : numeroVenda)}",
            Width = 350,
            Height = 650,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")), 
            Content = new ScrollViewer 
            { 
                Content = pagina, 
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 20, 0, 20)
            }
        };
        
        janelaPreview.ShowDialog();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Achado ao vivo (09/09) — pedido de 131 itens cortava na impressora
    // térmica (limite de altura de página, do driver ou do Windows). Ideia do
    // Matheus: dar uma opção A4, reaproveitando o mesmo padrão do
    // OrcamentoPrinter (FlowDocument + DocumentPaginator = paginação nativa
    // do WPF, sem depender de calcular altura ou adivinhar limite nenhum de
    // impressora). Fica só no menu "mais ações" da tela de Vendas (F5), não
    // no fluxo normal do caixa — o cupom térmico continua sendo o padrão de
    // toda venda comum.
    // ═══════════════════════════════════════════════════════════════════════

    public static void ImprimirA4(
        Guid idVenda,
        IEnumerable<ViewModels.CartItem> listaItens,
        decimal valorTotal,
        decimal desconto,
        string nomeCliente,
        string nomeVendedor,
        IEnumerable<(string Forma, decimal Valor)> pagamentos,
        decimal troco,
        string enderecoOuObservacao,
        string tipoDocumento = "VENDA",
        DateTime? dataVenda = null,
        string? numeroVenda = null,
        string? observacaoGeral = null)
    {
        var dlg = new System.Windows.Controls.PrintDialog();
        if (dlg.ShowDialog() != true) return;
        var doc = GerarFlowDocumentA4(idVenda, listaItens, valorTotal, desconto, nomeCliente, nomeVendedor,
            pagamentos, troco, enderecoOuObservacao, tipoDocumento, dataVenda, numeroVenda, observacaoGeral,
            dlg.PrintableAreaWidth);
        dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator,
            $"Recibo {numeroVenda ?? idVenda.ToString().Substring(0, 8)}");
    }

    public static void VisualizarA4(
        Guid idVenda,
        IEnumerable<ViewModels.CartItem> listaItens,
        decimal valorTotal,
        decimal desconto,
        string nomeCliente,
        string nomeVendedor,
        IEnumerable<(string Forma, decimal Valor)> pagamentos,
        decimal troco,
        string enderecoOuObservacao,
        string tipoDocumento = "VENDA",
        DateTime? dataVenda = null,
        string? numeroVenda = null,
        string? observacaoGeral = null)
    {
        var w = new Window
        {
            Title  = $"Recibo A4 — {(string.IsNullOrWhiteSpace(numeroVenda) ? idVenda.ToString().Substring(0, 8) : numeroVenda)}",
            Width  = 840, Height = 900,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(Colors.LightGray)
        };
        var doc = GerarFlowDocumentA4(idVenda, listaItens, valorTotal, desconto, nomeCliente, nomeVendedor,
            pagamentos, troco, enderecoOuObservacao, tipoDocumento, dataVenda, numeroVenda, observacaoGeral, 793);
        w.Content = new FlowDocumentReader { Document = doc };
        w.ShowDialog();
    }

    private static FlowDocument GerarFlowDocumentA4(
        Guid idVenda,
        IEnumerable<ViewModels.CartItem> listaItens,
        decimal valorTotal,
        decimal desconto,
        string nomeCliente,
        string nomeVendedor,
        IEnumerable<(string Forma, decimal Valor)> pagamentos,
        decimal troco,
        string enderecoOuObservacao,
        string tipoDocumento,
        DateTime? dataVenda,
        string? numeroVenda,
        string? observacaoGeral,
        double larguraImpressora)
    {
        var cfg = ConfiguracaoService.Carregar();
        var pageWidth = Math.Max(larguraImpressora, 793);
        var doc = new FlowDocument
        {
            FontFamily  = new FontFamily("Segoe UI"),
            FontSize    = 11,
            PagePadding = new Thickness(60, 50, 60, 50),
            ColumnWidth = double.MaxValue,
            PageWidth   = pageWidth
        };
        double larguraConteudo = pageWidth - 120;

        DateTime dataParaExibir = dataVenda ?? ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();
        string numeroExibicao = !string.IsNullOrWhiteSpace(numeroVenda)
            ? numeroVenda : idVenda.ToString().Substring(0, 8).ToUpper();

        doc.Blocks.Add(CabecalhoA4(cfg, tipoDocumento, numeroExibicao, dataParaExibir, larguraConteudo));
        doc.Blocks.Add(DivisoriaA4());
        doc.Blocks.Add(DadosVendaA4(nomeCliente, nomeVendedor, observacaoGeral, larguraConteudo));
        doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 10, 0, 6) });
        doc.Blocks.Add(TabelaItensA4(listaItens, larguraConteudo, out decimal subtotalGeral));
        doc.Blocks.Add(BlocoTotaisPagamentoA4(subtotalGeral, desconto, pagamentos, troco, tipoDocumento, larguraConteudo));
        if (!string.IsNullOrWhiteSpace(enderecoOuObservacao))
            doc.Blocks.Add(EnderecoOuObservacaoA4(enderecoOuObservacao, tipoDocumento));
        doc.Blocks.Add(RodapeA4(cfg));
        return doc;
    }

    private static Block CabecalhoA4(ReciboConfig cfg, string tipoDocumento, string numero, DateTime data, double larguraConteudo)
    {
        const double larguraDireita = 220;
        var table = new Table { CellSpacing = 0 };
        table.Columns.Add(new TableColumn { Width = new GridLength(Math.Max(200, larguraConteudo - larguraDireita)) });
        table.Columns.Add(new TableColumn { Width = new GridLength(larguraDireita) });
        var rg = new TableRowGroup();
        var row = new TableRow();

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
            catch (Exception ex) { Log.Warning(ex, "ReciboPrinter (A4): falha ao carregar o logo da empresa"); }
        }
        sec.Blocks.Add(PA4(cfg.NomeFantasia, 18, FontWeights.Black, CorPrimaria));
        sec.Blocks.Add(PA4(cfg.RazaoSocial,  10, FontWeights.Normal, Colors.Gray));
        sec.Blocks.Add(PA4(cfg.Endereco,     10, FontWeights.Normal, Colors.Gray));
        sec.Blocks.Add(PA4(cfg.Telefone,     10, FontWeights.Normal, Colors.Gray));
        esq.Blocks.Add(sec);
        row.Cells.Add(esq);

        var dir = new TableCell { Padding = new Thickness(0) };
        var sec2 = new Section();
        var titulo = tipoDocumento == "ORÇAMENTO" ? "ORÇAMENTO" : "RECIBO DE VENDA";
        var pT = new Paragraph { TextAlignment = TextAlignment.Right };
        pT.Inlines.Add(new Run(titulo) { FontSize = 20, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(CorAcento) });
        sec2.Blocks.Add(pT);
        var pN = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
        pN.Inlines.Add(new Run($"Nº {numero}") { FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(CorPrimaria) });
        sec2.Blocks.Add(pN);
        var pD = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 2, 0, 0) };
        pD.Inlines.Add(new Run($"{data:dd/MM/yyyy HH:mm}") { FontSize = 10, Foreground = new SolidColorBrush(Colors.Gray) });
        sec2.Blocks.Add(pD);
        dir.Blocks.Add(sec2);
        row.Cells.Add(dir);

        rg.Rows.Add(row);
        table.RowGroups.Add(rg);
        return table;
    }

    private static Block DivisoriaA4()
    {
        var p = new Paragraph { Margin = new Thickness(0, 12, 0, 12) };
        p.Inlines.Add(new Run(new string('─', 120)) { Foreground = new SolidColorBrush(CorBorda), FontSize = 8 });
        return p;
    }

    private static Block DadosVendaA4(string nomeCliente, string nomeVendedor, string? observacaoGeral, double larguraConteudo)
    {
        var sec = new Section();
        var linha = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
        linha.Inlines.Add(new Run("VENDEDOR: ") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.Gray) });
        linha.Inlines.Add(new Run(string.IsNullOrWhiteSpace(nomeVendedor) ? "BALCÃO" : nomeVendedor.ToUpper()) { FontSize = 11, Foreground = new SolidColorBrush(CorPrimaria) });
        sec.Blocks.Add(linha);

        if (!string.IsNullOrWhiteSpace(nomeCliente) && nomeCliente != "Consumidor Final")
        {
            var linhaCli = new Paragraph { Margin = new Thickness(0, 0, 0, 4) };
            linhaCli.Inlines.Add(new Run("CLIENTE: ") { FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.Gray) });
            linhaCli.Inlines.Add(new Run(nomeCliente.ToUpper()) { FontSize = 11, Foreground = new SolidColorBrush(CorPrimaria) });
            sec.Blocks.Add(linhaCli);
        }

        if (!string.IsNullOrWhiteSpace(observacaoGeral))
        {
            sec.Blocks.Add(PA4("OBS. DO PEDIDO", 9, FontWeights.Bold, Colors.Gray));
            sec.Blocks.Add(PA4(observacaoGeral.ToUpper(), 11, FontWeights.Normal, CorPrimaria));
        }
        return sec;
    }

    /// <summary>Mesma lógica de exibição do item que o cupom térmico (S22,
    /// 17/08) — se o preço unitário derivado (Total/Quantidade) não
    /// reproduz o Total batendo exato, mostra só quantidade e valor total,
    /// sem inventar um "preço unitário" que não existe de verdade (caso de
    /// atacado/barra cravada). Mantém os dois formatos consistentes.</summary>
    private static Block TabelaItensA4(IEnumerable<ViewModels.CartItem> listaItens, double larguraConteudo, out decimal subtotalGeral)
    {
        const double larguraFixas = 60 + 100 + 100; // QTD/UN + PREÇO UNIT. + TOTAL
        var table = new Table { CellSpacing = 0, BorderBrush = new SolidColorBrush(CorBorda), BorderThickness = new Thickness(1) };
        table.Columns.Add(new TableColumn { Width = new GridLength(Math.Max(180, larguraConteudo - larguraFixas)) });
        table.Columns.Add(new TableColumn { Width = new GridLength(60) });
        table.Columns.Add(new TableColumn { Width = new GridLength(100) });
        table.Columns.Add(new TableColumn { Width = new GridLength(100) });

        var rgH = new TableRowGroup();
        var rH = new TableRow { Background = new SolidColorBrush(CorAcento) };
        foreach (var (h, align) in new[] {
            ("DESCRIÇÃO", TextAlignment.Left),
            ("QTD/UN", TextAlignment.Right),
            ("PREÇO UNIT.", TextAlignment.Right),
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

        var rgI = new TableRowGroup();
        subtotalGeral = 0;
        int i = 0;
        foreach (var item in listaItens)
        {
            string unidadeLabel = !string.IsNullOrWhiteSpace(item.LabelUnidadeVenda)
                ? item.LabelUnidadeVenda
                : (!string.IsNullOrWhiteSpace(item.UnidadeEstoque) ? item.UnidadeEstoque : "UN");

            decimal qtdExibir = item.Quantity;
            string qtdFormatada = (qtdExibir % 1 == 0) ? qtdExibir.ToString("0") : qtdExibir.ToString("0.##");
            decimal totalExibir = item.Total;

            decimal unitDerivado = qtdExibir > 0 ? Math.Round(totalExibir / qtdExibir, 2) : item.UnitPrice;
            bool multiplicacaoFecha = qtdExibir > 0 && Math.Round(unitDerivado * qtdExibir, 2) == Math.Round(totalExibir, 2);

            var bg = i % 2 == 0 ? Colors.White : CorLinhaAltern;
            var r = new TableRow { Background = new SolidColorBrush(bg) };

            TableCell Cel(string txt, TextAlignment al = TextAlignment.Right)
            {
                var c = new TableCell { Padding = new Thickness(8, 5, 8, 5), BorderBrush = new SolidColorBrush(CorBorda), BorderThickness = new Thickness(0, 0, 0, 1) };
                var p2 = new Paragraph { TextAlignment = al };
                p2.Inlines.Add(new Run(txt) { FontSize = 11 });
                if (!string.IsNullOrWhiteSpace(item.Observacao))
                    p2.Inlines.Add(new Run($"\nObs: {item.Observacao.ToUpper()}") { FontSize = 9, Foreground = new SolidColorBrush(Colors.Gray) });
                c.Blocks.Add(p2);
                return c;
            }

            string descricao = $"{i + 1}. {item.ProductName.ToUpper()}";
            r.Cells.Add(Cel(descricao, TextAlignment.Left));
            r.Cells.Add(Cel($"{qtdFormatada} {unidadeLabel}"));
            r.Cells.Add(Cel(multiplicacaoFecha ? unitDerivado.ToString("N2") : "—"));
            r.Cells.Add(Cel(totalExibir.ToString("N2")));

            rgI.Rows.Add(r);
            subtotalGeral += item.Total;
            i++;
        }
        table.RowGroups.Add(rgI);
        return table;
    }

    private static Block BlocoTotaisPagamentoA4(decimal subtotalGeral, decimal desconto,
        IEnumerable<(string Forma, decimal Valor)> pagamentos, decimal troco, string tipoDocumento, double larguraConteudo)
    {
        const double larguraDireita = 290;
        var total = subtotalGeral - desconto;

        var outer = new Table { CellSpacing = 0, Margin = new Thickness(0, 12, 0, 0) };
        outer.Columns.Add(new TableColumn { Width = new GridLength(Math.Max(180, larguraConteudo - larguraDireita)) });
        outer.Columns.Add(new TableColumn { Width = new GridLength(larguraDireita) });
        var rg = new TableRowGroup();
        var row = new TableRow();

        // Esquerda — forma(s) de pagamento (venda) ou vazio (orçamento)
        var cEsq = new TableCell { Padding = new Thickness(0, 12, 16, 0) };
        if (tipoDocumento != "ORÇAMENTO" && pagamentos.Any())
        {
            var secPag = new Section();
            secPag.Blocks.Add(PA4("PAGAMENTO", 9, FontWeights.Bold, Colors.Gray));
            foreach (var pag in pagamentos)
            {
                string formaBonita = pag.Forma.ToUpper() switch
                {
                    "CARTAOCREDITO" => "Cartão de Crédito",
                    "CARTAODEBITO"  => "Cartão de Débito",
                    "APRAZO"        => "A Prazo",
                    "HAVER"         => "Saldo Haver",
                    _               => pag.Forma
                };
                secPag.Blocks.Add(PA4($"{formaBonita}: {pag.Valor:N2}", 11, FontWeights.Normal, CorPrimaria));
            }
            if (troco > 0) secPag.Blocks.Add(PA4($"Troco: {troco:N2}", 11, FontWeights.Normal, CorPrimaria));
            cEsq.Blocks.Add(secPag);
        }
        else cEsq.Blocks.Add(new Paragraph());
        row.Cells.Add(cEsq);

        // Direita — totais, mesmo padrão visual do OrcamentoPrinter
        var cTot = new TableCell
        {
            Padding = new Thickness(12),
            Background = new SolidColorBrush(CorLinhaAltern),
            BorderBrush = new SolidColorBrush(CorBorda),
            BorderThickness = new Thickness(1)
        };
        void LinhaTot(string lbl, string val, bool destaque = false)
        {
            var p = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 2, 0, 2) };
            p.Inlines.Add(new Run($"{lbl}  ") { FontSize = destaque ? 11 : 10, FontWeight = destaque ? FontWeights.Black : FontWeights.Normal, Foreground = new SolidColorBrush(destaque ? CorPrimaria : Colors.Gray) });
            p.Inlines.Add(new Run(val) { FontSize = destaque ? 15 : 11, FontWeight = destaque ? FontWeights.Black : FontWeights.Normal, Foreground = new SolidColorBrush(destaque ? CorVerde : CorPrimaria) });
            cTot.Blocks.Add(p);
        }
        LinhaTot("Subtotal:", subtotalGeral.ToString("N2"));
        if (desconto > 0) LinhaTot("Desconto:", $"- {desconto:N2}");
        var sep = new Paragraph { TextAlignment = TextAlignment.Right };
        sep.Inlines.Add(new Run(new string('─', 38)) { FontSize = 8, Foreground = new SolidColorBrush(CorBorda) });
        cTot.Blocks.Add(sep);
        LinhaTot("TOTAL:", total.ToString("N2"), destaque: true);
        row.Cells.Add(cTot);

        rg.Rows.Add(row);
        outer.RowGroups.Add(rg);
        return outer;
    }

    private static Block EnderecoOuObservacaoA4(string enderecoOuObservacao, string tipoDocumento)
    {
        var sec = new Section { Margin = new Thickness(0, 16, 0, 0) };
        if (tipoDocumento == "ORÇAMENTO")
        {
            sec.Blocks.Add(PA4(enderecoOuObservacao.ToUpper(), 11, FontWeights.Normal, CorPrimaria));
            return sec;
        }

        string textoEndereco = enderecoOuObservacao;
        string textoImposto = "";
        int indexImposto = enderecoOuObservacao.ToUpper().IndexOf("TRIB. APROX");
        if (indexImposto >= 0)
        {
            textoEndereco = enderecoOuObservacao.Substring(0, indexImposto).Trim();
            textoImposto = enderecoOuObservacao.Substring(indexImposto).Trim();
        }

        if (!string.IsNullOrWhiteSpace(textoEndereco))
        {
            sec.Blocks.Add(PA4("ENDEREÇO DE ENTREGA", 9, FontWeights.Bold, Colors.Gray));
            sec.Blocks.Add(PA4(textoEndereco.ToUpper(), 11, FontWeights.Normal, CorPrimaria));
        }
        if (!string.IsNullOrWhiteSpace(textoImposto))
            sec.Blocks.Add(PA4(textoImposto.ToUpper(), 8, FontWeights.Normal, Colors.Gray));

        return sec;
    }

    private static Block RodapeA4(ReciboConfig cfg)
    {
        var sec = new Section { Margin = new Thickness(0, 20, 0, 0) };
        var sep = new Paragraph { TextAlignment = TextAlignment.Center };
        sep.Inlines.Add(new Run(new string('─', 120)) { Foreground = new SolidColorBrush(CorBorda), FontSize = 8 });
        sec.Blocks.Add(sep);

        void Centro(string texto, double size, FontWeight peso)
        {
            if (string.IsNullOrWhiteSpace(texto)) return;
            var p = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 2) };
            p.Inlines.Add(new Run(texto) { FontSize = size, FontWeight = peso, Foreground = new SolidColorBrush(Colors.Gray) });
            sec.Blocks.Add(p);
        }
        Centro(cfg.RodapeLinha1, 10, FontWeights.Bold);
        Centro(cfg.RodapeLinha2, 10, FontWeights.Normal);
        Centro(cfg.RodapeLinha3, 10, FontWeights.Bold);
        Centro("Tecnologia por TTSoft — CNPJ: 65.183.796/0001-00 — WhatsApp: (41) 99627-2846", 8, FontWeights.Normal);
        return sec;
    }

    private static Paragraph PA4(string txt, double size, FontWeight weight, Color cor)
    {
        var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
        p.Inlines.Add(new Run(txt) { FontSize = size, FontWeight = weight, Foreground = new SolidColorBrush(cor) });
        return p;
    }

    private class DefaultTextBlock : TextBlock
    {
        public DefaultTextBlock()
        {
            FontFamily = new FontFamily("Arial");
            Foreground = Brushes.Black;
        }
    }

    private static void AddTexto(StackPanel painel, string texto, int tamanho, bool negrito)
    {
        painel.Children.Add(new DefaultTextBlock 
        { 
            Text = texto, 
            FontSize = tamanho, 
            FontWeight = negrito ? FontWeights.Black : FontWeights.Normal,
            Margin = new Thickness(0, 2, 0, 2),
            TextWrapping = TextWrapping.Wrap
        });
    }

    private static void AddTextoCentrado(StackPanel painel, string texto, int tamanho, bool negrito)
    {
        painel.Children.Add(new DefaultTextBlock 
        { 
            Text = texto, 
            FontSize = tamanho, 
            FontWeight = negrito ? FontWeights.Black : FontWeights.Normal,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2),
            TextWrapping = TextWrapping.Wrap
        });
    }

    private static void AddSeparador(StackPanel painel)
    {
        painel.Children.Add(new DefaultTextBlock 
        { 
            Text = "------------------------------------------", 
            FontSize = 12, 
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 5, 0, 5)
        });
    }

    private static void AddLinhaDupla(StackPanel painel, string esquerda, string direita, bool negrito, int tamanho)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var txtEsq = new DefaultTextBlock { Text = esquerda, FontSize = tamanho, FontWeight = negrito ? FontWeights.Black : FontWeights.Normal };
        var txtDir = new DefaultTextBlock { Text = direita, FontSize = tamanho, FontWeight = negrito ? FontWeights.Black : FontWeights.Normal };

        Grid.SetColumn(txtEsq, 0);
        Grid.SetColumn(txtDir, 1);

        grid.Children.Add(txtEsq);
        grid.Children.Add(txtDir);
        painel.Children.Add(grid);
    }
}