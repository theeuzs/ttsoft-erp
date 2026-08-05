using System.Windows;
using System.Windows.Controls;

namespace ERP.WPF.Controls;

public partial class ModuloHeader : UserControl
{
    public static readonly DependencyProperty IconeProperty =
        DependencyProperty.Register(nameof(Icone), typeof(string), typeof(ModuloHeader), new PropertyMetadata(""));

    public static readonly DependencyProperty TituloProperty =
        DependencyProperty.Register(nameof(Titulo), typeof(string), typeof(ModuloHeader), new PropertyMetadata(""));

    /// <summary>Nulo/vazio = badge escondido. Preenchido (ex: "3") = badge visível.</summary>
    public static readonly DependencyProperty BadgeProperty =
        DependencyProperty.Register(nameof(Badge), typeof(string), typeof(ModuloHeader), new PropertyMetadata(null));

    public string Icone  { get => (string)GetValue(IconeProperty);  set => SetValue(IconeProperty, value); }
    public string Titulo { get => (string)GetValue(TituloProperty); set => SetValue(TituloProperty, value); }
    public string? Badge { get => (string?)GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }

    public ModuloHeader()
    {
        InitializeComponent();
    }
}
