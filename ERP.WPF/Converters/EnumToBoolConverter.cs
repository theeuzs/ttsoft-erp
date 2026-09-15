using System;
using System.Globalization;
using System.Windows.Data;

namespace ERP.WPF.Converters;

/// <summary>Liga um RadioButton a um valor específico de enum — IsChecked
/// vira true só quando o valor bate com o ConverterParameter (nome do enum
/// como string). Padrão clássico de WPF pra grupo de RadioButton em cima de
/// uma única propriedade enum, em vez de N propriedades bool separadas.</summary>
public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is bool b && b) ? Enum.Parse(targetType, parameter?.ToString() ?? "") : Binding.DoNothing;
}
