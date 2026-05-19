using System;
using System.Globalization;
using System.Windows.Data;

namespace ERP.WPF;

/// <summary>
/// Converte um decimal para bool: true se negativo.
/// Usado no FluxoCaixaView para colorir o saldo.
/// Adicione no App.xaml ou no UserControl.Resources:
///   xmlns:local="clr-namespace:ERP.WPF"
///   <local:NegativoConverter x:Key="NegativoConverter"/>
/// </summary>
public class NegativoConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is decimal d && d < 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
