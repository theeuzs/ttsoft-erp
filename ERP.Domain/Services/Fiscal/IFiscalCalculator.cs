namespace ERP.Domain.Services.Fiscal;

public interface IFiscalCalculator
{
    // Calcula o valor aproximado de tributos para o cupom (Lei 12.741 / IBPT)
    decimal CalcularTributosAproximados(decimal valorTotal, decimal aliquotaMedia);

    // Calcula o valor do ICMS baseado no CSOSN
    decimal CalcularIcms(decimal baseCalculo, string csosn, decimal aliquotaIcms);
}