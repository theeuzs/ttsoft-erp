using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.WPF.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

namespace ERP.WPF.ViewModels;

public class OrcamentosViewModel : BaseViewModel
{
    private readonly IOrcamentoService _orcamentoService;
    // 👇 Injetamos o serviço de clientes para buscar o telefone!
    private readonly ICustomerService _customerService; 

    // ─── Propriedades dos Cartões de Resumo ────────────────────────────
    private decimal _totalOrcamentosAbertos;
    public decimal TotalOrcamentosAbertos { get => _totalOrcamentosAbertos; set { _totalOrcamentosAbertos = value; OnPropertyChanged(nameof(TotalOrcamentosAbertos)); } }

    private int _qtdOrcamentosAbertos;
    public int QtdOrcamentosAbertos { get => _qtdOrcamentosAbertos; set { _qtdOrcamentosAbertos = value; OnPropertyChanged(nameof(QtdOrcamentosAbertos)); } }

    private int _qtdOrcamentosAprovados;
    public int QtdOrcamentosAprovados { get => _qtdOrcamentosAprovados; set { _qtdOrcamentosAprovados = value; OnPropertyChanged(nameof(QtdOrcamentosAprovados)); } }

    // ─── Lista de Orçamentos ───────────────────────────────────────────
    public ObservableCollection<Orcamento> ListaOrcamentos { get; } = new();

    // ─── Filtro de Período (padrão: últimos 7 dias) ────────────────────
    private DateTime _dataInicial = DateTime.Today.AddDays(-7);
    public DateTime DataInicial
    {
        get => _dataInicial;
        set { _dataInicial = value; OnPropertyChanged(nameof(DataInicial)); }
    }

    private DateTime _dataFinal = DateTime.Today;
    public DateTime DataFinal
    {
        get => _dataFinal;
        set { _dataFinal = value; OnPropertyChanged(nameof(DataFinal)); }
    }

    // ─── Comandos ──────────────────────────────────────────────────────
    public ICommand CarregarOrcamentosCommand { get; }
    public ICommand MandarParaCaixaCommand { get; }
    public ICommand ImprimirOrcamentoCommand { get; }
    public ICommand VisualizarOrcamentoCommand { get; }
    public ICommand EnviarWhatsAppOrcamentoCommand { get; }

    // ─── Construtor ────────────────────────────────────────────────────
    public OrcamentosViewModel(IOrcamentoService orcamentoService, ICustomerService customerService)
    {
        _orcamentoService = orcamentoService;
        _customerService = customerService; // Salva o serviço
        
        CarregarOrcamentosCommand = new AsyncRelayCommand(_ => CarregarListaAsync());
        MandarParaCaixaCommand = new RelayCommand(param => CarregarNoPdv(param as Orcamento));
        ImprimirOrcamentoCommand = new RelayCommand(param => Imprimir(param as Orcamento));
        VisualizarOrcamentoCommand = new RelayCommand(param => Visualizar(param as Orcamento));
        
        // 👇 Agora é um Comando Assíncrono para dar tempo de ir no banco buscar o telefone
        EnviarWhatsAppOrcamentoCommand = new AsyncRelayCommand(param => MandarWhatsAppOrcamento(param as Orcamento));
        
        _ = CarregarListaAsync();
    }

