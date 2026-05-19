using ERP.Application.DTOs;
using ERP.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP.WPF.Reports;

/// <summary>
/// Gera carnê de parcelamento em PDF (A4).
/// Cada parcela ocupa 1/3 de uma folha A4 — 3 recibos por página.
/// Inclui: número da parcela, vencimento, valor, nome do cliente e código de barras simulado.
/// </summary>
public class CarnePdfReport : IDocument
{
    private readonly ReciboConfig          _config;
    private readonly string                _nomeCliente;
    private readonly string?               _telefoneCliente;
    private readonly string                _descricao;
    private readonly List<ParcelaDto>      _parcelas;

    public CarnePdfReport(
        ReciboConfig    config,
        string          nomeCliente,
        string?         telefoneCliente,
        string          descricao,
        List<ParcelaDto> parcelas)
    {
        _config          = config;
        _nomeCliente     = nomeCliente;
        _telefoneCliente = telefoneCliente;
        _descricao       = descricao;
        _parcelas        = parcelas.OrderBy(p => p.NumeroParcela).ToList();
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title        = $"Carnê — {_nomeCliente}",
        Author       = "TTSoft ERP",
        CreationDate = DateTimeOffset.Now
    };

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        // Agrupa 3 parcelas por página
        var paginas = _parcelas
            .Select((p, i) => (parcela: p, idx: i))
            .GroupBy(x => x.idx / 3)
            .Select(g => g.Select(x => x.parcela).ToList())
            .ToList();

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(0);
            page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

