using ERP.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class NfseView : UserControl
{
    public NfseView()
    {
        InitializeComponent();
        var customerService = App.Services.GetRequiredService<ICustomerService>();
        DataContext = new ViewModels.NfseViewModel(customerService);
    }
}
