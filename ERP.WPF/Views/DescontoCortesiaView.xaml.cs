using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ERP.WPF.Views;

public partial class DescontoCortesiaView : Window
{
    public bool Confirmou { get; private set; } = false;
    
    // Agora a tela devolve direto o valor em R$ para o caixa
    public decimal ValorDescontoLiberado { get; private set; } = 0; 

    private decimal _totalVenda;
    private bool _isUpdating = false; // Trava para um TextBox não dar loop infinito no outro

    // O construtor agora pede o Total da Venda para fazer a matemática
    public DescontoCortesiaView(decimal totalVenda)
    {
        InitializeComponent();
        _totalVenda = totalVenda;
        TxtDescontoPerc.Focus(); // Começa focando na %
    }

    // Se digitar na Porcentagem, calcula o R$
    private void TxtDescontoPerc_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        if (decimal.TryParse(TxtDescontoPerc.Text, out decimal perc))
        {
            decimal valorEmReais = Math.Round(_totalVenda * (perc / 100m), 2);
            TxtDescontoDinheiro.Text = valorEmReais.ToString("N2");
        }
        else if (string.IsNullOrWhiteSpace(TxtDescontoPerc.Text))
        {
            TxtDescontoDinheiro.Text = string.Empty;
        }

        _isUpdating = false;
    }

    // Se digitar no R$, calcula a Porcentagem
    private void TxtDescontoDinheiro_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        if (decimal.TryParse(TxtDescontoDinheiro.Text, out decimal dinheiro) && _totalVenda > 0)
        {
            decimal valorEmPerc = Math.Round((dinheiro / _totalVenda) * 100m, 2);
            TxtDescontoPerc.Text = valorEmPerc.ToString("N2");
        }
        else if (string.IsNullOrWhiteSpace(TxtDescontoDinheiro.Text))
        {
            TxtDescontoPerc.Text = string.Empty;
        }

        _isUpdating = false;
    }

    private void Inputs_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ValidarEFechar();
        else if (e.Key == Key.Escape)
        {
            Confirmou = false;
            Close();
        }
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        Confirmou = false;
        Close();
    }

    private void BtnLiberar_Click(object sender, RoutedEventArgs e)
    {
        ValidarEFechar();
    }

    private void ValidarEFechar()
    {
        // A TRAVA SUMIU! Agora ele simplesmente pega o valor final em Reais e joga pro caixa!
        if (decimal.TryParse(TxtDescontoDinheiro.Text, out decimal descontoDigitadoEmReais))
        {
            if (descontoDigitadoEmReais > _totalVenda)
            {
                MessageBox.Show("O desconto não pode ser maior que o total da venda, chefe!", "Atenção", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ValorDescontoLiberado = descontoDigitadoEmReais;
            Confirmou = true;
            Close();
        }
        else
        {
            // Se ele apagar tudo e der enter, zera o desconto
            ValorDescontoLiberado = 0;
            Confirmou = true;
            Close();
        }
    }
}