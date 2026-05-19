using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class MovimentoHaver : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public decimal Valor { get; set; }

    /// <summary>"Entrada" ou "Saida"</summary>
    public string Tipo { get; set; } = "Entrada";

    public string Descricao { get; set; } = string.Empty;

    public DateTime DataMovimento { get; set; } = DateTime.Now;

    /// <summary>Venda relacionada (opcional)</summary>
    public Guid? SaleId { get; set; }

    public string OperadorNome { get; set; } = string.Empty;
}
