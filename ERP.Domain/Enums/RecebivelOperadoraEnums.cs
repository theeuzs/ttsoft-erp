// ── ERP.Domain/Enums/RecebivelOperadoraEnums.cs ───────────────────────────────
namespace ERP.Domain.Enums;

public enum StatusRecebivel
{
    Pendente = 1,
    Liquidado = 2,
    Cancelado = 3,
    Divergente = 4
}

/// <summary>
/// Forma de recebimento específica da operadora — mais granular que PaymentMethod
/// porque a taxa e o prazo variam entre débito, crédito à vista e parcelado.
/// S17: PDV ainda não captura parcelamento — CartaoCredito sempre vira
/// CreditoVista aqui até essa captura existir.
/// </summary>
public enum FormaRecebimentoOperadora
{
    Debito = 1,
    CreditoVista = 2,
    CreditoParcelado = 3
}
