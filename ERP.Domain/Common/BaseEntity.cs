namespace ERP.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    // 👇 NOVO: A semente do Multi-lojas! 
    // Colocando aqui, Produto, Cliente, Venda e Caixa ganham essa coluna automaticamente.
    public Guid TenantId { get; set; } 
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;
}