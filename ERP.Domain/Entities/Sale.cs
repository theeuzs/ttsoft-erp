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
    public DateTime SaleDate { get; set; } = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();
    public SaleStatus Status { get; set; } = SaleStatus.SemNota;
    public virtual ICollection<SalePayment> Payments { get; set; } = new List<SalePayment>();
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    // S24 (17/08) — frete pra NF-e A4 (marketplace/entrega): entra no Total
    // de verdade, não é só um detalhe pro momento de emitir. Sem isso, o
    // valor cobrado do cliente (produtos + frete) nunca bate com o que a
    // NF-e precisa declarar em vFrete, e a conferência de pagamento
    // (FaltaPagar) ficaria cega pro frete.
    public decimal ShippingValue { get; set; }
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
        CancelledAt = ERP.Domain.Common.FusoBrasilHelper.AgoraNoBrasil();
    }

    public void RecalculateTotals()
    {
        Subtotal = Items.Sum(i => i.TotalPrice);
        Total = Subtotal - DiscountAmount + ShippingValue;
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
    /// Número do item (nItem) no documento fiscal ORIGINAL autorizado pela SEFAZ —
    /// não é "a posição do item na venda", é uma snapshot fiscal congelada no
    /// momento da autorização. Nunca reconstruir a partir da ordem atual de
    /// SaleItems (Id é Guid aleatório, CreatedAt pode empatar entre itens do
    /// mesmo carrinho — nenhum dos dois é um índice fiscal confiável).
    /// Null = venda sem nota autorizada, ou autorizada antes deste campo
    /// existir (ver estratégia de backfill). Atribuído em
    /// FiscalService.EmitirNotaAsync, só após confirmação de autorização —
    /// nunca em rejeição. Usado por EmitirNotaDevolucaoAsync pra montar
    /// numero_item_dfe_referenciado (regra VC02-14, NT 2025.002-RTC).
    /// </summary>
    public int? NumeroItemFiscal { get; set; }

    /// <summary>
    /// Retorna TotalItem se salvo, senão recalcula via Quantity × UnitPrice (compatibilidade).
    /// </summary>
    public decimal TotalPrice => TotalItem > 0 ? TotalItem : Quantity * UnitPrice * (1 - DiscountPercent / 100);
}