using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class Permission : BaseEntity
{
    // Ex: "sale.cancel", "product.edit.price"
    public string Code { get; set; } = string.Empty; 
    
    // Ex: "Permite cancelar vendas finalizadas"
    public string Description { get; set; } = string.Empty; 

    // Relacionamento (Muitas permissões para muitos perfis)
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}