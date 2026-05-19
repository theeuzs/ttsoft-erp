using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ERP.WPF.Converters;

/// <summary>
/// Retorna True quando dois valores são iguais.
/// Usado no destaque amarelo do último item adicionado ao carrinho:
/// DataTrigger compara ProductId com UltimoItemId do ViewModel.
/// </summary>
public class EqualConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2) return false;
        if (values[0] == null || values[1] == null) return false;
        return values[0].Equals(values[1]);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converte percentual da meta (0-100) para uma cor:
/// < 30% → vermelho | 30-70% → laranja | 70-100% → verde | 100%+ → azul (meta batida)
/// </summary>
public class PercentToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not double pct) return new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

        return pct switch
        {
            >= 100 => new SolidColorBrush(Color.FromRgb(0x1E, 0x62, 0xA6)), // azul — bateu a meta!
            >= 70  => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)), // verde — no caminho certo
            >= 30  => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), // laranja — atenção
            _      => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // vermelho — crítico
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Converte percentual da meta em largura pixel para a barra de progresso.
/// MultiBinding: [percentual, largura_total_do_container]
/// </summary>
public class PercentToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2) return 0d;
        if (values[0] is not double pct)    return 0d;
        if (values[1] is not double width)  return 0d;
        return Math.Min(width, width * Math.Min(100, pct) / 100d);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
