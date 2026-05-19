namespace ERP.Domain.Enums;

public enum StatusCaixa
{
    Aberto = 1,
    Fechado = 2
}

public enum TipoMovimentoCaixa
{
    Abertura = 1,
    Venda = 2,
    Suprimento = 3, 
    Sangria = 4,    
    Fechamento = 5,
    // 👇 ADICIONE ESTES 3 PARA FICAR PERFEITO 👇
    PagamentoDespesa = 6,
    RecebimentoConta = 7,
    CancelamentoVenda = 8
}