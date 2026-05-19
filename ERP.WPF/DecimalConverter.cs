using System;
using System.Globalization;
using System.Windows.Data;

namespace ERP.WPF;

public class DecimalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return d.ToString("0.####", CultureInfo.InvariantCulture);
        return "0";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string s) return 0m;
        
        s = s.Trim();
        
        // Detecta se é formato brasileiro (1.234,56) ou inglês (1,234.56)
        // Se tem vírgula E ponto, o último separador é o decimal
        if (s.Contains(',') && s.Contains('.'))
        {
            // ex: "1.234,56" → remove ponto, troca vírgula por ponto
            if (s.LastIndexOf(',') > s.LastIndexOf('.'))
                s = s.Replace(".", "").Replace(",", ".");
            else
                s = s.Replace(",", "");
        }
        else if (s.Contains(','))
        {
            // Só vírgula → separador decimal brasileiro: "0,8333" → "0.8333"
            s = s.Replace(",", ".");
        }
        // Só ponto → já está no formato inglês: "0.8333"

        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            return result;

        return 0m;
    }
}