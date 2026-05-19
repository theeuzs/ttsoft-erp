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

public enum MercadoriaOrigem
{
    Nacional = 0,
    EstrangeiraImportacaoDireta = 1,
    EstrangeiraAdquiridaMercadoInterno = 2
}
