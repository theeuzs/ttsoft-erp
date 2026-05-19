// ERP.WPF/ViewModels/HaverViewModel.cs
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

public class HaverMovimentoItem
{
    public DateTime Data       { get; set; }
    public string   Tipo       { get; set; } = string.Empty;
    public string   Descricao  { get; set; } = string.Empty;
    public decimal  Valor      { get; set; }
    public decimal  SaldoApos  { get; set; }
    public string   Operador   { get; set; } = string.Empty;

    public string ValorFormatado => Tipo == "Entrada" ? $"+R$ {Valor:N2}" : $"-R$ {Valor:N2}";
    public string CorTipo        => Tipo == "Entrada" ? "#16A34A" : "#DC2626";
    public string DataFormatada  => Data.ToString("dd/MM/yyyy HH:mm");
}

public class HaverViewModel : BaseViewModel
{
    private readonly Guid   _customerId;
    private readonly string _customerName;

    private decimal _saldoAtual;
    public decimal SaldoAtual
    {
        get => _saldoAtual;
        set { SetProperty(ref _saldoAtual, value); OnPropertyChanged(nameof(CorSaldo)); }
    }
    public string CorSaldo => SaldoAtual > 0 ? "#16A34A" : SaldoAtual < 0 ? "#DC2626" : "#64748B";

    public ObservableCollection<HaverMovimentoItem> Movimentos { get; } = new();

    private decimal _valorLancamento;
    public decimal ValorLancamento
    {
        get => _valorLancamento;
        set { SetProperty(ref _valorLancamento, value); AtualizarComandos(); }
    }

    private string _descricaoLancamento = string.Empty;
    public string DescricaoLancamento
    {
        get => _descricaoLancamento;
        set => SetProperty(ref _descricaoLancamento, value);
    }

    public ICommand CarregarCommand  { get; }
    public ICommand AdicionarCommand { get; }
    public ICommand DescontarCommand { get; }

    public HaverViewModel(Guid customerId, string customerName)
    {
        _customerId   = customerId;
        _customerName = customerName;

        CarregarCommand  = new RelayCommand(async _ => await CarregarAsync());
        AdicionarCommand = new RelayCommand(async _ => await LancarAsync("Entrada"),
            _ => ValorLancamento > 0);
        DescontarCommand = new RelayCommand(async _ => await LancarAsync("Saida"),
            _ => ValorLancamento > 0 && ValorLancamento <= SaldoAtual);

        _ = CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        IsBusy = true;
        try
        {
            var service = App.Services.GetRequiredService<IHaverService>();

            SaldoAtual = await service.ObterSaldoAsync(_customerId);

            var historico = await service.ObterHistoricoAsync(_customerId);
            Movimentos.Clear();

            // Calcula saldo acumulado para exibição
            decimal saldoAcumulado = 0;
            var comSaldo = historico
                .Select(m =>
                {
                    saldoAcumulado += m.Tipo == "Entrada" ? m.Valor : -m.Valor;
                    return new HaverMovimentoItem
                    {
                        Data      = m.Data,
                        Tipo      = m.Tipo,
                        Descricao = m.Descricao,
                        Valor     = m.Valor,
                        SaldoApos = saldoAcumulado,
                        Operador  = m.OperadorNome,
                    };
                })
                .OrderByDescending(m => m.Data);

            foreach (var m in comSaldo) Movimentos.Add(m);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar Haver:\n{ex.Message}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private async Task LancarAsync(string tipo)
    {
        // Movimentos manuais de Haver exigem permissão de gerente
        if (!ERP.WPF.State.PermissionChecker.Has(ERP.WPF.State.PermissionChecker.HaverEdit))
        {
            var senha = new Views.SenhaGerenteView();
            senha.ShowDialog();
            if (!senha.Autorizado)
            {
                MessageBox.Show("Operação bloqueada — necessário senha de gerente.", "Permissão negada",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        if (ValorLancamento <= 0) return;

        string descPadrao = tipo == "Entrada" ? "Depósito Haver" : "Retirada Haver";
        string descFinal  = string.IsNullOrWhiteSpace(DescricaoLancamento) ? descPadrao : DescricaoLancamento;

        var confirm = MessageBox.Show(
            $"{(tipo == "Entrada" ? "Adicionar" : "Descontar")} R$ {ValorLancamento:N2} " +
            $"de Haver para {_customerName}?\n\nDescrição: {descFinal}",
            "Confirmar Haver", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var service      = App.Services.GetRequiredService<IHaverService>();
            var operadorNome = ERP.WPF.State.AppSession.UserName ?? "Sistema";

            // ← Toda a lógica (verificação de saldo, SQL parametrizado, log) está no serviço
            await service.LancarAsync(_customerId, ValorLancamento, tipo, descFinal, operadorNome);

            ValorLancamento     = 0;
            DescricaoLancamento = string.Empty;

            await CarregarAsync();

            MessageBox.Show(
                $"✅ Haver {(tipo == "Entrada" ? "adicionado" : "descontado")}!\n" +
                $"Novo saldo: R$ {SaldoAtual:N2}",
                "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro:\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }

    private void AtualizarComandos() => CommandManager.InvalidateRequerySuggested();
}