using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ERP.WPF.Views;

public partial class NotificacoesView : UserControl
{
    public NotificacoesView()
    {
        Resources.Add("StringToColorConverter", new StringToColorConverter());
        Resources.Add("InverseBoolToVisibilityConverter", new InverseBoolToVisibilityConverter());

        InitializeComponent();
        DataContext = new ViewModels.NotificacoesViewModel();
    }
}

/// <summary>Converte uma string hex (#DC2626) para Color — usado no fundo do ícone.</summary>
public class StringToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex)
                return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch { }
        return Colors.Gray;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

/// <summary>Inverte bool para Visibility — true = Collapsed, false = Visible.</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true
            ? System.Windows.Visibility.Collapsed
            : System.Windows.Visibility.Visible;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}
