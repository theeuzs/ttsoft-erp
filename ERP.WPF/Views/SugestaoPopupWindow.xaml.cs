using ERP.Application.DTOs;
using ERP.WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace ERP.WPF.Views;

/// <summary>
/// Popup não-modal que aparece sobre o PDV quando um produto com sugestões
/// é adicionado ao carrinho.
///
/// Características:
/// - Posicionado no canto inferior direito da janela pai
/// - Fecha automaticamente após 12s sem interação (timer resetável)
/// - Cada clique em "+ Adicionar" remove o item da lista e reseta o timer
/// - Fecha sozinho quando a lista fica vazia
/// - Nunca bloqueia o PDV (não é ShowDialog)
/// </summary>
public partial class SugestaoPopupWindow : Window
{
    private readonly PdvViewModel    _vm;
    private readonly DispatcherTimer _timer;
    private int                      _segundosRestantes = 12;

    public SugestaoPopupWindow(PdvViewModel vm, IEnumerable<ProdutoAgregadoDto> sugestoes, Window pai)
    {
        InitializeComponent();
        _vm = vm;

        // Bind lista ao ItemsControl do XAML
        ListaSugestoes.ItemsSource = _vm.Sugestoes;

        // Mostra nome do produto principal no subtítulo (pega do primeiro item se disponível)
        TxtProdutoPrincipal.Text = "Estes produtos costumam ser comprados juntos";

        // Posiciona no canto inferior direito da janela pai
        Posicionar(pai);

        // Timer de auto-close
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();

        // Monitora quando a lista esvazia (todos foram adicionados)
        _vm.Sugestoes.CollectionChanged += (_, _) =>
        {
            if (_vm.Sugestoes.Count == 0)
                FecharPopup();
            else
                ResetarTimer(); // cada adição reseta os 12s
        };
    }

    private void Posicionar(Window pai)
    {
        try
        {
            // Canto inferior direito da janela do PDV, com 16px de margem
            Left = pai.Left + pai.ActualWidth  - Width  - 28;
            Top  = pai.Top  + pai.ActualHeight - Height - 60;

            // Fallback: se não couber na tela, centraliza
            if (Left < 0) Left = 16;
            if (Top  < 0) Top  = 16;
        }
        catch
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _segundosRestantes--;
        TxtTimer.Text = $"Fechando em {_segundosRestantes}s...";

        if (_segundosRestantes <= 0)
            FecharPopup();
    }

    private void ResetarTimer()
    {
        _segundosRestantes = 12;
        TxtTimer.Text = $"Fechando em {_segundosRestantes}s...";
    }

    private void BtnAdicionar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn &&
            btn.Tag is ProdutoAgregadoDto sugestao)
        {
            _vm.AdicionarSugestaoCommand.Execute(sugestao);
            // CollectionChanged acima cuida de resetar o timer ou fechar
        }
    }

    private void BtnX_Click(object sender, RoutedEventArgs e)     => FecharPopup();
    private void BtnFechar_Click(object sender, RoutedEventArgs e) => FecharPopup();

    private void FecharPopup()
    {
        _timer.Stop();
        _vm.FecharSugestoesCommand.Execute(null);
        try { Close(); } catch { /* já fechado */ }
    }
}
