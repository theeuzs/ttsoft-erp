using ERP.WPF.ViewModels;
using LiveChartsCore.SkiaSharpView.WPF;
using System.Windows.Controls;
using System.Windows.Data;

namespace ERP.WPF.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();

        var chart = new CartesianChart();
        GraficoDashboard.Content = chart;

        this.Loaded += (s, e) =>
        {
            if (DataContext is DashboardViewModel vm)
            {
                chart.SetBinding(CartesianChart.SeriesProperty,
                    new Binding(nameof(vm.GraficoVendas)));
                chart.SetBinding(CartesianChart.XAxesProperty,
                    new Binding(nameof(vm.GraficoVendasEixoX)));
                chart.SetBinding(CartesianChart.YAxesProperty,
                    new Binding(nameof(vm.GraficoVendasEixoY)));
            }
        };
    }
}