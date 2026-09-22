using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ERP.WPF.Reports;
using QuestPDF.Infrastructure;
using ERP.WPF.Helpers;

namespace ERP.WPF.ViewModels;

public class ResumoCaixaViewModel : BaseViewModel
{
    private readonly ICaixaService _caixaService;

    public Action OnFechar { get; set; }
    public Action OnEncerrarCaixa { get; set; }

    public string OperadorNome { get; set; } = ERP.WPF.State.AppSession.UserName ?? "MATHEUS SILVA";
    
    private string _numeroCaixa = "#...";
    public string NumeroCaixa { get => _numeroCaixa; set => SetProperty(ref _numeroCaixa, value); }

    private DateTime _dataConsulta = DateTime.Today;
    public DateTime DataConsulta 
    { 
        get => _dataConsulta; 
        set 
        { 
            SetProperty(ref _dataConsulta, value); 
            _ = CarregarResumoAsync(); 
        } 
    }

    private string _statusCaixaTexto = "Aberto: Hoje";
    public string StatusCaixaTexto { get => _statusCaixaTexto; set => SetProperty(ref _statusCaixaTexto, value); }

    private Visibility _visibilidadeBotoesAcao = Visibility.Visible;
    public Visibility VisibilidadeBotoesAcao { get => _visibilidadeBotoesAcao; set => SetProperty(ref _visibilidadeBotoesAcao, value); }
    
    private decimal _vendasPix;
    public decimal VendasPix { get => _vendasPix; set { SetProperty(ref _vendasPix, value); OnPropertyChanged(nameof(TotalMovimentado)); } }

    private decimal _vendasCartaoDebito;
    public decimal VendasCartaoDebito { get => _vendasCartaoDebito; set { SetProperty(ref _vendasCartaoDebito, value); OnPropertyChanged(nameof(TotalMovimentado)); } }

    private decimal _vendasCartaoCredito;
    public decimal VendasCartaoCredito { get => _vendasCartaoCredito; set { SetProperty(ref _vendasCartaoCredito, value); OnPropertyChanged(nameof(TotalMovimentado)); } }

    private decimal _vendasAPrazo;
    public decimal VendasAPrazo { get => _vendasAPrazo; set { SetProperty(ref _vendasAPrazo, value); OnPropertyChanged(nameof(TotalMovimentado)); } }

    private decimal _vendasHaver;
    public decimal VendasHaver { get => _vendasHaver; set { SetProperty(ref _vendasHaver, value); OnPropertyChanged(nameof(TotalMovimentado)); } }

    public decimal TotalMovimentado => VendasDinheiro + VendasPix + VendasCartaoDebito + VendasCartaoCredito + VendasAPrazo + VendasHaver;

    private decimal _saldoInicial;
    public decimal SaldoInicial { get => _saldoInicial; set { SetProperty(ref _saldoInicial, value); OnPropertyChanged(nameof(TotalEmEspecie)); } }

    private decimal _vendasDinheiro;
    public decimal VendasDinheiro { get => _vendasDinheiro; set { SetProperty(ref _vendasDinheiro, value); OnPropertyChanged(nameof(TotalMovimentado)); OnPropertyChanged(nameof(TotalEmEspecie)); } }

    private decimal _suprimentos;
    public decimal Suprimentos { get => _suprimentos; set { SetProperty(ref _suprimentos, value); OnPropertyChanged(nameof(TotalEmEspecie)); } }

    private decimal _sangrias;
    public decimal Sangrias { get => _sangrias; set { SetProperty(ref _sangrias, value); OnPropertyChanged(nameof(TotalEmEspecie)); } }

    // S17 FIX: PagamentoDespesa nunca era tratado neste loop — não aparecia no
    // extrato, e "EM ESPÉCIE" nunca descontava o valor, mostrando dinheiro na
    // gaveta maior do que o real sempre que uma despesa era paga do caixa.
    private decimal _despesas;
    public decimal Despesas { get => _despesas; set { SetProperty(ref _despesas, value); OnPropertyChanged(nameof(TotalEmEspecie)); } }

    public decimal TotalEmEspecie => SaldoInicial + VendasDinheiro + Suprimentos - Sangrias - Despesas;

