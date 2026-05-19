using ERP.Application.Interfaces;
using ERP.WPF.Commands;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

/// <summary>
/// Só consulta saldo e retorna quantos pontos o operador quer resgatar.
/// O débito real acontece em FinalizarVendaViewModel APÓS a venda ser confirmada.
/// </summary>
public class FidelidadeViewModel : BaseViewModel
{
    private readonly Guid               _customerId;
    private readonly IFidelidadeService _service;

    public Action<decimal>? OnConfirmado { get; set; }
    public int PontosParaResgatar { get; private set; } = 0;
    public string NomeCliente { get; }

    private int _saldo;
    public int Saldo
    {
        get => _saldo;
        set { _saldo = value; OnPropertyChanged(); OnPropertyChanged(nameof(ValorEquivalente)); }
    }

    public decimal ValorEquivalente => Saldo * 0.01m;

    private int _pontosInput;
    public int PontosInput
    {
        get => _pontosInput;
        set
        {
            _pontosInput = Math.Max(0, value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(DescontoEquivalente));
            ErroVisivel = false;
        }
    }

    public decimal DescontoEquivalente => PontosInput * 0.01m;

    private bool   _erroVisivel;
    public  bool   ErroVisivel { get => _erroVisivel; set { _erroVisivel = value; OnPropertyChanged(); } }
    private string _erroMsg = "";
    public  string ErroMsg    { get => _erroMsg;    set { _erroMsg = value; OnPropertyChanged(); } }

    // Binding no XAML usa PontosParaResgatar — alias para PontosInput
    public int PontosParaResgatarBinding
    {
        get => PontosInput;
        set => PontosInput = value;
    }

    public ICommand ResgatarCommand { get; }

    public FidelidadeViewModel(Guid customerId, string nomeCliente, IFidelidadeService service)
    {
        _customerId     = customerId;
        NomeCliente     = nomeCliente;
        _service        = service;
        ResgatarCommand = new AsyncRelayCommand(_ => AplicarAsync());
        _ = CarregarSaldoAsync();
    }

    private async Task CarregarSaldoAsync()
    {
        try { Saldo = await _service.GetSaldoAsync(_customerId); }
        catch (Exception ex) { ErroMsg = $"Erro ao carregar saldo: {ex.Message}"; ErroVisivel = true; }
    }

    private Task AplicarAsync()
    {
        ErroVisivel = false;
        if (PontosInput <= 0) { ErroMsg = "Informe a quantidade de pontos."; ErroVisivel = true; return Task.CompletedTask; }
        if (PontosInput > Saldo) { ErroMsg = $"Saldo insuficiente. Disponível: {Saldo} pts."; ErroVisivel = true; return Task.CompletedTask; }

        // Apenas informa o desconto — débito real ocorre após venda confirmada
        PontosParaResgatar = PontosInput;
        OnConfirmado?.Invoke(DescontoEquivalente);
        return Task.CompletedTask;
    }
}
