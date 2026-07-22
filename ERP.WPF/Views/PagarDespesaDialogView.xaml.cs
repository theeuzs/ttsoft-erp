using System;
using System.Windows;

namespace ERP.WPF.Views;

public partial class PagarDespesaDialogView : Window
{
    /// <summary>true = pagar via conta bancária; false = pagar via caixa (padrão original).</summary>
    public bool UsarConta { get; private set; }

    /// <summary>Preenchido só quando UsarConta é true.</summary>
    public Guid? ContaBancariaId { get; private set; }

    public PagarDespesaDialogView(ViewModels.PagarDespesaDialogViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.OnConfirmado = () =>
        {
            UsarConta       = vm.UsarConta;
            ContaBancariaId = vm.ContaSelecionada?.Id;
            DialogResult    = true;
            Close();
        };

        vm.OnCancelado = () =>
        {
            DialogResult = false;
            Close();
        };
    }
}
