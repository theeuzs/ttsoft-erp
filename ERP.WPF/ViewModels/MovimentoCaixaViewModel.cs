using ERP.WPF.Commands;
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class MovimentoCaixaViewModel : BaseViewModel
{
    public Action OnFechar { get; set; }

    // S{N} FIX — achado testando Fase C: era Action<decimal,string> (void).
    // ResumoCaixaViewModel atribui uma lambda "async (valor, descricao) =>
    // {...}" aqui — com o delegate retornando void, o C# compila isso como
    // "async void": Confirmar() disparava e IMEDIATAMENTE fechava o diálogo
    // (OnFechar), sem esperar RegistrarMovimentoAsync/CarregarResumoAsync/
    // NotificacaoCaixaAlterado terminarem. Qualquer exceção lá dentro
    // desaparecia sem log. Era exatamente por isso que o badge "MEU CAIXA"
    // do PDV não atualizava de forma confiável depois de Sangria/Suprimento
    // — a notificação podia rodar bem depois do diálogo já ter fechado, ou
    // falhar em silêncio. Func<Task> torna isso esperável de verdade.
    public Func<decimal, string, Task> OnConfirmado { get; set; }

    public string TipoMovimento { get; set; } 
    public string CorTema { get; set; } 
    public string Icone { get; set; }

    private decimal _valor;
    public decimal Valor
    {
        get => _valor;
        set => SetProperty(ref _valor, value);
    }

    private string _descricao = string.Empty;
    public string Descricao
    {
        get => _descricao;
        set => SetProperty(ref _descricao, value);
    }

    public ICommand ConfirmarCommand { get; }

    // O truque: passamos 'true' se for Sangria e 'false' se for Suprimento
    public MovimentoCaixaViewModel(bool isSangria)
    {
        TipoMovimento = isSangria ? "Sangria (Retirada)" : "Suprimento (Entrada)";
        CorTema = isSangria ? "#EF4444" : "#10B981"; // Vermelho ou Verde
        Icone = isSangria ? "—" : "+";

        // AsyncRelayCommand: aguarda ConfirmarAsync de verdade antes de
        // liberar o botão de novo, e qualquer exceção vira MessageBox +
        // Log.Error (em vez de sumir como antes) — ver Commands/RelayCommand.cs.
        ConfirmarCommand = new AsyncRelayCommand(
            async _ => await ConfirmarAsync(),
            _ => Valor > 0 && !string.IsNullOrWhiteSpace(Descricao));
    }

    private async Task ConfirmarAsync()
    {
        if (OnConfirmado != null)
            await OnConfirmado(Valor, Descricao);

        // Só fecha DEPOIS que registrar o movimento, atualizar o resumo e
        // avisar o PDV já tiverem terminado de verdade — antes o diálogo
        // fechava antes disso tudo rodar.
        OnFechar?.Invoke();
    }
}