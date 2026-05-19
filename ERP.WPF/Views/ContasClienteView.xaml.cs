using System.Windows;
using ERP.WPF.ViewModels;

namespace ERP.WPF.Views;

public partial class ContasClienteView : Window
{
    public ResumoClienteDevedor Resumo { get; }

    public ContasClienteView(FinanceiroViewModel vm, ResumoClienteDevedor resumo)
    {
        InitializeComponent();
        
        // Conecta a janela ao cérebro do Financeiro principal!
        DataContext = vm;
        Resumo = resumo;

        // Preenche as informações na tela
        TxtNomeCliente.Text = $"Contas em Aberto: {Resumo.CustomerName.ToUpper()}";
        GridContas.ItemsSource = Resumo.Contas;
    }
}