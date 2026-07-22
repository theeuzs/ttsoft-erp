using System;
using ERP.Domain.Entities;
using ERP.WPF.Helpers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ERP.WPF.Reports;

public class OrcamentoBobinaReport : IDocument
{
    private readonly Orcamento _orcamento;
    private readonly ReciboConfig _empresa;

    public OrcamentoBobinaReport(Orcamento orcamento, ReciboConfig empresa)
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
                page.ContinuousSize(80, Unit.Millimetre);
                page.Margin(4, Unit.Millimetre); // Margem um pouquinho menor para aproveitar a largura
                page.PageColor(Colors.White);
                
                // 👇 FONTE BASE MAIOR (Aumentamos de 9 para 11)
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial).FontColor(Colors.Black)); 

                page.Content().Element(ComposeContent);
            });
    }

    void ComposeContent(IContainer container)
    {
        container.Column(column =>
        {
            // --- CABEÇALHO DA EMPRESA ---
            column.Item().PaddingBottom(5).Column(c => {
                c.Item().AlignCenter().Text(_empresa.NomeFantasia).FontSize(16).Black(); // Fonte bem grande e pesada
                c.Item().AlignCenter().Text(_empresa.Telefone).FontSize(10);
            });

            // Efeito visual de linha dupla
            column.Item().LineHorizontal(1.5f, Unit.Point).LineColor(Colors.Black);
            column.Item().PaddingVertical(2).LineHorizontal(1.5f, Unit.Point).LineColor(Colors.Black); 

            // --- TÍTULO E INFORMAÇÕES ---
            column.Item().PaddingVertical(10).Column(c => { // Mais respiro aqui
                c.Item().AlignCenter().Text("DOCUMENTO NÃO FISCAL").FontSize(11).Bold();
                c.Item().AlignCenter().Text("ORÇAMENTO").FontSize(15).Black();
                
                c.Item().PaddingTop(8).Text($"Nº: {_orcamento.Numero}").Bold();
                c.Item().Text($"Data: {DateTime.Now:dd/MM/yyyy HH:mm}");
                c.Item().Text($"Cliente: {_orcamento.CustomerName ?? "Consumidor Final"}");
            });

            column.Item().LineHorizontal(1, Unit.Point).LineColor(Colors.Black);

            // --- CABEÇALHO DOS ITENS ---
            column.Item().PaddingVertical(8).Text("QTD   X   V.UNIT   =   TOTAL").FontSize(10).Bold().AlignCenter();
            
            column.Item().LineHorizontal(1, Unit.Point).LineColor(Colors.Black);

            // --- LISTA DE ITENS ---
            if (_orcamento.Itens != null)
            {
                // Um padding vertical para não grudar na linha
                column.Item().PaddingVertical(10).Column(c => 
                {
                    foreach (var item in _orcamento.Itens)
                    {
                        // Espaçamento entre um produto e outro
                        c.Item().PaddingBottom(8).Column(itemCol => 
                        {
                            // Nome do produto agora fica em NEGRITO
                            itemCol.Item().Text(item.ProductName).FontSize(11).Bold();
                            
                            // Valores alinhados à direita
                            itemCol.Item().AlignRight().Text($"{item.Quantity:N2} x {item.UnitPrice:C} = {(item.Quantity * item.UnitPrice):C}").FontSize(11);
                        });
                    }
                });
            }

            column.Item().LineHorizontal(1.5f, Unit.Point).LineColor(Colors.Black);

            // --- TOTAL ---
            // Letra GIGANTE no total para o cliente bater o olho e ver o valor
            column.Item().PaddingVertical(12).AlignRight().Text($"TOTAL: {_orcamento.ValorTotal:C}").FontSize(18).Black(); 

            column.Item().LineHorizontal(1, Unit.Point).LineColor(Colors.Black);

            // --- RODAPÉ ---
            column.Item().PaddingTop(15).PaddingBottom(10).Column(c => { // Bastante espaço no fim para não cortar feio
                c.Item().AlignCenter().Text("Validade: 5 dias").FontSize(10).Italic();
                c.Item().AlignCenter().Text("Obrigado pela preferência!").FontSize(11).SemiBold();
            });
        });
    }
}