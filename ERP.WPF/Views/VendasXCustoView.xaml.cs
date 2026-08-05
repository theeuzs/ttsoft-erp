using ERP.WPF.ViewModels;
using LiveChartsCore.SkiaSharpView.WPF;
using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class VendasXCustoView : UserControl
{
    private CartesianChart? _chart;

    public VendasXCustoView()
    {
        InitializeComponent();

        _chart = new CartesianChart { Margin = new System.Windows.Thickness(8) };
        GraficoContainer.Content = _chart;

        DataContext = new VendasXCustoViewModel();

        if (DataContext is VendasXCustoViewModel vm)
        {
            _chart.SetBinding(CartesianChart.SeriesProperty,
                new System.Windows.Data.Binding(nameof(vm.GraficoSeries)));
            _chart.SetBinding(CartesianChart.XAxesProperty,
                new System.Windows.Data.Binding(nameof(vm.GraficoEixoX)));
            _chart.SetBinding(CartesianChart.YAxesProperty,
                new System.Windows.Data.Binding(nameof(vm.GraficoEixoY)));
        }
    }
}
