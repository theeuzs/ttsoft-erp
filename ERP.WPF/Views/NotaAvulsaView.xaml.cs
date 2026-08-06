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
        var customerService = App.Services.GetRequiredService<ICustomerService>();
        DataContext = new ViewModels.NotaAvulsaViewModel(productService, customerService);
    }
}