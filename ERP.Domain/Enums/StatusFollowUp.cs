namespace ERP.Domain.Enums;

public enum StatusFollowUp
{
    Pendente   = 0,  // Agendado, ainda não contatado
    Contatado  = 1,  // Ligou/visitou mas não fechou
    Convertido = 2,  // Virou venda (espelho de StatusOrcamento.Vendido)
    Perdido    = 3   // Cliente foi para concorrente ou desistiu
}
