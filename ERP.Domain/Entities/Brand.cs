using ERP.Domain.Common;
using System.Collections.Generic;

namespace ERP.Domain.Entities;

public class Brand : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    // Relacionamento: Uma marca pode ter vários produtos
    public ICollection<Product> Products { get; set; } = new List<Product>();
}