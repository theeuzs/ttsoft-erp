using ERP.Application.Interfaces;
using ERP.Domain.Entities; 
using ERP.Domain.Enums;
using ERP.Domain.Interfaces; 
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Reflection;
using ERP.WPF.Reports;
using QuestPDF.Infrastructure;
using ERP.WPF.Helpers;

namespace ERP.WPF.ViewModels;

public class ResumoCaixaViewModel : BaseViewModel
{
    private readonly ICaixaService _caixaService;
    private readonly IUnitOfWork _uow;

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
        _uow = ERP.WPF.App.Services.GetRequiredService<IUnitOfWork>();
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
            Suprimentos = 0; Sangrias = 0;
            Extrato.Clear();
            NumeroCaixa = "#----";

            Caixa caixaSelecionado = null;

            using (var scope = ERP.WPF.App.Services.CreateScope())
            {
                var uowFresco = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                
                Guid usuarioLogadoId = ERP.WPF.State.AppSession.UserId;

                if (DataConsulta.Date == DateTime.Today)
                {
                    if (usuarioLogadoId != Guid.Empty)
                    {
                        caixaSelecionado = await uowFresco.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioLogadoId);
                    }
                }

                if (caixaSelecionado == null)
                {
                    var todosCaixas = await uowFresco.Caixas.GetAllAsync();
                    
                    var caixasDoDia = todosCaixas.Where(c => c.Movimentos != null && c.Movimentos.Any(m => m.DataHora.Date == DataConsulta.Date)).ToList();
                    
                    caixaSelecionado = caixasDoDia.FirstOrDefault(c => 
                    {
                        var prop = c.GetType().GetProperty("UsuarioId");
                        if (prop != null && usuarioLogadoId != Guid.Empty)
                        {
                            var id = (Guid?)prop.GetValue(c);
                            return id == usuarioLogadoId;
                        }
                        return true; 
                    });

                    if (caixaSelecionado == null) caixaSelecionado = caixasDoDia.FirstOrDefault();
                }

                if (caixaSelecionado == null)
                {
                    Extrato.Add($"Nenhum caixa encontrado em {DataConsulta:dd/MM/yyyy}.");
                    StatusCaixaTexto = "Sem Movimento";
                    VisibilidadeBotoesAcao = Visibility.Collapsed;
                    AtualizarTotaisTela();
                    return;
                }

                NumeroCaixa = $"#{caixaSelecionado.Id.ToString().Substring(0, 4).ToUpper()}";

                if (caixaSelecionado.Status == StatusCaixa.Aberto && DataConsulta.Date == DateTime.Today)
                {
                    StatusCaixaTexto = "Aberto: Hoje";
                    VisibilidadeBotoesAcao = Visibility.Visible;
                }
                else
                {
                    StatusCaixaTexto = $"Fechado em: {DataConsulta:dd/MM/yyyy}";
                    VisibilidadeBotoesAcao = Visibility.Collapsed;
                }

