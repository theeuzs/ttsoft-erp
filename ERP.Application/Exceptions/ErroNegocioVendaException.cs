namespace ERP.Application.Exceptions;

/// <summary>
/// Venda recusada por regra de negócio (produto inexistente, estoque
/// insuficiente, desconto/limite de crédito acima do permitido, caixa
/// fechado). Distinta de falha inesperada (banco, bug) e de falha de rede:
/// a API devolve 400 para este tipo e 500 para o resto, e o Sync Engine do
/// WPF só põe em quarentena este caso. Herda de InvalidOperationException
/// para não quebrar quem já trata esse tipo (o HttpSaleService sempre lançou
/// InvalidOperationException para HTTP 400). Quando vem do servidor, a causa
/// original fica em InnerException.
/// </summary>
public class ErroNegocioVendaException : InvalidOperationException
{
    public ErroNegocioVendaException(string message) : base(message) { }
    public ErroNegocioVendaException(string message, Exception inner) : base(message, inner) { }
}
