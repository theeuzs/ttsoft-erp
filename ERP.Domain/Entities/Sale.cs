using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

public class Sale : BaseEntity
{
    public string SaleNumber { get; set; } = string.Empty;
    public SaleOrigin Origem { get; set; } = SaleOrigin.PDV;
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string? SellerId { get; set; }
    public string? SellerName { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.Now;
    public SaleStatus Status { get; set; } = SaleStatus.SemNota;
    public virtual ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public string? CancelReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    // NFC-e (futuro)
    public string? NfceChave { get; set; }
    public string? NfceNumero { get; set; }
    public DateTime? NfceEmitidaEm { get; set; }
    public string? NfceUrlDanfe { get; set; } 
    public string? NfceAmbiente { get; set; } // "Homologação" ou "Produção"
    public string? NfceStatusFocus { get; set; } // "Autorizada", "Cancelada", "Rejeitada"
    public string? NfceReferencia { get; set; } // O ID da venda que mandamos pra Focus

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();

    public void Cancel(string reason)
    {
        Status = SaleStatus.Cancelada;
        CancelReason = reason;
        CancelledAt = DateTime.Now;
    }

    public void RecalculateTotals()
    {
        Subtotal = Items.Sum(i => i.TotalPrice);
        Total = Subtotal - DiscountAmount;
    }
}

public class SaleItem : BaseEntity
{
    public Guid SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string ProductName { get; set; } = string.Empty; // snapshot
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    /// <summary>
    /// Total exato conforme calculado no carrinho (inclui lógica de atacado por pacote).
    /// Evita diferença de centavos causada por arredondamento do UnitPrice.
    /// </summary>
    public decimal TotalItem { get; set; }

    /// <summary>
    /// Retorna TotalItem se salvo, senão recalcula via Quantity × UnitPrice (compatibilidade).
    /// </summary>
    public decimal TotalPrice => TotalItem > 0 ? TotalItem : Quantity * UnitPrice * (1 - DiscountPercent / 100);
}