using ERP.WPF.State;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ERP.WPF.Converters;

/// <summary>
/// Controla visibilidade de elementos de UI por código de permissão.
/// ConverterParameter deve ser um código de permissão: "sale.cancel", "financeiro.view", etc.
/// Sem parâmetro: elemento visível para qualquer usuário autenticado com ao menos uma permissão.
/// </summary>
public class RoleToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is string code && !string.IsNullOrWhiteSpace(code))
            return PermissionChecker.Has(code) ? Visibility.Visible : Visibility.Collapsed;

        // Fallback sem parâmetro: visível se o usuário tem ao menos uma permissão
        var codes = AppSession.PermissionCodes;
        return codes != null && codes.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
