using System;

namespace ERP.Domain.Services.Fiscal;

public class SimplesNacionalCalculator : IFiscalCalculator
{
    public decimal CalcularTributosAproximados(decimal valorTotal, decimal aliquotaMedia = 13.45m)
    {
        // Padrão para materiais de construção costuma girar em torno dessa alíquota
        return Math.Round(valorTotal * (aliquotaMedia / 100), 2);
    }

    public decimal CalcularIcms(decimal baseCalculo, string csosn, decimal aliquotaIcms)
    {
        // No Simples Nacional, apenas os CSOSN 101 e 201 permitem aproveitamento de crédito de ICMS
        if (csosn == "101" || csosn == "201")
        {
            return Math.Round(baseCalculo * (aliquotaIcms / 100), 2);
        }

        // Para os CSOSNs mais comuns no varejo (102, 103, 300, 400, 500), 
        // a loja não destaca valor de ICMS na nota para o cliente final.
        return 0m;
    }
}