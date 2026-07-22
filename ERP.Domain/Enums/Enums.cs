namespace ERP.Domain.Enums;

public enum SaleStatus
{
    SemNota = 0,
    NotaEmitida = 1,
    Cancelada = 2
}

public enum PaymentMethod
{
    Dinheiro = 0,
    CartaoDebito = 1,
    CartaoCredito = 2,
    Pix = 3,
    APrazo = 4,
    Haver = 5
}

/// <summary>
/// De onde a venda nasceu — decide se exige caixa físico aberto (ver
/// ISalePolicyService) e se gera Conta a Receber de repasse em vez de
/// movimento de Caixa/Conta Bancária direto.
/// Deliberadamente só 2 valores por enquanto — API própria, representante
/// etc. entram quando existirem de verdade (Roadmap, Parte 4).
/// </summary>
public enum SaleOrigin
{
    PDV         = 0, // balcão físico, exige caixa aberto
    Marketplace = 1  // Mercado Livre/Shopee — nunca passa pelo caixa
}

public enum MercadoriaOrigem
{
    Nacional = 0,
    EstrangeiraImportacaoDireta = 1,
    EstrangeiraAdquiridaMercadoInterno = 2
}