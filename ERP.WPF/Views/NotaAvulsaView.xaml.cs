using ERP.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class NotaAvulsaView : UserControl
{
    public NotaAvulsaView()
    {
        InitializeComponent();
        var productService = App.Services.GetRequiredService<IProductService>();
        DataContext = new ViewModels.NotaAvulsaViewModel(productService);
    }
}
