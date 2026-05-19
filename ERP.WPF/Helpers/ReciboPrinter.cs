using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Printing; 

namespace ERP.WPF.Helpers;

public static class ReciboPrinter
{
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
            catch { }
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
            dataParaExibir = DateTime.Now;
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
            // Recalcula o unitário a partir do total para nunca ter diferença de centavos
            decimal totalExibir = item.Total;
            decimal unitExibir  = qtdExibir > 0 ? Math.Round(totalExibir / qtdExibir, 2) : item.UnitPrice;
            string calculo = $"{qtdFormatada} {unidadeLabel} x {unitExibir:N2} = {totalExibir:N2}";
            AddLinhaDupla(painel, "", calculo, true, 13);
            
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