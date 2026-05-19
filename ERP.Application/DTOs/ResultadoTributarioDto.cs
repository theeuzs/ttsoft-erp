namespace ERP.Application.DTOs;

public class ResultadoTributarioDto
{
    // Valores Base
    public decimal ValorProduto { get; set; } // Qtd * Valor Unitário
    public decimal ValorFrete { get; set; }
    public decimal ValorDesconto { get; set; }

    // ICMS Normal
    public decimal BaseCalculoIcms { get; set; }
    public decimal AliquotaIcms { get; set; }
    public decimal ValorIcms { get; set; }

    // ICMS Substituição Tributária (O terror do Material de Construção)
    public decimal MargemValorAgregado { get; set; } // MVA / IVA
    public decimal BaseCalculoIcmsSt { get; set; }
    public decimal ValorIcmsSt { get; set; }

    // IPI
    public decimal BaseCalculoIpi { get; set; }
    public decimal AliquotaIpi { get; set; }
    public decimal ValorIpi { get; set; }

    // O valor que o cliente efetivamente paga na ponta
    public decimal ValorTotalItem { get; set; } 
}