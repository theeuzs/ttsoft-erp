using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Histórico de movimentações de pontos de fidelidade por cliente.
/// Tipo: Credito (acúmulo na venda) | Debito (resgate como desconto).
/// </summary>
public class PontosFidelidade : BaseEntity
{
    public Guid   CustomerId   { get; set; }
    public Customer? Customer  { get; set; }

    public Guid?  SaleId       { get; set; }
    public Sale?  Sale         { get; set; }

    public string Tipo         { get; set; } = "Credito"; // "Credito" | "Debito"
    public int    Pontos       { get; set; }               // sempre positivo
    public string Descricao    { get; set; } = string.Empty;
    public DateTime Data       { get; set; } = DateTime.UtcNow;

    public PontosFidelidade()
    {
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
