using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>Meta de vendas por vendedor ou geral para um período.</summary>
public class MetaVendas : BaseEntity
{
    public string   VendedorNome  { get; set; } = "Geral"; // "Geral" = meta da loja
    public int      Mes           { get; set; }
    public int      Ano           { get; set; }
    public decimal  ValorMeta     { get; set; }
    public string?  Descricao     { get; set; }
}
