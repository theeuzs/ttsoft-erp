using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class ContaPagarViewModel : BaseViewModel
{
    private readonly IUnitOfWork _uow;

    public ObservableCollection<ContaPagar> Contas { get; } = new();

    // Item 1.2 do roadmap: conta em edição, ou null se o form é "nova despesa".
    private ContaPagar? _contaEditando;
    public bool IsEditando => _contaEditando != null;
    public string TextoBotaoSalvar => IsEditando ? "SALVAR EDIÇÃO" : "LANÇAR DESPESA";

    private string _descricao;
    public string Descricao { get => _descricao; set => SetProperty(ref _descricao, value); }

    private decimal _valor;
    public decimal Valor { get => _valor; set => SetProperty(ref _valor, value); }

    private string _categoria;
    public string Categoria { get => _categoria; set => SetProperty(ref _categoria, value); }

    private DateTime _dataVencimento = DateTime.Now;
    public DateTime DataVencimento { get => _dataVencimento; set => SetProperty(ref _dataVencimento, value); }

    public ICommand AdicionarCommand { get; }
    public ICommand PagarCommand { get; }
    public ICommand ExcluirCommand { get; }
    public ICommand EditarCommand { get; }
    public ICommand CancelarEdicaoCommand { get; }

    public ContaPagarViewModel(IUnitOfWork uow)
    {
        _uow = uow;
        AdicionarCommand = new RelayCommand(async _ => await SalvarAsync());
        PagarCommand = new RelayCommand(async param => await PagarContaAsync(param as ContaPagar));
        ExcluirCommand = new RelayCommand(async param => await ExcluirContaAsync(param as ContaPagar));
        EditarCommand = new RelayCommand(param => CarregarParaEdicao(param as ContaPagar));
        CancelarEdicaoCommand = new RelayCommand(_ => LimparForm());

        _ = CarregarContasAsync();
    }

    public async Task CarregarContasAsync()
    {
        var todas = await _uow.ContasPagar.GetAllAsync();
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Contas.Clear();
            foreach (var c in todas.OrderBy(x => x.Status).ThenBy(x => x.DataVencimento))
            {
                Contas.Add(c);
            }
        });
    }

    private async Task SalvarAsync()
    {
        if (string.IsNullOrWhiteSpace(Descricao) || Valor <= 0)
        {
            MessageBox.Show("Preencha a descrição e o valor corretamente!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_contaEditando != null)
        {
            // Item 1.2 do roadmap: edição. ContaPagar não tem coleção de itens
            // filhos (diferente de PedidoCompra), então Update() aqui é simples e
            // seguro mesmo com o contexto NoTracking global — sem grafo pra
            // confundir o EF, só propriedades escalares.
            _contaEditando.Descricao      = Descricao;
            _contaEditando.Valor          = Valor;
            _contaEditando.Categoria      = string.IsNullOrWhiteSpace(Categoria) ? "Geral" : Categoria;
            _contaEditando.DataVencimento = DataVencimento;

            _uow.ContasPagar.Update(_contaEditando);
            await _uow.CommitAsync();

            LimparForm();
            await CarregarContasAsync();
            MessageBox.Show("Conta atualizada com sucesso!", "Sucesso");
            return;
        }

        var novaConta = new ContaPagar
        {
            Descricao = Descricao,
            Valor = Valor,
            Categoria = string.IsNullOrWhiteSpace(Categoria) ? "Geral" : Categoria,
            DataVencimento = DataVencimento,
            Status = "Pendente"
        };

        await _uow.ContasPagar.AddAsync(novaConta);
        await _uow.CommitAsync();

        LimparForm();
        await CarregarContasAsync();
        MessageBox.Show("Despesa adicionada com sucesso!", "Vila Verde");
    }

    /// <summary>Item 1.2 do roadmap: carrega a conta selecionada no formulário para edição.</summary>
    private void CarregarParaEdicao(ContaPagar? conta)
    {
        if (conta is null) return;

        if (conta.Status != "Pendente")
        {
            MessageBox.Show(
                "Só é possível editar contas ainda Pendentes — essa já foi paga ou cancelada.",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _contaEditando  = conta;
        Descricao       = conta.Descricao;
        Valor           = conta.Valor;
        Categoria       = conta.Categoria;
        DataVencimento  = conta.DataVencimento;

        OnPropertyChanged(nameof(IsEditando));
        OnPropertyChanged(nameof(TextoBotaoSalvar));
    }

    private void LimparForm()
    {
        _contaEditando = null;
        Descricao = string.Empty;
        Valor = 0;
        Categoria = string.Empty;
        DataVencimento = DateTime.Now;

        OnPropertyChanged(nameof(IsEditando));
        OnPropertyChanged(nameof(TextoBotaoSalvar));
    }

    private async Task PagarContaAsync(ContaPagar conta)
    {
        if (conta == null || conta.Status == "Pago") return;
        if (_contaEditando?.Id == conta.Id) LimparForm();

        // Item: escolher a origem do pagamento (Caixa ou Conta Bancária) — antes
        // o pagamento sempre saía do caixa físico, sem opção, mesmo depois da
        // Conta Bancária existir como conceito no sistema (item 1.5).
        var contaBancariaService = ERP.WPF.App.Services.GetRequiredService<IContaBancariaService>();
        var contasAtivas = await contaBancariaService.ObterContasAtivasAsync();

        var dialogVm = new PagarDespesaDialogViewModel(conta.Descricao, conta.Valor, contasAtivas);
        var dialog = new ERP.WPF.Views.PagarDespesaDialogView(dialogVm);

        if (dialog.ShowDialog() != true) return;

        try
        {
            var usuarioId = ERP.WPF.State.AppSession.UserId;

            if (dialog.UsarConta && dialog.ContaBancariaId.HasValue)
            {
                // Paga via Conta Bancária — não mexe no caixa físico.
                await contaBancariaService.RegistrarMovimentoAsync(
                    dialog.ContaBancariaId.Value, conta.Valor,
                    $"PAGTO DESPESA: {conta.Descricao}", TipoMovimentoContaBancaria.Saida,
                    OrigemMovimentoFinanceiro.ContaPagar, conta.Id);
            }
            else
            {
                // Paga via caixa físico — comportamento original.
                var caixaService = ERP.WPF.App.Services.GetRequiredService<ICaixaService>();
                await caixaService.RegistrarMovimentoAsync(usuarioId, -conta.Valor, $"PAGTO DESPESA: {conta.Descricao}", PaymentMethod.Dinheiro, TipoMovimentoCaixa.PagamentoDespesa);
            }

            conta.Status = "Pago";
            conta.DataPagamento = DateTime.Now;

            _uow.ContasPagar.Update(conta);
            await _uow.CommitAsync();
            
            await CarregarContasAsync();
            ERP.WPF.ViewModels.PdvViewModel.NotificacaoCaixaAlterado?.Invoke();

            string origemTexto;
            if (dialog.UsarConta)
            {
                var contaEscolhida = contasAtivas.FirstOrDefault(c => c.Id == dialog.ContaBancariaId);
                origemTexto = $"da conta '{contaEscolhida?.Apelido ?? "bancária"}'";
            }
            else
            {
                origemTexto = "do Caixa";
            }

            MessageBox.Show($"Conta paga! O valor foi descontado {origemTexto}.", "Sucesso");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao pagar conta: {ex.Message}");
        }
    }

    private async Task ExcluirContaAsync(ContaPagar conta)
    {
         if (conta == null) return;
         if (_contaEditando?.Id == conta.Id) LimparForm();
         if (MessageBox.Show("Tem certeza que deseja apagar este registro?", "Aviso", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
         {
             _uow.ContasPagar.Remove(conta);
             await _uow.CommitAsync();
             await CarregarContasAsync();
         }
    }
}