namespace ERP.Application.Exceptions;

/// <summary>
/// Venda A Prazo excederia o limite de crédito do cliente. Tipo dedicado
/// (não InvalidOperationException genérica) pra quem chama (WPF) conseguir
/// distinguir esse caso especificamente e oferecer a autorização por senha
/// de gerente — sem isso, qualquer InvalidOperationException (ex: "estoque
/// insuficiente") acionaria a mesma tela de senha por engano.
/// </summary>
public class LimiteCreditoExcedidoException : Exception
{
    public decimal LimiteCredito { get; }
    public decimal SaldoDevedorAtual { get; }
    public decimal ValorDaVenda { get; }

    public LimiteCreditoExcedidoException(string customerName, decimal limiteCredito, decimal saldoDevedorAtual, decimal valorDaVenda)
        : base($"Limite de crédito excedido para {customerName}. " +
               $"Limite: {limiteCredito:C} | Dívida atual: {saldoDevedorAtual:C} | Esta venda: {valorDaVenda:C}.")
    {
        LimiteCredito     = limiteCredito;
        SaldoDevedorAtual = saldoDevedorAtual;
        ValorDaVenda      = valorDaVenda;
    }
}
