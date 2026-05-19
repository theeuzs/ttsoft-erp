using System;

namespace ERP.Application.DTOs;

public class SalesReportItemDto
{
    public DateTime DataVenda { get; set; }
    public string NumeroRecibo { get; set; } = string.Empty;
    public string ClienteNome { get; set; } = string.Empty;
    public string VendedorNome { get; set; } = string.Empty;
    public string FormaPagamento { get; set; } = string.Empty;
    public decimal ValorTotal { get; set; }
}