                foreach (var mov in caixaSelecionado.Movimentos.OrderBy(m => m.DataHora))
                {
                    string textoDescricao = "";
                    var tipoMovimento = mov.GetType();
                    var propriedadeTexto = tipoMovimento.GetProperty("Descricao") ?? tipoMovimento.GetProperty("Observacao") ?? tipoMovimento.GetProperty("Motivo") ?? tipoMovimento.GetProperty("Historico");
                    
                    if (propriedadeTexto != null)
                    {
                        textoDescricao = propriedadeTexto.GetValue(mov)?.ToString() ?? "";
                    }

                    bool isEstorno = textoDescricao.ToLower().Contains("estorno");

                    if (mov.Tipo == TipoMovimentoCaixa.Abertura)
                    {
                        SaldoInicial += mov.Valor;
                        Extrato.Add($"ABERTURA \t\t\t + R$ {mov.Valor:N2}");
                    }
                    else if (mov.Tipo == TipoMovimentoCaixa.Venda || mov.Tipo.ToString() == "RecebimentoConta")
                    {
                        if (mov.FormaPagamento == PaymentMethod.Dinheiro) VendasDinheiro += mov.Valor;
                        else if (mov.FormaPagamento == PaymentMethod.Pix) VendasPix += mov.Valor;
                        else if (mov.FormaPagamento == PaymentMethod.CartaoDebito) VendasCartaoDebito += mov.Valor;
                        else if (mov.FormaPagamento == PaymentMethod.CartaoCredito) VendasCartaoCredito += mov.Valor;
                        else if (mov.FormaPagamento == PaymentMethod.Haver) VendasHaver += mov.Valor;
                        else VendasAPrazo += mov.Valor; 
                        
                        string prefixoExtrato = textoDescricao.ToUpper().Contains("FIADO") || mov.Tipo.ToString() == "RecebimentoConta" 
                                            ? "REC. FIADO" 
                                            : "VENDA";

                        Extrato.Add($"{prefixoExtrato} ({mov.FormaPagamento}) \t + R$ {mov.Valor:N2}");
                    }
                    else if (mov.Tipo == TipoMovimentoCaixa.Suprimento)
                    {
                        Suprimentos += mov.Valor;
                        Extrato.Add($"SUPRIMENTO\t\t\t + R$ {mov.Valor:N2}");
                    }
                    else if (mov.Tipo == TipoMovimentoCaixa.Sangria)
                    {
                        if (isEstorno)
                        {
                            if (mov.FormaPagamento == PaymentMethod.Dinheiro) VendasDinheiro -= mov.Valor;
                            else if (mov.FormaPagamento == PaymentMethod.Pix) VendasPix -= mov.Valor;
                            else if (mov.FormaPagamento == PaymentMethod.CartaoDebito) VendasCartaoDebito -= mov.Valor;
                            else if (mov.FormaPagamento == PaymentMethod.CartaoCredito) VendasCartaoCredito -= mov.Valor;
                            else if (mov.FormaPagamento == PaymentMethod.Haver) VendasHaver -= mov.Valor;
                            else VendasAPrazo -= mov.Valor;

                            Extrato.Add($"ESTORNO ({mov.FormaPagamento})\t\t - R$ {mov.Valor:N2}");
                        }
                        else
                        {
                            if (mov.FormaPagamento == PaymentMethod.Dinheiro) Sangrias += mov.Valor;
                            Extrato.Add($"SANGRIA \t\t\t - R$ {mov.Valor:N2}");
                        }
                    }
                    else if (mov.Tipo == TipoMovimentoCaixa.PagamentoDespesa)
                    {
                        // mov.Valor já vem negativo daqui (RegistrarMovimentoAsync recebe
                        // -conta.Valor em ContaPagarViewModel) — usa Math.Abs pra exibir e
                        // somar como valor positivo de saída, igual às outras categorias.
                        Despesas += Math.Abs(mov.Valor);
                        Extrato.Add($"{textoDescricao}\t\t - R$ {Math.Abs(mov.Valor):N2}");
                    }
                    else if (!string.IsNullOrWhiteSpace(textoDescricao))
                    {
                        // Defesa: qualquer TipoMovimentoCaixa futuro que apareça aqui sem
                        // um branch dedicado ainda aparece no extrato, em vez de sumir
                        // silenciosamente como acontecia antes com PagamentoDespesa.
                        Extrato.Add($"{textoDescricao}\t\t {(mov.Valor >= 0 ? "+" : "-")} R$ {Math.Abs(mov.Valor):N2}");
                    }
                }
            } 

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
                
                var caixaAberto = await _uow.Caixas.GetCaixaAbertoByUsuarioAsync(usuarioId);
            
                if (caixaAberto != null)
                {
                    caixaAberto.Status = StatusCaixa.Fechado; 
                    
                    Guid caixaId = ERP.WPF.State.AppSession.CaixaId ?? caixaAberto.Id;
                    await _caixaService.RegistrarMovimentoAsync(usuarioId, 0, "FECHAMENTO DE CAIXA", PaymentMethod.Dinheiro, TipoMovimentoCaixa.Fechamento);

                    _uow.Caixas.Update(caixaAberto);
                    await _uow.CommitAsync();
                }

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