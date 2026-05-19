namespace ERP.Domain.Enums;

public enum StatusOrcamento
{
    Aberto = 0,
    Vendido = 1,
    Cancelado = 2,
    Vencido = 3 // 👈 NOVO: Para orçamentos que passaram dos 7 dias!
}