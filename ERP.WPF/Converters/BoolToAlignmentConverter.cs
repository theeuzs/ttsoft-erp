using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ERP.WPF.Converters;

public class BoolToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Se for true (sua mensagem), alinha para a direita. Se não, esquerda.
        if (value is bool isMine && isMine)
            return HorizontalAlignment.Right;
            
        return HorizontalAlignment.Left;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}