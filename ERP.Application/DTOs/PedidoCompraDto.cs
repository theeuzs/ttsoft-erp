using ERP.Domain.Enums;
using System;
using System.Collections.Generic;

namespace ERP.Application.DTOs;

// ── Listagem ──────────────────────────────────────────────────────────────
public class PedidoCompraDto
{
    public Guid Id              { get; set; }
    public string Numero        { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public Guid? SupplierId     { get; set; }
    public DateTime DataPedido  { get; set; }
    public DateTime? DataPrevista { get; set; }
    public DateTime? DataRecebimento { get; set; }
    public StatusPedidoCompra Status { get; set; }
    public string StatusTexto   => Status switch
    {
        StatusPedidoCompra.Rascunho  => "Rascunho",
        StatusPedidoCompra.Enviado   => "Enviado",
        StatusPedidoCompra.Recebido  => "Recebido",
        StatusPedidoCompra.Cancelado => "Cancelado",
        _ => "?"
    };
    public decimal Total        { get; set; }
    public string? Observacoes  { get; set; }
    public List<PedidoCompraItemDto> Itens { get; set; } = new();
}

public class PedidoCompraItemDto
{
    public Guid Id              { get; set; }
    public Guid ProductId       { get; set; }
    public string ProductName   { get; set; } = string.Empty;
    public decimal Quantidade   { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Total        => Quantidade * PrecoUnitario;
}

// ── Criação ───────────────────────────────────────────────────────────────
public class CreatePedidoCompraDto
{
    public Guid? SupplierId     { get; set; }
    public string FornecedorNome { get; set; } = string.Empty;
    public DateTime? DataPrevista { get; set; }
    public string? Observacoes  { get; set; }
    public string? CriadoPor    { get; set; }
    public List<CreatePedidoCompraItemDto> Itens { get; set; } = new();
}

/// <summary>Item 1.1 do roadmap — editar pedido de compra já lançado (só em Rascunho).</summary>
public class AtualizarPedidoCompraDto
{
    public string FornecedorNome { get; set; } = string.Empty;
    public DateTime? DataPrevista { get; set; }
    public string? Observacoes   { get; set; }
    public List<CreatePedidoCompraItemDto> Itens { get; set; } = new();
}

public class CreatePedidoCompraItemDto
{
    public Guid ProductId       { get; set; }
    public string ProductName   { get; set; } = string.Empty;
    public decimal Quantidade   { get; set; }
    public decimal PrecoUnitario { get; set; }
}

/// <summary>Item 1.3 do roadmap — uma linha do histórico de compras de um produto.</summary>
public class HistoricoCompraProdutoDto
{
    public DateTime DataPedido      { get; set; }
    public string   NumeroPedido    { get; set; } = string.Empty;
    public string   FornecedorNome  { get; set; } = string.Empty;
    public decimal  Quantidade      { get; set; }
    public decimal  PrecoUnitario   { get; set; }
    public decimal  Total           => Quantidade * PrecoUnitario;
    public string   Status          { get; set; } = string.Empty;
}