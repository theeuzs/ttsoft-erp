using ERP.Domain.Common;
using ERP.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ERP.Domain.Entities;

public class PedidoCompra : BaseEntity
{
    public string Numero { get; set; } = string.Empty;          // Ex: PC-2025-001

    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string FornecedorNome { get; set; } = string.Empty;  // Snapshot do nome

    public DateTime DataPedido    { get; set; } = DateTime.UtcNow;
    public DateTime? DataPrevista { get; set; }
    public DateTime? DataRecebimento { get; set; }

    public StatusPedidoCompra Status { get; set; } = StatusPedidoCompra.Rascunho;

    public string? Observacoes   { get; set; }
    public string? CriadoPor     { get; set; }   // Nome do usuário que criou

    public ICollection<PedidoCompraItem> Itens { get; set; } = new List<PedidoCompraItem>();

    // ── Totalizadores calculados ──────────────────────────────────────────
    public decimal Total => Itens.Sum(i => i.Total);

    // ── Ações de negócio ──────────────────────────────────────────────────
    public void Enviar()
    {
        if (Status != StatusPedidoCompra.Rascunho)
            throw new InvalidOperationException("Apenas pedidos em Rascunho podem ser enviados.");
        if (!Itens.Any())
            throw new InvalidOperationException("O pedido não possui itens.");

        Status = StatusPedidoCompra.Enviado;
    }

    public void Cancelar()
    {
        if (Status == StatusPedidoCompra.Recebido)
            throw new InvalidOperationException("Pedidos já recebidos não podem ser cancelados.");

        Status = StatusPedidoCompra.Cancelado;
    }

    /// <summary>
    /// Marca como recebido. Retorna os itens para que o serviço atualize o estoque.
    /// </summary>
    public IEnumerable<PedidoCompraItem> Receber()
    {
        if (Status == StatusPedidoCompra.Recebido)
            throw new InvalidOperationException("Pedido já foi recebido.");
        if (Status == StatusPedidoCompra.Cancelado)
            throw new InvalidOperationException("Pedido cancelado não pode ser recebido.");

        Status = StatusPedidoCompra.Recebido;
        DataRecebimento = DateTime.UtcNow;

        return Itens;
    }
}

public class PedidoCompraItem : BaseEntity
{
    public Guid PedidoCompraId { get; set; }
    public PedidoCompra PedidoCompra { get; set; } = null!;

    public Guid ProductId       { get; set; }
    public Product? Product     { get; set; }
    public string ProductName   { get; set; } = string.Empty;   // Snapshot

    public decimal Quantidade    { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Total => Quantidade * PrecoUnitario;
}
