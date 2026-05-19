using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Tabela de junção explícita para o relacionamento many-to-many
/// auto-referenciante de Produto → ProdutosRelacionados.
///
/// Explícita (não implícita do EF) para permitir o campo Ordem
/// que define a sequência de exibição no popup do PDV.
///
/// Exemplo: Cimento CP-II → [Areia (Ordem=1), Brita (Ordem=2), Desempenadeira (Ordem=3)]
/// </summary>
public class ProdutoAgregado : BaseEntity
{
    /// <summary>Produto "dono" da sugestão (ex: Cimento CP-II).</summary>
    public Guid    ProdutoPrincipalId  { get; set; }
    public Product ProdutoPrincipal    { get; set; } = null!;

    /// <summary>Produto sugerido quando o principal é adicionado ao carrinho.</summary>
    public Guid    ProdutoRelacionadoId { get; set; }
    public Product ProdutoRelacionado  { get; set; } = null!;

    /// <summary>Posição de exibição no popup. Menor = aparece primeiro.</summary>
    public int Ordem { get; set; } = 0;
}
