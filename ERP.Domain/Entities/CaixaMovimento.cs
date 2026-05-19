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
}
