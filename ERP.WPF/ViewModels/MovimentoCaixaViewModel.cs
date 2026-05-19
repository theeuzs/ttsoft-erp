using ERP.WPF.Commands;
using System;
using System.Windows.Input;

namespace ERP.WPF.ViewModels;

public class MovimentoCaixaViewModel : BaseViewModel
{
    public Action OnFechar { get; set; }
    public Action<decimal, string> OnConfirmado { get; set; }

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

        // Só deixa confirmar se o valor for maior que zero e tiver uma descrição
        ConfirmarCommand = new RelayCommand(_ => Confirmar(), _ => Valor > 0 && !string.IsNullOrWhiteSpace(Descricao));
    }

    private void Confirmar()
    {
        OnConfirmado?.Invoke(Valor, Descricao);
        OnFechar?.Invoke();
    }
}