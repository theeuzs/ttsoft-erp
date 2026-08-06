using ERP.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace ERP.WPF.Controls;

public partial class AmbienteFiscalBadge : UserControl
{
    public static readonly DependencyProperty EhProducaoProperty =
        DependencyProperty.Register(nameof(EhProducao), typeof(bool), typeof(AmbienteFiscalBadge), new PropertyMetadata(false));

    public bool EhProducao
    {
        get => (bool)GetValue(EhProducaoProperty);
        set => SetValue(EhProducaoProperty, value);
    }

    public AmbienteFiscalBadge()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            try
            {
                var configProvider = App.Services.GetRequiredService<IFiscalConfigurationProvider>();
                var config = await configProvider.ObterConfiguracaoAsync();
                EhProducao = config.UsarAmbienteProducao;
            }
            catch { /* best-effort — badge sem dado não deve travar a tela */ }
        };
    }
}
