using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace ERP.WPF.Helpers;

public class ThemeConfig
{
    public string NomeSistema  { get; set; } = "ERP Materiais";
    public string CorPrimaria  { get; set; } = "#1E3A5F";
    public string CorAcento    { get; set; } = "#3B82F6";
    public string CorTextoMenu { get; set; } = "#FFFFFF";
    public string CorFundo     { get; set; } = "#F1F5F9";
    public bool   ModoEscuro   { get; set; } = false;
}

public static class ThemeService
{
    private static readonly string CaminhoArquivo =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "theme.json");

    public static ThemeConfig Carregar()
    {
        if (!File.Exists(CaminhoArquivo)) return new ThemeConfig();
        try
        {
            string json = File.ReadAllText(CaminhoArquivo);
            return JsonSerializer.Deserialize<ThemeConfig>(json) ?? new ThemeConfig();
        }
        catch { return new ThemeConfig(); }
    }

    public static void Salvar(ThemeConfig config)
    {
        string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(CaminhoArquivo, json);
    }

    public static void Aplicar(ThemeConfig config)
    {
        var res = System.Windows.Application.Current.Resources;

        // ── Cores do tema (sidebar, botões) ───────────────────────────────
        res["CorPrimaria"]  = Brush(config.CorPrimaria,  "#1E3A5F");
        res["CorAcento"]    = Brush(config.CorAcento,    "#3B82F6");
        res["CorTextoMenu"] = Brush(config.CorTextoMenu, "#FFFFFF");
        res["CorFundo"]     = Brush(config.CorFundo,     "#F1F5F9");
        res["NomeSistema"]  = config.NomeSistema;

        // ── SystemColors do WPF — controlam dropdown do ComboBox ───────────────
        if (config.ModoEscuro)
        {
            res[SystemColors.WindowBrushKey]        = Brush("#1E293B");
            res[SystemColors.WindowTextBrushKey]    = Brush("#F1F5F9");
            res[SystemColors.ControlBrushKey]       = Brush("#1E293B");
            res[SystemColors.ControlTextBrushKey]   = Brush("#F1F5F9");
            res[SystemColors.HighlightBrushKey]     = Brush("#1E3A5F");
            res[SystemColors.HighlightTextBrushKey] = Brush("#FFFFFF");
        }
        else
        {
            if (res.Contains(SystemColors.WindowBrushKey))        res.Remove(SystemColors.WindowBrushKey);
            if (res.Contains(SystemColors.WindowTextBrushKey))    res.Remove(SystemColors.WindowTextBrushKey);
            if (res.Contains(SystemColors.ControlBrushKey))       res.Remove(SystemColors.ControlBrushKey);
            if (res.Contains(SystemColors.ControlTextBrushKey))   res.Remove(SystemColors.ControlTextBrushKey);
            if (res.Contains(SystemColors.HighlightBrushKey))     res.Remove(SystemColors.HighlightBrushKey);
            if (res.Contains(SystemColors.HighlightTextBrushKey)) res.Remove(SystemColors.HighlightTextBrushKey);
            if (res.Contains(SystemColors.InactiveSelectionHighlightBrushKey))     res.Remove(SystemColors.InactiveSelectionHighlightBrushKey);
            if (res.Contains(SystemColors.InactiveSelectionHighlightTextBrushKey)) res.Remove(SystemColors.InactiveSelectionHighlightTextBrushKey);
        }

        // ── Cores que mudam com o modo escuro ─────────────────────────────
        // ⚠️ ATENÇÃO: Se algum XAML específico crashar pedindo "Color", 
        // basta trocar a chamada Brush("#HEX") para Cor("#HEX") na linha correspondente!
        if (config.ModoEscuro)
        {
            // Fundos
            res["FundoGeral"]       = Brush("#0F172A");   
            res["CardBg"]           = Brush("#1E293B");   
            res["CardBgAlt"]        = Brush("#263244");   
            res["FundoInput"]       = Brush("#1E293B");   
            res["FundoInputHover"]  = Brush("#263244");

            // Bordas
            res["BordaCard"]        = Brush("#334155");
            res["RowHoverBg"]       = Brush("#1A2B4A");
            res["RowSelectedBg"] = Cor("#1E3A5F");
            res["BordaInput"]       = Brush("#475569");

            // Textos
            res["TextDark"]         = Brush("#F1F5F9");   
            res["TextMuted"]        = Brush("#94A3B8");   
            res["TextoDark"]        = Brush("#F1F5F9");
            res["TextoMuted"]       = Brush("#94A3B8");
            res["TextoLabel"]       = Brush("#CBD5E1");

            // Rodapé do carrinho
            res["FundoRodape"]      = Brush("#1E293B");
            res["BordaRodape"]      = Brush("#334155");

            // Popup autocomplete
            res["FundoPopup"]       = Brush("#1E293B");
            res["BordaPopup"]       = Brush("#475569");
            res["HoverPopup"]       = Brush("#263244");
        }
        else
        {
            // Modo claro
            res["FundoGeral"]       = Brush("#F0F4F8");
            res["CardBg"]           = Brush("#FFFFFF");
            res["CardBgAlt"]        = Brush("#F8F9FA");
            res["FundoInput"]       = Brush("#FFFFFF");
            res["FundoInputHover"]  = Brush("#F8FAFC");

            res["BordaCard"]        = Brush("#E2E8F0");
            res["RowHoverBg"]       = Brush("#EFF6FF");
            res["RowSelectedBg"] = Cor("#2563EB");
            res["BordaInput"]       = Brush("#CBD5E1");

            res["TextDark"]         = Brush("#2D3436");   
            res["TextMuted"]        = Brush("#636E72");   
            res["TextoDark"]        = Brush("#2D3436");
            res["TextoMuted"]       = Brush("#636E72");
            res["TextoLabel"]       = Brush("#64748B");

            res["FundoRodape"]      = Brush("#F8FAFC");
            res["BordaRodape"]      = Brush("#E2E8F0");

            res["FundoPopup"]       = Brush("#FFFFFF");
            res["BordaPopup"]       = Brush("#CBD5E1");
            res["HoverPopup"]       = Brush("#F8FAFC");
        }
    }

    public static void AplicarFundoJanelas()
    {
        var res = System.Windows.Application.Current.Resources;
        var fundo = res["FundoGeral"] as SolidColorBrush ?? new SolidColorBrush(Colors.White);

        foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
            w.Background = fundo;
    }

    public static void GarantirCardShadow()
    {
        var res = System.Windows.Application.Current.Resources;
        if (!res.Contains("CardShadow"))
        {
            res["CardShadow"] = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius   = 12,
                ShadowDepth  = 2,
                Opacity      = 0.08,
                Color        = System.Windows.Media.Colors.Black,
                Direction    = 270,
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // HELPERS CONVERSORES (O segredo tá aqui no .Freeze())
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converte Hexadecimal para SolidColorBrush e congela para máxima performance no WPF.
    /// Use para propriedades como Background="", Foreground="", BorderBrush=""
    /// </summary>
    private static SolidColorBrush Brush(string hex, string fallback = "#000000")
    {
        try
        {
            Color color = (Color)ColorConverter.ConvertFromString(hex);
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze(); // Impede crash de thread e consome menos RAM
            return brush;
        }
        catch
        {
            Color fallbackColor = (Color)ColorConverter.ConvertFromString(fallback);
            SolidColorBrush fallbackBrush = new SolidColorBrush(fallbackColor);
            fallbackBrush.Freeze();
            return fallbackBrush;
        }
    }

    /// <summary>
    /// Converte Hexadecimal puro para a estrutura Color do WPF.
    /// Use apenas se o XAML estiver exigindo explicitamente uma <SolidColorBrush Color="{DynamicResource ...}" />
    /// </summary>
    private static Color Cor(string hex, string fallback = "#000000")
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return (Color)ColorConverter.ConvertFromString(fallback);
        }
    }
}