    public ObservableCollection<string> Extrato { get; } = new();

    public ICommand SuprimentoCommand { get; }
    public ICommand SangriaCommand { get; }
    public ICommand EncerrarCaixaCommand { get; }
    public ICommand ExportarPdfCommand { get; }

    public ResumoCaixaViewModel()
    {
        _caixaService = ERP.WPF.App.Services.GetRequiredService<ICaixaService>();
        QuestPDF.Settings.License = LicenseType.Community;

        SuprimentoCommand = new RelayCommand(_ => RealizarSuprimento());
        SangriaCommand = new RelayCommand(_ => RealizarSangria());
        EncerrarCaixaCommand = new AsyncRelayCommand(async _ => await Encerrar());
        ExportarPdfCommand = new RelayCommand(_ => ExportarPdf());

        _ = CarregarResumoAsync();
    }

    private async Task CarregarResumoAsync()
    {
        try
        {
            SaldoInicial = 0; VendasDinheiro = 0; VendasPix = 0; VendasCartaoDebito = 0; 
            VendasCartaoCredito = 0; VendasAPrazo = 0; VendasHaver = 0; 
            Suprimentos = 0; Sangrias = 0; Despesas = 0;
            Extrato.Clear();
            NumeroCaixa = "#----";

            // S{N} FIX — achado auditando pra Fase C (módulo Caixa): esta
            // tela lia IUnitOfWork.Caixas direto e refazia toda a agregação
            // por tipo de movimento aqui mesmo (reflexão pra achar Descricao
            // e UsuarioId inclusos — nenhuma das duas fazia sentido, ver
            // comentário em CaixaService.ObterResumoAsync). Essa lógica já
            // teve pelo menos 2 bugs reais (PagamentoDespesa, Cancelamento-
            // Venda — comentários originais preservados do lado do
            // servidor). Migrado pra usar ObterResumoAsync via API: a
            // agregação agora mora num lugar só (servidor), corrigível uma
            // vez só pra qualquer cliente futuro.
            Guid usuarioLogadoId = ERP.WPF.State.AppSession.UserId;
            var resumo = await _caixaService.ObterResumoAsync(usuarioLogadoId, DataConsulta);

            if (resumo == null)
            {
                Extrato.Add($"Nenhum caixa encontrado em {DataConsulta:dd/MM/yyyy}.");
                StatusCaixaTexto = "Sem Movimento";
                VisibilidadeBotoesAcao = Visibility.Collapsed;
                AtualizarTotaisTela();
                return;
            }

            // Mantido EXATAMENTE como antes: número mostrado na tela vem dos
            // 4 primeiros caracteres do Id (não do NumeroCaixa sequencial da
            // entidade, que existe mas nunca foi usado aqui) — achado durante
            // a migração, não é bug (não perde nem inventa dado), só uma
            // escolha de exibição estranha que preferi não mudar sem pedir.
            NumeroCaixa = $"#{resumo.CaixaId.ToString().Substring(0, 4).ToUpper()}";

            if (resumo.Status == StatusCaixa.Aberto && DataConsulta.Date == DateTime.Today)
            {
                StatusCaixaTexto = "Aberto: Hoje";
                VisibilidadeBotoesAcao = Visibility.Visible;
            }
            else
            {
                StatusCaixaTexto = $"Fechado em: {DataConsulta:dd/MM/yyyy}";
                VisibilidadeBotoesAcao = Visibility.Collapsed;
            }

            SaldoInicial        = resumo.SaldoInicial;
            VendasDinheiro      = resumo.VendasDinheiro;
            VendasPix           = resumo.VendasPix;
            VendasCartaoDebito  = resumo.VendasCartaoDebito;
            VendasCartaoCredito = resumo.VendasCartaoCredito;
            VendasAPrazo        = resumo.VendasAPrazo;
            VendasHaver         = resumo.VendasHaver;
            Suprimentos         = resumo.Suprimentos;
            Sangrias            = resumo.Sangrias;
            Despesas            = resumo.Despesas;

            foreach (var linha in resumo.Extrato)
                Extrato.Add(linha);

            AtualizarTotaisTela();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao ler o caixa: {ex.Message}");
        }
    }

