using System;
using System.Collections.Generic;

namespace ERP.Application.DTOs;

public class CreateOrcamentoDto
{
    public Guid? CustomerId { get; set; }
    public Guid UsuarioId { get; set; }
    public string? CustomerName { get; set; }
    public string? SellerName { get; set; }
    public decimal ValorTotal { get; set; }
    public List<OrcamentoItemDto> Itens { get; set; } = new();
}

public class OrcamentoItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
}