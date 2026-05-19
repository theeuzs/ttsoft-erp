using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public enum StatusTransferencia { Rascunho, Enviada, Confirmada, Cancelada }

/// <summary>
/// Transferência de estoque entre filiais.
/// Origem debita, destino credita somente na confirmação.
/// </summary>
public class TransferenciaEstoque : BaseEntity
{
    public Guid   OrigemId   { get; set; }
    public Branch Origem     { get; set; } = null!;

    public Guid   DestinoId  { get; set; }
    public Branch Destino    { get; set; } = null!;

    public DateTime           DataTransferencia { get; set; } = DateTime.Now;
    public StatusTransferencia Status            { get; set; } = StatusTransferencia.Rascunho;
    public string?            Observacao        { get; set; }
    public string             OperadorNome      { get; set; } = string.Empty;

    public ICollection<TransferenciaItem> Itens { get; set; } = new List<TransferenciaItem>();
}

public class TransferenciaItem : BaseEntity
{
    public Guid                TransferenciaId { get; set; }
    public TransferenciaEstoque Transferencia  { get; set; } = null!;

    public Guid    ProductId   { get; set; }
    public Product Product     { get; set; } = null!;

    public decimal Quantidade  { get; set; }
}
