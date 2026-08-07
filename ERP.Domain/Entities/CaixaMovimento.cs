using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

public class CaixaMovimento : BaseEntity
{
    public Guid CaixaId { get; set; }
    public Caixa? Caixa { get; set; }

    public DateTime DataHora { get; set; } = DateTime.Now;
    public TipoMovimentoCaixa Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public PaymentMethod? FormaPagamento { get; set; }

    /// <summary>Achado de auditoria pré-Fase-2 do Offline-First (08/2026) —
    /// antes, o único vínculo com a venda era a Descricao em texto livre
    /// ("VENDA - DINHEIRO"), sem nenhum jeito estruturado de checar
    /// duplicidade. Nulo pra lançamentos que não vêm de venda (Sangria,
    /// Suprimento, ajuste manual, etc.) — comportamento deles não muda.</summary>
    public Guid? VendaId { get; set; }

    /// <summary>Idempotência financeira granular (08/2026) — identifica a linha
    /// de pagamento específica. VendaId continua pra relatório/consulta.</summary>
    public Guid? SalePaymentId { get; set; }
}