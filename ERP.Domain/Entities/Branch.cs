using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Filial/loja da empresa. Cada filial tem estoque independente.
/// A matriz é uma filial com IsMatriz = true.
/// </summary>
public class Branch : BaseEntity
{
    public string  Name       { get; set; } = string.Empty;
    public string? CNPJ       { get; set; }
    public string? Endereco   { get; set; }
    public string? Telefone   { get; set; }
    public bool    IsMatriz   { get; set; } = false;
    public bool    IsActive   { get; set; } = true;

    // Navegação
    public ICollection<ProductBranchStock> Stocks { get; set; } = new List<ProductBranchStock>();
}

/// <summary>
/// Estoque de um produto em uma filial específica.
/// Substitui o campo Stock direto do Product para ambientes multi-filial.
/// </summary>
public class ProductBranchStock : BaseEntity
{
    public Guid    ProductId { get; set; }
    public Product Product   { get; set; } = null!;

    public Guid   BranchId { get; set; }
    public Branch Branch   { get; set; } = null!;

    public decimal Quantity  { get; set; } = 0m;
    public decimal MinStock  { get; set; } = 0m;
}
