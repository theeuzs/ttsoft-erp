using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class ContaReceber : BaseEntity
{
    // Vincula a dívida ao cliente correto
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; }

    // Opcional: Vincula a dívida a uma venda específica do PDV
    public Guid? SaleId { get; set; }

    // Valores da dívida
    public decimal ValorTotal { get; set; }
    public decimal ValorRecebido { get; set; }

    // Datas importantes para cobrança
    public DateTime DataEmissao { get; set; } = DateTime.Now;
    public DateTime DataVencimento { get; set; }
    public DateTime? DataPagamento { get; set; }

    // Status para controle: "Pendente", "Pago", "Cancelado"
    public string Status { get; set; } = "Pendente";

    // Ex: "Venda A Prazo - Cupom #1234"
    public string Descricao { get; set; } = string.Empty;

    // ── Parcelamento ─────────────────────────────────────────────────────────
    /// <summary>Número desta parcela. Ex: 1 de 3.</summary>
    public int NumeroParcela { get; set; } = 1;
    /// <summary>Total de parcelas do parcelamento. 1 = à vista.</summary>
    public int TotalParcelas { get; set; } = 1;
    /// <summary>ID do grupo de parcelas (todas as parcelas da mesma venda compartilham).</summary>
    public Guid? ParcelamentoId { get; set; }
    /// <summary>Forma de recebimento. Ex: "Dinheiro", "Cheque", "Cartão Crédito".</summary>
    public string FormaPagamento { get; set; } = string.Empty;
}