    private void CarregarNoPdv(Orcamento? orcamento)
    {
        if (orcamento == null) return;
        PdvViewModel.OrcamentoPendente = orcamento;
        MessageBox.Show($"Orçamento {orcamento.Numero} carregado com sucesso!\n\nVá para a aba 'Caixa (PDV)' no menu lateral para finalizar esta venda.", "Vila Verde - Orçamentos", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task CarregarListaAsync()
{
    IsBusy = true;
    try
    {
        IEnumerable<Orcamento> todos;
        using (var scope = App.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IOrcamentoService>();
            todos = await service.ObterTodosAsync();
        }

        var filtrados = todos
            .Where(o => o.DataEmissao.Date >= DataInicial.Date
                     && o.DataEmissao.Date <= DataFinal.Date)
            .OrderByDescending(o => o.DataEmissao)
            .ToList();

        ListaOrcamentos.Clear();
        foreach (var o in filtrados) ListaOrcamentos.Add(o);

        AtualizarCartoes(filtrados);
    }
    catch (Exception ex) { MessageBox.Show("Erro ao buscar: " + ex.Message); }
    finally { IsBusy = false; }
}

    private void AtualizarCartoes(IEnumerable<Orcamento> orcamentos)
    {
        TotalOrcamentosAbertos = orcamentos.Where(o => o.Status.ToString() == "Aberto").Sum(o => o.ValorTotal);
        QtdOrcamentosAbertos = orcamentos.Count(o => o.Status.ToString() == "Aberto");
        QtdOrcamentosAprovados = orcamentos.Count(o => o.Status.ToString() == "Vendido" && o.DataEmissao.Month == DateTime.Now.Month);
    }

    // 👇 Transformamos em "async Task" para poder usar o banco de dados
    private async Task MandarWhatsAppOrcamento(Orcamento? orcamento)
    {
        if (orcamento == null) return;
        try
        {
            string texto = $"*VILA VERDE MATERIAIS DE CONSTRUÇÃO*\n\n";
            texto += $"Olá, {orcamento.CustomerName ?? "Cliente"}! Aqui está a sua cotação solicitada:\n\n";
            texto += $"*Orçamento:* {orcamento.Numero}\n";
            texto += $"*Validade:* {orcamento.DataEmissao.AddDays(3):dd/MM/yyyy}\n\n";
            texto += $"*ITENS DA COTAÇÃO:*\n";
            
            if (orcamento.Itens != null)
            {
                foreach (var item in orcamento.Itens)
                {
                    texto += $"{(int)item.Quantity}x {item.ProductName} - R$ {item.UnitPrice:N2} (Total: R$ {item.Total:N2})\n";
                }
            }
            
            texto += $"\n---------------------------\n*VALOR TOTAL: R$ {orcamento.ValorTotal:N2}*\n---------------------------\n\n";
            texto += $"Podemos aprovar este orçamento e separar o material para você? 🧱🌱";
            
            string textoCodificado = Uri.EscapeDataString(texto);

            string parametroTelefone = "";

            // 🔍 A MÁGICA ACONTECE AQUI: Vamos no banco buscar o cliente pelo ID para pegar o telefone fresquinho!
            if (orcamento.CustomerId.HasValue)
            {
                var cliente = await _customerService.GetByIdAsync(orcamento.CustomerId.Value);
                if (cliente != null && !string.IsNullOrWhiteSpace(cliente.Phone))
                {
                    string numeroLimpo = new string(cliente.Phone.Where(char.IsDigit).ToArray());
                    if (numeroLimpo.Length >= 10)
                    {
                        if (!numeroLimpo.StartsWith("55")) numeroLimpo = "55" + numeroLimpo;
                        parametroTelefone = $"phone={numeroLimpo}&";
                    }
                }
            }

            // Sprint P: usa WhatsApp Web (abre no navegador padrão — funciona sem app instalado)
            // wa.me com número → abre conversa direta; sem número → abre WhatsApp Web geral
            string url = !string.IsNullOrEmpty(parametroTelefone)
                ? $"https://wa.me/{parametroTelefone.Replace("phone=","").TrimEnd('&')}?text={textoCodificado}"
                : $"https://web.whatsapp.com/send?text={textoCodificado}";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao abrir o WhatsApp: {ex.Message}");
        }
    }

    private void Visualizar(Orcamento? orcamento)
    {
        if (orcamento == null) return;
        try
        {
            var itensParaImprimir = orcamento.Itens.Select(i => new CartItem
            { 
                ProductName = i.ProductName, 
                Quantity = i.Quantity, 
                UnitPrice = i.UnitPrice, 
                NormalUnitPrice = i.UnitPrice, // 👈 CORREÇÃO: Preço base para o Cérebro do Carrinho
                DiscountPercent = 0 
            }).ToList();

            var pagamentosVazios = new List<(string, decimal)>();
            string vendedor = orcamento.SellerName ?? "Não Identificado"; 
            string obs = $"Validade: 3 dias (até {orcamento.DataEmissao.AddDays(3):dd/MM/yyyy})\nNão substitui Nota Fiscal.";

            Helpers.ReciboPrinter.Visualizar(orcamento.Id, itensParaImprimir, orcamento.ValorTotal, 0, orcamento.CustomerName ?? "Consumidor Final", vendedor, pagamentosVazios, 0, obs, "ORÇAMENTO");
        }
        catch (Exception ex) { MessageBox.Show($"Erro ao abrir preview: {ex.Message}", "Erro"); }
    }

    private void Imprimir(Orcamento? orcamento)
    {
        if (orcamento == null) return;
        try
        {
            var itensParaImprimir = orcamento.Itens.Select(i => new CartItem
            { 
                ProductName = i.ProductName, 
                Quantity = i.Quantity, 
                UnitPrice = i.UnitPrice, 
                NormalUnitPrice = i.UnitPrice, // 👈 CORREÇÃO: Preço base para o Cérebro do Carrinho
                DiscountPercent = 0 
            }).ToList();

            var pagamentosVazios = new List<(string, decimal)>();
            string vendedor = orcamento.SellerName ?? "Não Identificado";
            string obs = $"Validade: 3 dias (até {orcamento.DataEmissao.AddDays(3):dd/MM/yyyy})\nNão substitui Nota Fiscal.";

            Helpers.ReciboPrinter.Imprimir(orcamento.Id, itensParaImprimir, orcamento.ValorTotal, 0, orcamento.CustomerName ?? "Consumidor Final", vendedor, pagamentosVazios, 0, obs, "ORÇAMENTO");
        }
        catch (Exception ex) { MessageBox.Show($"Erro ao imprimir: {ex.Message}", "Erro"); }
    }
}