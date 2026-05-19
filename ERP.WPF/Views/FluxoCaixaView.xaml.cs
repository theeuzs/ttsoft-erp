using ERP.WPF.ViewModels;
using LiveChartsCore.SkiaSharpView.WPF;
using System.Windows.Controls;

namespace ERP.WPF.Views;

public partial class FluxoCaixaView : UserControl
{
    private CartesianChart? _chart;

    public FluxoCaixaView()
    {
        InitializeComponent();

        _chart = new CartesianChart { Margin = new System.Windows.Thickness(8) };
        GraficoContainer.Content = _chart;

        DataContext = new FluxoCaixaViewModel();

        // Conecta o gráfico ao ViewModel depois que o DataContext está pronto
        if (DataContext is FluxoCaixaViewModel vm)
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