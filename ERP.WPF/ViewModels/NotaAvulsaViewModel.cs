// ERP.WPF/ViewModels/NotaAvulsaViewModel.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class ItemNotaAvulsa
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public string Cfop { get; set; } = "5102";
    public decimal Total => Quantidade * ValorUnitario;
}

/// <summary>
/// Item 9 do roadmap fiscal — editor de NF-e desacoplada de venda, com
/// rascunho ("salvar sem emitir") e conferência de impostos.
/// </summary>
public class NotaAvulsaViewModel : BaseViewModel
{
    private readonly IProductService _productService;
    private Guid? _notaId;

    // ── Cabeçalho ────────────────────────────────────────────────────────
    public string NaturezaOperacao { get; set; } = "VENDA DE MERCADORIA";
    public string[] TiposOperacao { get; } = { "S", "E" };
    public string TipoOperacaoEntradaSaida { get; set; } = "S";

    // ── Destinatário ─────────────────────────────────────────────────────
    public string DestinatarioNome { get; set; } = string.Empty;
    public string? DestinatarioDocumento { get; set; }
    public string? DestinatarioLogradouro { get; set; }
    public string? DestinatarioNumero { get; set; }
    public string? DestinatarioBairro { get; set; }
    public string? DestinatarioMunicipio { get; set; }
    public string? DestinatarioUf { get; set; }
    public string? DestinatarioCep { get; set; }
    public string? DestinatarioIe { get; set; }

    public string[] IndicadoresIe { get; } = { "1", "2", "9" };
    public string IndicadorIeDestinatario { get; set; } = "9";

    // ── Item picker (mesmo padrão da tela de Compras) ──────────────────────
    private string _buscaProduto = string.Empty;
    public string BuscaProduto
    {
        get => _buscaProduto;
        set { SetProperty(ref _buscaProduto, value); _ = BuscarProdutosAsync(value); }
    }

    private ProductDto? _produtoSelecionado;
    public ProductDto? ProdutoSelecionado
    {
        get => _produtoSelecionado;
        set { SetProperty(ref _produtoSelecionado, value); if (value != null) ValorUnitarioItem = value.SalePrice; }
    }

    public ObservableCollection<ProductDto> ProdutosSugestao { get; } = new();

    public decimal QuantidadeItem { get; set; } = 1;
    public decimal ValorUnitarioItem { get; set; }
    public string CfopItem { get; set; } = "5102";

    public ObservableCollection<ItemNotaAvulsa> Itens { get; } = new();
    public decimal Total => Itens.Sum(i => i.Total);

    // ── Rascunhos existentes ────────────────────────────────────────────
    public ObservableCollection<NotaFiscalAvulsaResumoDto> Rascunhos { get; } = new();

    private string _statusTexto = string.Empty;
    public string StatusTexto { get => _statusTexto; set => SetProperty(ref _statusTexto, value); }

    public ICommand AdicionarItemCommand { get; }
    public ICommand RemoverItemCommand { get; }
    public ICommand SalvarRascunhoCommand { get; }
    public ICommand ConferirCommand { get; }
    public ICommand EmitirCommand { get; }
    public ICommand NovaNotaCommand { get; }
    public ICommand CarregarRascunhoCommand { get; }
    public ICommand ExcluirRascunhoCommand { get; }
    public ICommand AtualizarRascunhosCommand { get; }

    public NotaAvulsaViewModel(IProductService productService)
    {
        _productService = productService;

        AdicionarItemCommand = new RelayCommand(_ => AdicionarItem(), _ => ProdutoSelecionado != null && QuantidadeItem > 0);
        RemoverItemCommand   = new RelayCommand(item => { if (item is ItemNotaAvulsa i) { Itens.Remove(i); OnPropertyChanged(nameof(Total)); } });
        SalvarRascunhoCommand = new AsyncRelayCommand(async _ => await SalvarRascunhoAsync());
        ConferirCommand        = new AsyncRelayCommand(async _ => await ConferirAsync());
        EmitirCommand           = new AsyncRelayCommand(async _ => await EmitirAsync());
        NovaNotaCommand         = new RelayCommand(_ => LimparFormulario());
        CarregarRascunhoCommand = new AsyncRelayCommand(async item => { if (item is NotaFiscalAvulsaResumoDto r) await CarregarRascunhoAsync(r.Id); });
        ExcluirRascunhoCommand  = new AsyncRelayCommand(async item => { if (item is NotaFiscalAvulsaResumoDto r) await ExcluirRascunhoAsync(r.Id); });
        AtualizarRascunhosCommand = new AsyncRelayCommand(async _ => await CarregarRascunhosAsync());

        _ = CarregarRascunhosAsync();
    }

