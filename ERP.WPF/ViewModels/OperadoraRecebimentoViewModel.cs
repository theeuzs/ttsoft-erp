// ── ERP.WPF/ViewModels/OperadoraRecebimentoViewModel.cs ───────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Cadastro de Operadoras de Recebimento (Stone, Cielo, Mercado Pago) —
/// preparação de base (Categoria B) pro futuro conceito de Recebíveis de
/// Operadora. Hoje só cadastro; nenhum fluxo real consome isso ainda.
/// Injeção via construtor desde o início (Parte 0 do roadmap).
/// </summary>
public class OperadoraRecebimentoViewModel : BaseViewModel
{
    private readonly IOperadoraRecebimentoService _service;
    private readonly IContaBancariaService        _contaBancariaService;

    public ObservableCollection<OperadoraRecebimentoDto> Operadoras { get; } = new();
    public ObservableCollection<ContaBancariaDto> ContasBancarias { get; } = new();

    private string _novoNome = string.Empty;
    public string NovoNome { get => _novoNome; set => SetProperty(ref _novoNome, value); }

    private int _novoPrazoDebito = 1;
    public int NovoPrazoDebito { get => _novoPrazoDebito; set => SetProperty(ref _novoPrazoDebito, value); }

    private int _novoPrazoCreditoVista = 1;
    public int NovoPrazoCreditoVista { get => _novoPrazoCreditoVista; set => SetProperty(ref _novoPrazoCreditoVista, value); }

    private int _novoPrazoCreditoParcelado = 30;
    public int NovoPrazoCreditoParcelado { get => _novoPrazoCreditoParcelado; set => SetProperty(ref _novoPrazoCreditoParcelado, value); }

    private bool _novaAntecipacaoAutomatica;
    public bool NovaAntecipacaoAutomatica { get => _novaAntecipacaoAutomatica; set => SetProperty(ref _novaAntecipacaoAutomatica, value); }

    private decimal _novaTaxaDebito;
    public decimal NovaTaxaDebito { get => _novaTaxaDebito; set => SetProperty(ref _novaTaxaDebito, value); }

    private decimal _novaTaxaCreditoVista;
    public decimal NovaTaxaCreditoVista { get => _novaTaxaCreditoVista; set => SetProperty(ref _novaTaxaCreditoVista, value); }

    private decimal _novaTaxaCreditoParcelado;
    public decimal NovaTaxaCreditoParcelado { get => _novaTaxaCreditoParcelado; set => SetProperty(ref _novaTaxaCreditoParcelado, value); }

    private ContaBancariaDto? _novaContaDestino;
    public ContaBancariaDto? NovaContaDestino { get => _novaContaDestino; set => SetProperty(ref _novaContaDestino, value); }

    public ICommand CriarCommand       { get; }
    public ICommand InativarCommand    { get; } // param: OperadoraRecebimentoDto
    public ICommand DefinirPadraoCommand { get; } // param: OperadoraRecebimentoDto

    public OperadoraRecebimentoViewModel(IOperadoraRecebimentoService service, IContaBancariaService contaBancariaService)
    {
        _service              = service;
        _contaBancariaService = contaBancariaService;

        CriarCommand       = new RelayCommand(async _ => await CriarAsync(), _ => !string.IsNullOrWhiteSpace(NovoNome));
        InativarCommand    = new RelayCommand(async p => await InativarAsync(p as OperadoraRecebimentoDto));
        DefinirPadraoCommand = new RelayCommand(async p => await DefinirPadraoAsync(p as OperadoraRecebimentoDto));

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        var operadoras = await _service.ObterAtivasAsync();
        Operadoras.Clear();
        foreach (var o in operadoras) Operadoras.Add(o);

        var contas = await _contaBancariaService.ObterContasAtivasAsync();
        ContasBancarias.Clear();
        foreach (var c in contas) ContasBancarias.Add(c);
    }

    private async Task CriarAsync()
    {
        try
        {
            await _service.CriarAsync(new CriarOperadoraRecebimentoDto
            {
                Nome                      = NovoNome,
                PrazoDebitoDias           = NovoPrazoDebito,
                PrazoCreditoVistaDias     = NovoPrazoCreditoVista,
                PrazoCreditoParceladoDias = NovoPrazoCreditoParcelado,
                AntecipacaoAutomatica     = NovaAntecipacaoAutomatica,
                TaxaDebitoPercentual           = NovaTaxaDebito,
                TaxaCreditoVistaPercentual     = NovaTaxaCreditoVista,
                TaxaCreditoParceladoPercentual = NovaTaxaCreditoParcelado,
                ContaDestinoId            = NovaContaDestino?.Id
            });

            NovoNome = string.Empty;
            NovoPrazoDebito = 1;
            NovoPrazoCreditoVista = 1;
            NovoPrazoCreditoParcelado = 30;
            NovaAntecipacaoAutomatica = false;
            NovaTaxaDebito = 0;
            NovaTaxaCreditoVista = 0;
            NovaTaxaCreditoParcelado = 0;
            NovaContaDestino = null;

            await CarregarAsync();
        }
        catch (System.InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Não foi possível cadastrar", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task InativarAsync(OperadoraRecebimentoDto? operadora)
    {
        if (operadora is null) return;

        var res = MessageBox.Show($"Inativar '{operadora.Nome}'?", "Confirmar",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (res != MessageBoxResult.Yes) return;

        await _service.InativarAsync(operadora.Id);
        await CarregarAsync();
    }

    private async Task DefinirPadraoAsync(OperadoraRecebimentoDto? operadora)
    {
        if (operadora is null) return;

        await _service.DefinirComoPadraoAsync(operadora.Id);
        await CarregarAsync();

        MessageBox.Show(
            $"'{operadora.Nome}' agora processa automaticamente os cartões do PDV.",
            "Operadora padrão definida", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}