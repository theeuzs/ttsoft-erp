using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ERP.WPF.Views;

public partial class InventarioView : UserControl
{
    public InventarioView()
    {
        // Registra os converters antes de InitializeComponent
        Resources.Add("BoolToConfirmText", new BoolToConfirmTextConverter());
        Resources.Add("BoolToGreenGray",   new BoolToGreenGrayConverter());

        InitializeComponent();
        DataContext = new ViewModels.InventarioViewModel();
    }
}

public class BoolToConfirmTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? "✅" : "Conferir";

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}

public class BoolToGreenGrayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true
            ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
            : new SolidColorBrush(Color.FromRgb(100, 116, 139));

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => throw new NotImplementedException();
}
