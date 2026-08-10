using System;
using System.Globalization;
using System.Windows.Data;

namespace ERP.WPF;

/// <summary>Converte uma contagem simples numa largura de barra em pixels —
/// usado no gráfico de "Pedidos por status" do Dashboard de Marketplace.
/// Escala fixa (não relativa ao maior valor da lista, pra manter simples);
/// satura num teto razoável pra não estourar o card visualmente.</summary>
public class IntToBarWidthConverter : IValueConverter
{
    private const double PixelsPorUnidade = 14;
    private const double LarguraMaxima = 260;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not int quantidade) return 4.0;
        double largura = Math.Max(4, quantidade * PixelsPorUnidade);
        return Math.Min(largura, LarguraMaxima);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
