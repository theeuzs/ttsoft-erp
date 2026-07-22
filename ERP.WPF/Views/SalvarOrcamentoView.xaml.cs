using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ERP.WPF.Views;

public enum FormatoImpressaoOrcamento { Recibo, PdfPaisagem }

public partial class SalvarOrcamentoView : Window
{
    private readonly ICustomerService _customerService;
    private readonly IEnumerable<ViewModels.CartItem> _itensCarrinho;

    public string?  ObservacaoEscolhida       { get; private set; }
    public int       ValidadeDiasEscolhida    { get; private set; } = 3;
    public bool      AgendarFollowUpEscolhido { get; private set; }
    public DateTime? DataFollowUpEscolhida    { get; private set; }
    public FormatoImpressaoOrcamento FormatoEscolhido { get; private set; } = FormatoImpressaoOrcamento.Recibo;
    public bool      Confirmado               { get; private set; } = false;

    public Guid?   ClienteIdEscolhido   { get; private set; }
    public string? ClienteNomeEscolhido { get; private set; }

    public SalvarOrcamentoView(
        Guid? clienteIdAtual, string nomeClienteAtual,
        IEnumerable<ViewModels.CartItem> itensCarrinho,
        int quantidadeItens, decimal valorTotal, decimal totalTributos,
        ICustomerService customerService)
    {
        InitializeComponent();

        _customerService = customerService;
        _itensCarrinho   = itensCarrinho;

        TxtQtdItens.Text   = quantidadeItens.ToString();
        TxtTributos.Text   = totalTributos.ToString("C2");
        TxtValorTotal.Text = valorTotal.ToString("C2");
        GridItens.ItemsSource = itensCarrinho;

        if (clienteIdAtual.HasValue)
        {
            ClienteIdEscolhido   = clienteIdAtual;
            ClienteNomeEscolhido = nomeClienteAtual;
            MostrarClienteSelecionado(nomeClienteAtual, "");
            _ = CarregarDocumentoClienteAsync(clienteIdAtual.Value);
        }
        else
        {
            MostrarSemCliente();
        }

        // S17 FIX: setar IsChecked/data padrão DEPOIS do InitializeComponent,
        // nunca inline no XAML — evento dispara antes dos elementos existirem.
        Rb3Dias.IsChecked = true;
        RbRecibo.IsChecked = true;
        DpDataFollowUp.SelectedDate = DateTime.Today.AddDays(3);
    }

    private async Task CarregarDocumentoClienteAsync(Guid clienteId)
    {
        try
        {
            var cliente = await _customerService.GetByIdAsync(clienteId);
            if (cliente != null && !string.IsNullOrWhiteSpace(cliente.Document))
                TxtDocumentoClienteCard.Text = cliente.Document;
        }
        catch { /* card funciona sem documento se a busca falhar */ }
    }

    private void MostrarClienteSelecionado(string nome, string documento)
    {
        TxtNomeClienteCard.Text = nome;
        TxtDocumentoClienteCard.Text = documento;
        PainelClienteSelecionado.Visibility = Visibility.Visible;
        PainelSemCliente.Visibility = Visibility.Collapsed;
        PainelBuscaCliente.Visibility = Visibility.Collapsed;
    }

    private void MostrarSemCliente()
    {
        PainelClienteSelecionado.Visibility = Visibility.Collapsed;
        PainelSemCliente.Visibility = Visibility.Visible;
        PainelBuscaCliente.Visibility = Visibility.Collapsed;
    }

    private void AlterarCliente_Click(object sender, RoutedEventArgs e)
    {
        PainelClienteSelecionado.Visibility = Visibility.Collapsed;
        PainelSemCliente.Visibility = Visibility.Collapsed;
        PainelBuscaCliente.Visibility = Visibility.Visible;
        TxtBuscarCliente.Focus();
    }

    private void CancelarBuscaCliente_Click(object sender, RoutedEventArgs e)
    {
        if (ClienteIdEscolhido.HasValue)
            MostrarClienteSelecionado(ClienteNomeEscolhido ?? "", TxtDocumentoClienteCard.Text);
        else
            MostrarSemCliente();
    }

    private void ChkFollowUp_Changed(object sender, RoutedEventArgs e)
        => PainelDataFollowUp.Visibility = ChkFollowUp.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private int ObterValidadeDiasSelecionada()
        => Rb7Dias.IsChecked == true  ? 7
         : Rb15Dias.IsChecked == true ? 15
         : Rb30Dias.IsChecked == true ? 30
         : 3;

