using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class DevolucaoItemVm : BaseViewModel
{
    public Guid    ProductId          { get; set; }
    public string  ProductName        { get; set; } = string.Empty;
    public decimal QuantidadeVendida  { get; set; }
    public decimal UnitPrice          { get; set; }

    /// <summary>Quantidade já devolvida em devoluções anteriores desta venda.</summary>
    public decimal JaDevolvido        { get; set; }

    /// <summary>Máximo que ainda pode ser devolvido = Vendida - JaDevolvido.</summary>
    public decimal DisponivelParaDevolucao => QuantidadeVendida - JaDevolvido;

    public string  InfoDisponivel =>
        JaDevolvido > 0
            ? $"Vendido: {QuantidadeVendida:N2} | Já devolvido: {JaDevolvido:N2} | Disponível: {DisponivelParaDevolucao:N2}"
            : $"Vendido: {QuantidadeVendida:N2}";

    private decimal _quantidadeDevolver;
    public decimal QuantidadeDevolver
    {
        get => _quantidadeDevolver;
        set
        {
            // Limita ao máximo disponível (não ao original — anti-exploit)
            value = Math.Max(0, Math.Min(value, DisponivelParaDevolucao));
            SetProperty(ref _quantidadeDevolver, value);
            OnPropertyChanged(nameof(ValorDevolver));
            OnPropertyChanged(nameof(Selecionado));
        }
    }

    public bool    Selecionado  => QuantidadeDevolver > 0;
    public decimal ValorDevolver => QuantidadeDevolver * UnitPrice;
}

public class DevolucaoViewModel : BaseViewModel
{
    private readonly SaleDto  _venda;
    private readonly Guid?    _customerId;
    private readonly string   _customerName;

    public string NumeroVenda   => _venda.SaleNumber ?? _venda.Id.ToString()[..8].ToUpper();
    public string NomeCliente   => _customerName;
    public string DataVenda     => _venda.SaleDate.ToString("dd/MM/yyyy HH:mm");

    public ObservableCollection<DevolucaoItemVm> Itens { get; } = new();

    private string _motivo = string.Empty;
    public string Motivo
    {
        get => _motivo;
        set => SetProperty(ref _motivo, value);
    }

    public decimal TotalDevolver => Itens.Sum(i => i.ValorDevolver);

    public bool PodeConfirmar => Itens.Any(i => i.QuantidadeDevolver > 0);

    public ICommand ConfirmarCommand { get; }
    public ICommand CancelarCommand  { get; }

    public event Action<DevolucaoResultDto>? OnDevolucaoConcluida;
    public event Action?                     OnFechar;

    public DevolucaoViewModel(SaleDto venda, SaleDetailDto detalhe, Guid? customerId, string customerName,
        Dictionary<Guid, decimal>? jaDevolvidos = null)
    {
        _venda        = venda;
        _customerId   = customerId;
        _customerName = customerName;

        foreach (var item in detalhe.Items)
        {
            decimal jaDevolvido = jaDevolvidos != null && jaDevolvidos.TryGetValue(item.ProductId, out var jd) ? jd : 0m;
            var vm = new DevolucaoItemVm
            {
                ProductId          = item.ProductId,
                ProductName        = item.ProductName,
                QuantidadeVendida  = item.Quantity,
                UnitPrice          = item.UnitPrice,
                JaDevolvido        = jaDevolvido,
                QuantidadeDevolver = 0,
            };
            vm.PropertyChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(TotalDevolver));
                OnPropertyChanged(nameof(PodeConfirmar));
                CommandManager.InvalidateRequerySuggested();
            };
            Itens.Add(vm);
        }

        ConfirmarCommand = new AsyncRelayCommand(async _ => await ConfirmarAsync(),
            _ => PodeConfirmar);
        CancelarCommand  = new RelayCommand(_ => OnFechar?.Invoke());
    }

    private async Task ConfirmarAsync()
    {
        var itens = Itens.Where(i => i.QuantidadeDevolver > 0).ToList();
        if (!itens.Any()) return;

        // Devolução exige permissão de gerente
        if (!ERP.WPF.State.PermissionChecker.Has(ERP.WPF.State.PermissionChecker.SaleReturn))
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

        string credito = _customerId.HasValue
            ? $"\n\nR$ {TotalDevolver:N2} serão creditados em HAVER para {_customerName}."
            : "\n\nSem cliente vinculado — nenhum crédito será gerado.";

        var confirm = MessageBox.Show(
            $"Confirmar devolução de {itens.Count} item(ns) da venda {NumeroVenda}?{credito}",
            "Confirmar Devolução", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IDevolucaoService>();

            var dto = new CreateDevolucaoDto
            {
                VendaId      = _venda.Id,
                CustomerId   = _customerId,
                OperadorNome = ERP.WPF.State.AppSession.UserName ?? "Sistema",
                Motivo       = Motivo,
                Itens        = itens.Select(i => new DevolucaoItemDto
                {
                    ProductId          = i.ProductId,
                    ProductName        = i.ProductName,
                    QuantidadeVendida  = i.QuantidadeVendida,
                    QuantidadeDevolver = i.QuantidadeDevolver,
                    UnitPrice          = i.UnitPrice,
                }).ToList()
            };

            var resultado = await service.DevolverItensAsync(dto);
            OnDevolucaoConcluida?.Invoke(resultado);
            OnFechar?.Invoke();
        }
        catch (Exception ex)
        {
            // Mostra inner exception para diagnóstico
            var innerMsg = ex.InnerException?.InnerException?.Message
                        ?? ex.InnerException?.Message
                        ?? ex.Message;
            MessageBox.Show($"Erro na devolução:\n{ex.Message}\n\nDetalhe:\n{innerMsg}", "Erro",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { IsBusy = false; }
    }
}