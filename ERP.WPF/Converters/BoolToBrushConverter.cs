using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ERP.WPF.Converters;

public class BoolToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Se a mensagem for sua (true), pinta com o Azul padrão da TTSoft
        if (value is bool isMine && isMine)
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E62A6"));
            
        // Se for de outra filial (false), pinta de Cinza escuro
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}