    // ── Busca de cliente ─────────────────────────────────────────────────
    private async void TxtBuscarCliente_TextChanged(object sender, TextChangedEventArgs e)
        => await BuscarClientesAsync();

    private async void BuscarCliente_Click(object sender, RoutedEventArgs e)
        => await BuscarClientesAsync();

    private async Task BuscarClientesAsync()
    {
        var termo = TxtBuscarCliente.Text?.Trim() ?? string.Empty;
        if (termo.Length < 2)
        {
            ListaClientesEncontrados.ItemsSource = null;
            return;
        }

        try
        {
            var resultados = await _customerService.SearchAsync(termo);
            ListaClientesEncontrados.ItemsSource = resultados.Take(10).ToList();
        }
        catch
        {
            ListaClientesEncontrados.ItemsSource = null;
        }
    }

    private void ListaClientesEncontrados_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListaClientesEncontrados.SelectedItem is CustomerDto cliente)
        {
            ClienteIdEscolhido   = cliente.Id;
            ClienteNomeEscolhido = cliente.Name;
            MostrarClienteSelecionado(cliente.Name, cliente.Document ?? "");
            TxtBuscarCliente.Text = string.Empty;
        }
    }

    // ── Visualizar antes de salvar ───────────────────────────────────────
    private void Visualizar_Click(object sender, RoutedEventArgs e)
    {
        if (RbPdfPaisagem.IsChecked == true)
        {
            var itens = _itensCarrinho.Select(i => new Helpers.OrcamentoPrinter.ItemOrcamento(
                i.ProductName, i.Quantity, i.LabelUnidadeVenda ?? "un", i.UnitPrice)).ToList();

            var rascunho = new Helpers.OrcamentoPrinter.OrcamentoParaImprimir(
                Numero: "RASCUNHO (ainda não salvo)",
                DataEmissao: DateTime.Now,
                DataValidade: DateTime.Now.AddDays(ObterValidadeDiasSelecionada()),
                ClienteNome: ClienteNomeEscolhido ?? "Consumidor Final",
                ClienteTelefone: null,
                ClienteEmail: null,
                ClienteEndereco: null,
                VendedorNome: State.AppSession.UserName ?? "",
                Observacoes: TxtObservacao.Text,
                CondicoesPagamento: null,
                Itens: itens);

            Helpers.OrcamentoPrinter.Visualizar(rascunho);
        }
        else
        {
            var pagamentosVazios = new List<(string, decimal)>();
            var obs = $"RASCUNHO — Validade: {ObterValidadeDiasSelecionada()} dias\nNão substitui Nota Fiscal.";
            Helpers.ReciboPrinter.Visualizar(Guid.Empty, _itensCarrinho, _itensCarrinho.Sum(i => i.Total), 0,
                ClienteNomeEscolhido ?? "Consumidor Final", State.AppSession.UserName ?? "PDV",
                pagamentosVazios, 0, obs, "ORÇAMENTO", null, "RASCUNHO");
        }
    }

    private void Salvar_Click(object sender, RoutedEventArgs e)
    {
        if (!ClienteIdEscolhido.HasValue)
        {
            MessageBox.Show("Selecione um cliente antes de salvar o orçamento.", "Cliente obrigatório", MessageBoxButton.OK, MessageBoxImage.Warning);
            AlterarCliente_Click(this, new RoutedEventArgs());
            return;
        }

        ObservacaoEscolhida = string.IsNullOrWhiteSpace(TxtObservacao.Text) ? null : TxtObservacao.Text.Trim();
        ValidadeDiasEscolhida = ObterValidadeDiasSelecionada();

        FormatoEscolhido = RbPdfPaisagem.IsChecked == true
            ? FormatoImpressaoOrcamento.PdfPaisagem
            : FormatoImpressaoOrcamento.Recibo;

        AgendarFollowUpEscolhido = ChkFollowUp.IsChecked == true;
        if (AgendarFollowUpEscolhido)
        {
            if (DpDataFollowUp.SelectedDate is null)
            {
                MessageBox.Show("Escolha uma data pro follow-up, ou desmarque a opção.", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DataFollowUpEscolhida = DpDataFollowUp.SelectedDate.Value;
        }

        Confirmado = true;
        DialogResult = true;
        Close();
    }

    private void Cancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}