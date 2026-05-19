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

    public ContaPagarViewModel(IUnitOfWork uow)
    {
        _uow = uow;
        AdicionarCommand = new RelayCommand(async _ => await AdicionarContaAsync());
        PagarCommand = new RelayCommand(async param => await PagarContaAsync(param as ContaPagar));
        ExcluirCommand = new RelayCommand(async param => await ExcluirContaAsync(param as ContaPagar));
        
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

    private async Task AdicionarContaAsync()
    {
        if (string.IsNullOrWhiteSpace(Descricao) || Valor <= 0)
        {
            MessageBox.Show("Preencha a descrição e o valor corretamente!", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        Descricao = string.Empty;
        Valor = 0;
        Categoria = string.Empty;
        DataVencimento = DateTime.Now;

        await CarregarContasAsync();
        MessageBox.Show("Despesa adicionada com sucesso!", "Vila Verde");
    }

    private async Task PagarContaAsync(ContaPagar conta)
    {
        if (conta == null || conta.Status == "Pago") return;

        var res = MessageBox.Show($"Confirmar pagamento de R$ {conta.Valor:N2} para '{conta.Descricao}'?\nIsso fará uma SANGRIA no caixa atual.", "Confirmar Pagamento", MessageBoxButton.YesNo, MessageBoxImage.Question);
        
        if (res == MessageBoxResult.Yes)
        {
            try
            {
                conta.Status = "Pago";
                conta.DataPagamento = DateTime.Now;

                // 👇 CORRIGIDO: Retirando o valor da conta direto do Caixa do Usuário 👇
                var caixaService = ERP.WPF.App.Services.GetRequiredService<ICaixaService>();
                Guid caixaId = ERP.WPF.State.AppSession.CaixaId ?? Guid.Empty;
                
                await caixaService.RegistrarMovimentoAsync(caixaId, -conta.Valor, $"PAGTO DESPESA: {conta.Descricao}", PaymentMethod.Dinheiro, TipoMovimentoCaixa.PagamentoDespesa);

                _uow.ContasPagar.Update(conta);
                await _uow.CommitAsync();
                
                await CarregarContasAsync();
                ERP.WPF.ViewModels.PdvViewModel.NotificacaoCaixaAlterado?.Invoke();
                MessageBox.Show("Conta paga! O valor foi descontado do Caixa.", "Sucesso");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao pagar conta: {ex.Message}");
            }
        }
    }

    private async Task ExcluirContaAsync(ContaPagar conta)
    {
         if (conta == null) return;
         if (MessageBox.Show("Tem certeza que deseja apagar este registro?", "Aviso", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
         {
             _uow.ContasPagar.Remove(conta);
             await _uow.CommitAsync();
             await CarregarContasAsync();
         }
    }
}