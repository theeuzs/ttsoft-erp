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

    // S17: campos novos — tela de salvar orçamento
    public string? Observacao { get; set; }
    public int      ValidadeDias    { get; set; } = 7;
    public bool     AgendarFollowUp { get; set; }
    public DateTime? DataFollowUp   { get; set; }
}

public class OrcamentoItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
}