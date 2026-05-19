using ERP.Domain.Entities;
using ERP.WPF.Commands;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class ReciboItemPreview
{
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
}

public class ReciboPreviewViewModel : BaseViewModel
{
    public string TituloDocumento { get; set; }
    public DateTime Data { get; set; }
    public string NumeroDocumento { get; set; }
    public string NomeCliente { get; set; }
    public decimal TotalDocumento { get; set; }
    
    public ObservableCollection<ReciboItemPreview> Itens { get; set; } = new();

    public ICommand ImprimirCommand { get; }

    // ─── CONSTRUTOR 1: VENDA ───
    public ReciboPreviewViewModel(Sale sale)
    {
        TituloDocumento = "CUPOM:";
        Data = sale.SaleDate.ToLocalTime();
        NumeroDocumento = sale.SaleNumber;
        
        // 👇 CORREÇÃO 1: Puxando o nome corretamente da sua classe Sale
        NomeCliente = sale.Customer?.Name ?? "Consumidor Final"; 
        TotalDocumento = sale.Total;

        if (sale.Items != null)
        {
            foreach (var item in sale.Items)
                Itens.Add(new ReciboItemPreview { 
                    Quantidade = item.Quantity, 
                    PrecoUnitario = item.UnitPrice, 
                    Descricao = item.ProductName, 
                    ValorTotal = item.TotalPrice // Na SaleItem o TotalPrice existe
                });
        }
        
        ImprimirCommand = new RelayCommand(_ => Imprimir("Venda", NumeroDocumento));
    }

    // ─── CONSTRUTOR 2: ORÇAMENTO ───
    public ReciboPreviewViewModel(Orcamento orcamento)
    {
        TituloDocumento = "ORÇAMENTO:";
        Data = orcamento.DataEmissao.ToLocalTime();
        NumeroDocumento = orcamento.Numero;
        NomeCliente = orcamento.CustomerName ?? "Consumidor Final";
        TotalDocumento = orcamento.ValorTotal;

        if (orcamento.Itens != null)
        {
            foreach (var item in orcamento.Itens)
                Itens.Add(new ReciboItemPreview { 
                    Quantidade = item.Quantity, 
                    PrecoUnitario = item.UnitPrice, 
                    Descricao = item.ProductName, 
                    // 👇 CORREÇÃO 2: Calculando o total na mão para o Orçamento
                    ValorTotal = item.Quantity * item.UnitPrice 
                });
        }
        
        ImprimirCommand = new RelayCommand(_ => Imprimir("Orçamento", NumeroDocumento));
    }

    private void Imprimir(string tipo, string numero)
    {
        MessageBox.Show($"Enviando {tipo} {numero} para a impressora térmica...", "Imprimindo", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}