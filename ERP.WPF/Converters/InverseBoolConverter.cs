using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ERP.WPF.Converters;

/// <summary>
/// Inverte um booleano. Quando o tipo-alvo for Visibility, retorna
/// Visible para false e Collapsed para true (oposto do BoolToVisibility padrão).
/// Uso: IsEnabled="{Binding IsProtected, Converter={StaticResource InverseBool}}"
///      Visibility="{Binding TemErro, Converter={StaticResource InverseBool}}"
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool bval = value is bool b && b;

        if (targetType == typeof(Visibility))
            return bval ? Visibility.Collapsed : Visibility.Visible;

        return !bval;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v != Visibility.Visible;
        return value is bool b && !b;
    }
}
