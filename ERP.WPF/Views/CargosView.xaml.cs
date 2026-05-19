using ERP.WPF.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class CargosView : UserControl
{
    public CargosView()
    {
        InitializeComponent();
        // View embeddada — resolve próprio ViewModel do DI (sem scope para não descartar dependências)
        DataContext = App.Services.GetRequiredService<CargosViewModel>();
    }
}