    private async Task BuscarProdutosAsync(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo) || termo.Length < 2) { ProdutosSugestao.Clear(); return; }
        try
        {
            var resultado = await _productService.SearchAsync(termo);
            ProdutosSugestao.Clear();
            foreach (var p in resultado.Take(8)) ProdutosSugestao.Add(p);
        }
        catch { ProdutosSugestao.Clear(); }
    }

    private void AdicionarItem()
    {
        if (ProdutoSelecionado == null) return;

        Itens.Add(new ItemNotaAvulsa
        {
            ProductId     = ProdutoSelecionado.Id,
            ProductName   = ProdutoSelecionado.Name,
            Quantidade    = QuantidadeItem,
            ValorUnitario = ValorUnitarioItem,
            Cfop          = CfopItem,
        });
        OnPropertyChanged(nameof(Total));

        BuscaProduto = string.Empty;
        ProdutoSelecionado = null;
        QuantidadeItem = 1;
        ValorUnitarioItem = 0;
        CfopItem = "5102";
    }

    private SalvarNotaFiscalAvulsaDto MontarDto() => new()
    {
        Id                        = _notaId,
        NaturezaOperacao          = NaturezaOperacao,
        TipoOperacaoEntradaSaida  = TipoOperacaoEntradaSaida,
        DestinatarioNome          = DestinatarioNome,
        DestinatarioDocumento     = DestinatarioDocumento,
        DestinatarioLogradouro    = DestinatarioLogradouro,
        DestinatarioNumero        = DestinatarioNumero,
        DestinatarioBairro        = DestinatarioBairro,
        DestinatarioMunicipio     = DestinatarioMunicipio,
        DestinatarioUf            = DestinatarioUf,
        DestinatarioCep           = DestinatarioCep,
        DestinatarioIe            = DestinatarioIe,
        IndicadorIeDestinatario   = IndicadorIeDestinatario,
        Itens = Itens.Select(i => new NotaFiscalAvulsaItemDto
        {
            ProductId = i.ProductId, ProductName = i.ProductName,
            Quantidade = i.Quantidade, ValorUnitario = i.ValorUnitario, Cfop = i.Cfop,
        }).ToList(),
    };

    private async Task SalvarRascunhoAsync()
    {
        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            _notaId = await service.SalvarRascunhoAsync(MontarDto());
            MessageBox.Show("✅ Rascunho salvo!", "Nota Avulsa", MessageBoxButton.OK, MessageBoxImage.Information);
            await CarregarRascunhosAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ConferirAsync()
    {
        try
        {
            // Precisa ter salvo antes — a conferência lê do banco.
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            _notaId = await service.SalvarRascunhoAsync(MontarDto());

            var conferencia = await service.ConferirAsync(_notaId.Value);

            var texto = string.Join("\n", conferencia.Itens.Select(i =>
                $"{i.ProductName} — {i.Quantidade}x R$ {i.ValorUnitario:N2} = R$ {i.ValorTotal:N2} | ICMS: R$ {i.Tributos.ValorIcms:N2} | ICMS-ST: R$ {i.Tributos.ValorIcmsSt:N2}"));

            MessageBox.Show(
                $"CONFERÊNCIA DE IMPOSTOS\n\n{texto}\n\n" +
                $"Total produtos: R$ {conferencia.ValorTotalProdutos:N2}\nTotal impostos (ICMS+ST): R$ {conferencia.ValorTotalImpostos:N2}\n\n" +
                "⚠️ Isso não é o DANFE — a SEFAZ só gera o documento oficial depois de autorizar. Confira os valores antes de emitir.",
                "Conferência Fiscal", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao conferir: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task EmitirAsync()
    {
        if (!Itens.Any())
        {
            MessageBox.Show("Adicione pelo menos um item antes de emitir.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirmacao = MessageBox.Show(
            $"Emitir NF-e avulsa pra {DestinatarioNome} — total R$ {Total:N2}?\n\nIsso transmite pra SEFAZ de verdade.",
            "Confirmar Emissão", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmacao != MessageBoxResult.Yes) return;

        try
        {
            StatusTexto = "Salvando e emitindo...";
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            _notaId = await service.SalvarRascunhoAsync(MontarDto());

            var resultado = await service.EmitirAsync(_notaId.Value);

            if (resultado.Sucesso)
            {
                MessageBox.Show($"✅ {resultado.Mensagem}", "Nota Emitida", MessageBoxButton.OK, MessageBoxImage.Information);
                LimparFormulario();
                await CarregarRascunhosAsync();
            }
            else
            {
                MessageBox.Show($"❌ Falha ao emitir: {resultado.Mensagem}", "Erro Fiscal", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao emitir: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { StatusTexto = string.Empty; }
    }

    private async Task CarregarRascunhosAsync()
    {
        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            var lista = await service.ListarAsync();
            Rascunhos.Clear();
            foreach (var r in lista) Rascunhos.Add(r);
        }
        catch { /* best-effort */ }
    }

    private async Task CarregarRascunhoAsync(Guid id)
    {
        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            var nota = await service.ObterAsync(id);
            if (nota == null) return;

            if (nota.Status != "Rascunho")
            {
                MessageBox.Show("Essa nota já foi emitida — não é possível editar, só consultar na tela de Notas Fiscais.",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _notaId = nota.Id;
            NaturezaOperacao = nota.NaturezaOperacao;
            TipoOperacaoEntradaSaida = nota.TipoOperacaoEntradaSaida;
            DestinatarioNome = nota.DestinatarioNome;
            DestinatarioDocumento = nota.DestinatarioDocumento;
            DestinatarioLogradouro = nota.DestinatarioLogradouro;
            DestinatarioNumero = nota.DestinatarioNumero;
            DestinatarioBairro = nota.DestinatarioBairro;
            DestinatarioMunicipio = nota.DestinatarioMunicipio;
            DestinatarioUf = nota.DestinatarioUf;
            DestinatarioCep = nota.DestinatarioCep;
            DestinatarioIe = nota.DestinatarioIe;
            IndicadorIeDestinatario = nota.IndicadorIeDestinatario;

            Itens.Clear();
            foreach (var i in nota.Itens)
                Itens.Add(new ItemNotaAvulsa
                {
                    ProductId = i.ProductId, ProductName = i.ProductName,
                    Quantidade = i.Quantidade, ValorUnitario = i.ValorUnitario, Cfop = i.Cfop,
                });

            OnPropertyChanged(string.Empty); // atualiza todo o formulário de uma vez
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar rascunho: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExcluirRascunhoAsync(Guid id)
    {
        var confirmacao = MessageBox.Show("Excluir esse rascunho? Não dá pra desfazer.", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirmacao != MessageBoxResult.Yes) return;

        try
        {
            var service = App.Services.GetRequiredService<INotaFiscalAvulsaService>();
            await service.ExcluirRascunhoAsync(id);
            await CarregarRascunhosAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao excluir: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LimparFormulario()
    {
        _notaId = null;
        NaturezaOperacao = "VENDA DE MERCADORIA";
        TipoOperacaoEntradaSaida = "S";
        DestinatarioNome = string.Empty;
        DestinatarioDocumento = null;
        DestinatarioLogradouro = null;
        DestinatarioNumero = null;
        DestinatarioBairro = null;
        DestinatarioMunicipio = null;
        DestinatarioUf = null;
        DestinatarioCep = null;
        DestinatarioIe = null;
        Itens.Clear();
        OnPropertyChanged(string.Empty);
    }
}