            page.Content().Column(col =>
            {
                foreach (var grupo in paginas)
                {
                    // Completa até 3 parcelas por página com espaços em branco
                    while (grupo.Count < 3) grupo.Add(null!);

                    for (int i = 0; i < 3; i++)
                    {
                        var parcela = grupo[i];

                        col.Item().Height(PageSizes.A4.Height / 3).Padding(20).Element(c =>
                        {
                            if (parcela == null)
                            {
                                c.Column(_ => { }); // espaço vazio
                                return;
                            }

                            DesenharParcela(c, parcela);
                        });

                        // Linha de recorte entre parcelas (exceto depois da última)
                        if (i < 2)
                        {
                            col.Item().Height(1).Padding(0).Row(row =>
                            {
                                row.RelativeItem().Height(1)
                                   .Canvas((canvas, size) =>
                                   {
                                       var skCanvas = (SkiaSharp.SKCanvas)canvas;
                                       var paint = new SkiaSharp.SKPaint
                                       {
                                           Color       = SkiaSharp.SKColors.LightGray,
                                           StrokeWidth = 0.5f,
                                           IsStroke    = true,
                                           PathEffect  = SkiaSharp.SKPathEffect.CreateDash(new[] { 4f, 4f }, 0)
                                       };
                                       skCanvas.DrawLine(0, 0, size.Width, 0, paint);
                                   });
                            });
                        }
                    }
                }
            });
        });
    }

    private void DesenharParcela(IContainer container, ParcelaDto parcela)
    {
        var vencido  = parcela.DataVencimento.Date < DateTime.Today && parcela.Status == "Pendente";
        var pago     = parcela.Status == "Pago";
        var corStatus = pago    ? Colors.Green.Darken2 :
                        vencido ? Colors.Red.Darken2   : Colors.Blue.Darken1;

        container.Row(row =>
        {
            // ── Canhoto (esquerda) — fica com o cliente ────────────────────────
            row.ConstantItem(180).Border(0.5f).BorderColor(Colors.Grey.Lighten2)
               .Padding(10).Column(canhoto =>
            {
                canhoto.Item().Text(_config.NomeFantasia)
                    .FontSize(9).Bold().FontColor(Colors.Grey.Darken1);

                canhoto.Item().PaddingTop(4).Text($"Parcela {parcela.NumeroParcela}/{parcela.TotalParcelas}")
                    .FontSize(13).Bold().FontColor(corStatus);

                canhoto.Item().PaddingTop(2).Text($"Vencimento: {parcela.DataVencimento:dd/MM/yyyy}")
                    .FontSize(10).FontColor(Colors.Grey.Medium);

                canhoto.Item().PaddingTop(6).Text(parcela.ValorRestante.ToString("C"))
                    .FontSize(18).Bold().FontColor(corStatus);

                if (pago)
                    canhoto.Item().PaddingTop(4).Text("✓ PAGO")
                        .FontSize(10).Bold().FontColor(Colors.Green.Darken2);

                canhoto.Item().Extend();

                canhoto.Item().Text(_nomeCliente)
                    .FontSize(8).FontColor(Colors.Grey.Medium);

                if (!string.IsNullOrEmpty(_telefoneCliente))
                    canhoto.Item().Text(_telefoneCliente)
                        .FontSize(8).FontColor(Colors.Grey.Lighten1);
            });

            // ── Separador pontilhado ───────────────────────────────────────────
            row.ConstantItem(8).Column(sep =>
            {
                sep.Item().Extend().Canvas((canvas, size) =>
                {
                    var skCanvas = (SkiaSharp.SKCanvas)canvas;
                    var paint = new SkiaSharp.SKPaint
                    {
                        Color       = SkiaSharp.SKColors.LightGray,
                        StrokeWidth = 0.5f,
                        IsStroke    = true,
                        PathEffect  = SkiaSharp.SKPathEffect.CreateDash(new[] { 3f, 3f }, 0)
                    };
                    skCanvas.DrawLine(size.Width / 2, 0, size.Width / 2, size.Height, paint);
                });
            });

            // ── Recibo principal (direita) ────────────────────────────────────
            row.RelativeItem().Border(0.5f).BorderColor(Colors.Grey.Lighten2)
               .Padding(12).Column(recibo =>
            {
                // Cabeçalho
                recibo.Item().Row(cabRow =>
                {
                    cabRow.RelativeItem().Column(empresa =>
                    {
                        empresa.Item().Text(_config.NomeFantasia).FontSize(12).Bold();
                        empresa.Item().Text(_config.Endereco).FontSize(8).FontColor(Colors.Grey.Medium);
                        empresa.Item().Text(_config.Telefone).FontSize(8).FontColor(Colors.Grey.Medium);
                    });

                    cabRow.ConstantItem(80).AlignRight().Column(numParcela =>
                    {
                        numParcela.Item().Text("PARCELA").FontSize(8).FontColor(Colors.Grey.Medium);
                        numParcela.Item().Text($"{parcela.NumeroParcela}/{parcela.TotalParcelas}")
                            .FontSize(20).Bold().FontColor(corStatus);
                    });
                });

                recibo.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                // Dados do cliente e descrição
                recibo.Item().PaddingTop(6).Row(dados =>
                {
                    dados.RelativeItem().Column(cliente =>
                    {
                        cliente.Item().Text("CLIENTE").FontSize(8).FontColor(Colors.Grey.Medium);
                        cliente.Item().Text(_nomeCliente).FontSize(11).Bold();
                        cliente.Item().PaddingTop(4).Text(_descricao).FontSize(9).FontColor(Colors.Grey.Medium);
                    });

                    dados.ConstantItem(140).Column(valores =>
                    {
                        valores.Item().AlignRight().Text("VENCIMENTO").FontSize(8).FontColor(Colors.Grey.Medium);
                        valores.Item().AlignRight().Text(parcela.DataVencimento.ToString("dd/MM/yyyy"))
                            .FontSize(12).Bold();

                        valores.Item().PaddingTop(4).AlignRight().Text("VALOR").FontSize(8).FontColor(Colors.Grey.Medium);
                        valores.Item().AlignRight().Text(parcela.ValorRestante.ToString("C"))
                            .FontSize(16).Bold().FontColor(corStatus);
                    });
                });

                recibo.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                // Código de barras simulado (visual apenas)
                recibo.Item().PaddingTop(8).Height(32).Canvas((canvas, size) =>
                {
                    var rand = new Random(parcela.Id.GetHashCode());
                    var skCanvas = (SkiaSharp.SKCanvas)canvas;
                    var paint = new SkiaSharp.SKPaint
                    {
                        Color       = SkiaSharp.SKColors.Black,
                        IsStroke    = false
                    };
                    float x = 0;
                    while (x < size.Width)
                    {
                        var largura = rand.Next(1, 4);
                        var espaco  = rand.Next(1, 4);
                        skCanvas.DrawRect(new SkiaSharp.SKRect(x, 0, x + largura, size.Height), paint);
                        x += largura + espaco;
                    }
                });

                recibo.Item().PaddingTop(4).AlignCenter()
                    .Text($"*{parcela.Id.ToString().Replace("-","")[..20]}*")
                    .FontSize(7).FontColor(Colors.Grey.Medium);

                if (pago)
                {
                    recibo.Item().PaddingTop(4).Background(Colors.Green.Lighten4)
                        .Padding(6).AlignCenter()
                        .Text($"✓ PAGO EM {parcela.DataPagamento:dd/MM/yyyy}")
                        .FontSize(10).Bold().FontColor(Colors.Green.Darken2);
                }
                else if (vencido)
                {
                    recibo.Item().PaddingTop(4).Background(Colors.Red.Lighten4)
                        .Padding(6).AlignCenter()
                        .Text("⚠ VENCIDO — Sujeito a juros e multa")
                        .FontSize(9).Bold().FontColor(Colors.Red.Darken2);
                }
            });
        });
    }
}
