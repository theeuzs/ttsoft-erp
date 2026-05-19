using ERP.Domain.Common;
using System.Collections.Generic;

namespace ERP.Domain.Entities;

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    
    // Relacionamento: Um fornecedor pode fornecer vários produtos
    public ICollection<Product> Products { get; set; } = new List<Product>();
}