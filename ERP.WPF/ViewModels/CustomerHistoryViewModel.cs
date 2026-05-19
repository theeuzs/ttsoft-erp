using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;

namespace ERP.WPF.ViewModels;

public class CustomerHistoryViewModel : BaseViewModel
{
    private readonly IUnitOfWork _uow;

    public string NomeCliente { get; set; } = string.Empty;

    public ObservableCollection<Sale> ListaVendas { get; } = new();
    public ObservableCollection<Orcamento> ListaOrcamentos { get; } = new();

    public decimal TotalComprado => ListaVendas.Sum(v => v.Total);

    public ICommand VisualizarVendaCommand { get; }
    public ICommand VisualizarOrcamentoCommand { get; }

    public CustomerHistoryViewModel(Guid customerId, string nomeCliente)
    {
        _uow = ERP.WPF.App.Services.GetRequiredService<IUnitOfWork>();
        NomeCliente = nomeCliente;

        VisualizarVendaCommand     = new AsyncRelayCommand(async param => await VisualizarVenda(param as Sale));
        VisualizarOrcamentoCommand = new AsyncRelayCommand(async param => await VisualizarOrcamento(param as Orcamento));

        _ = CarregarHistoricoAsync(customerId);
    }

    private async Task VisualizarVenda(Sale? sale)
    {
        if (sale == null) return;

        var saleCompleta = await _uow.Sales.GetWithItemsAsync(sale.Id) ?? sale;

        // Converte os itens para CartItem (formato que o ReciboPrinter entende) com a vacina do NormalUnitPrice
        var itens = saleCompleta.Items?.Select(i => new ERP.WPF.ViewModels.CartItem
        {
            ProductId       = i.ProductId,
            ProductName     = i.ProductName,
            Quantity        = i.Quantity,
            UnitPrice       = i.UnitPrice,
            NormalUnitPrice = i.UnitPrice // <-- A Mágica que resolve o 0,00!
        }) ?? Enumerable.Empty<ERP.WPF.ViewModels.CartItem>();

        ERP.WPF.Helpers.ReciboPrinter.Visualizar(
            idVenda:               saleCompleta.Id,
            listaItens:            itens,
            valorTotal:            saleCompleta.Total,
            desconto:              saleCompleta.DiscountAmount,
            nomeCliente:           saleCompleta.Customer?.Name ?? "Consumidor Final",
            nomeVendedor:          saleCompleta.SellerName ?? "",
            pagamentos:            saleCompleta.Payments?.Select(p => (p.PaymentMethod.ToString(), p.Amount))
                                   ?? Enumerable.Empty<(string, decimal)>(),
            troco:                 0,
            enderecoOuObservacao:  "",
            dataVenda:             saleCompleta.SaleDate
        );
    }

    private async Task VisualizarOrcamento(Orcamento? orcamento)
    {
        if (orcamento == null) return;

        var orcCompleto = await _uow.Orcamentos.GetByIdAsync(orcamento.Id) ?? orcamento;

        var itens = orcCompleto.Itens?.Select(i => new ERP.WPF.ViewModels.CartItem
        {
            ProductName     = i.ProductName,
            Quantity        = i.Quantity,
            UnitPrice       = i.UnitPrice,
            NormalUnitPrice = i.UnitPrice, // Vacina no Orçamento também
            DiscountPercent = 0
        }) ?? Enumerable.Empty<ERP.WPF.ViewModels.CartItem>();

        string obs = $"Validade: 3 dias (até {orcCompleto.DataEmissao.AddDays(3):dd/MM/yyyy})\nNão substitui Nota Fiscal.";

        ERP.WPF.Helpers.ReciboPrinter.Visualizar(
            idVenda:              orcCompleto.Id,
            listaItens:           itens,
            valorTotal:           orcCompleto.ValorTotal,
            desconto:             0,
            nomeCliente:          orcCompleto.CustomerName ?? "Consumidor Final",
            nomeVendedor:         orcCompleto.SellerName   ?? "Não Identificado",
            pagamentos:           new List<(string, decimal)>(),
            troco:                0,
            enderecoOuObservacao: obs,
            tipoDocumento:        "ORÇAMENTO"
        );
    }

    private async Task CarregarHistoricoAsync(Guid customerId)
    {
        IsBusy = true;
        try
        {
            // ── Orçamentos ────────────────────────────────────────────────
            var todosOrcamentos = await _uow.Orcamentos.GetAllAsync();
            ListaOrcamentos.Clear();
            foreach (var o in todosOrcamentos
                .Where(o => o.CustomerId == customerId)
                .OrderByDescending(o => o.DataEmissao))
                ListaOrcamentos.Add(o);

            // ── Vendas ────────────────────────────────────────────────────
            var todasVendas = await _uow.Sales.GetAllAsync();
            ListaVendas.Clear();
            foreach (var v in todasVendas
                .Where(v => v.CustomerId == customerId)
                .OrderByDescending(v => v.SaleDate))
                ListaVendas.Add(v);

            OnPropertyChanged(nameof(TotalComprado));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar histórico: {ex.Message}");
        }
        finally { IsBusy = false; }
    }
}