    private void AtualizarTotaisTela()
    {
        OnPropertyChanged(nameof(TotalMovimentado));
        OnPropertyChanged(nameof(TotalEmEspecie));
    }

    private void RealizarSuprimento()
    {
        var vm = new MovimentoCaixaViewModel(false); 
        var view = new Views.MovimentoCaixaView(vm);

        vm.OnConfirmado = async (valor, descricao) =>
        {
            Guid usuarioId = ERP.WPF.State.AppSession.UserId;
            
            await _caixaService.RegistrarMovimentoAsync(usuarioId, valor, "SUPRIMENTO", PaymentMethod.Dinheiro, TipoMovimentoCaixa.Suprimento);
            await CarregarResumoAsync(); 
            PdvViewModel.NotificacaoCaixaAlterado?.Invoke();
        };
        view.ShowDialog();
    }

    private void RealizarSangria()
    {
        var telaSenha = new ERP.WPF.Views.SenhaGerenteView();
        telaSenha.ShowDialog();

        if (!telaSenha.Autorizado) return; 

        var vm = new MovimentoCaixaViewModel(true);
        var view = new Views.MovimentoCaixaView(vm);

        vm.OnConfirmado = async (valor, descricao) =>
        {
            Guid usuarioId = ERP.WPF.State.AppSession.UserId;
            
            await _caixaService.RegistrarMovimentoAsync(usuarioId, valor, "SANGRIA", PaymentMethod.Dinheiro, TipoMovimentoCaixa.Sangria);
            await CarregarResumoAsync(); 
            PdvViewModel.NotificacaoCaixaAlterado?.Invoke();
        };
        view.ShowDialog();
    }

    private async Task Encerrar()
    {
        var confirm = MessageBox.Show("Tem certeza que deseja encerrar o caixa do dia?\nEle não poderá ser reaberto.", "Vila Verde - Fechar Caixa", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (confirm == MessageBoxResult.Yes)
        {
            try
            {
                Guid usuarioId = ERP.WPF.State.AppSession.UserId;

                // S{N} FIX — achado auditando pra Fase C (módulo Caixa): esta
                // tela fechava o caixa NA MÃO (Status = Fechado direto no
                // _uow, sem passar por FecharCaixaAsync), e por isso nunca
                // gravava DataFechamento — todo caixa fechado por aqui ficava
                // com esse campo eternamente nulo. FecharCaixaAsync agora
                // também registra o movimento "FECHAMENTO DE CAIXA" (unificado
                // no serviço, ver CaixaService.cs), então esta chamada única
                // substitui as três linhas antigas (RegistrarMovimentoAsync +
                // _uow.Caixas.Update + _uow.CommitAsync) sem perder nada do
                // extrato.
                await _caixaService.FecharCaixaAsync(usuarioId);

                ERP.WPF.State.AppSession.CaixaId = null;

                OnEncerrarCaixa?.Invoke();
                OnFechar?.Invoke();

                MessageBox.Show("✅ Caixa encerrado com sucesso!", "Fechamento", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao fechar o caixa no banco: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportarPdf()
    {
        var config = ConfiguracaoService.Carregar();
        var doc = new ResumoCaixaPdfReport(
            config,
            numeroCaixa:         NumeroCaixa,
            operador:            OperadorNome,
            data:                DataConsulta,
            status:              StatusCaixaTexto,
            saldoInicial:        SaldoInicial,
            vendasDinheiro:      VendasDinheiro,
            vendasPix:           VendasPix,
            vendasCartaoDebito:  VendasCartaoDebito,
            vendasCartaoCredito: VendasCartaoCredito,
            vendasAPrazo:        VendasAPrazo,
            vendasHaver:         VendasHaver,
            suprimentos:         Suprimentos,
            sangrias:            Sangrias,
            totalMovimentado:    TotalMovimentado,
            totalEmEspecie:      TotalEmEspecie,
            extrato:             Extrato.ToList());

        PdfReportBase.SalvarEAbrir(doc, "ResumoCaixa");
    }
}