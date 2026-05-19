using ERP.Application.DTOs;
using ERP.WPF.ViewModels;
using System.Windows;

namespace ERP.WPF.Views;

public partial class DevolucaoView : Window
{
    public DevolucaoView(DevolucaoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.OnFechar += () => Close();
    }
}
