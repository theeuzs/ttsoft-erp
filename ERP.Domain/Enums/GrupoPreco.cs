namespace ERP.Domain.Enums;

/// <summary>
/// Grupo de preço do cliente.
/// A = Varejo (preço padrão — SalePrice)
/// B = Revendedor / Empreiteiro (desconto automático)
/// C = Atacadista / Grande conta (maior desconto)
/// </summary>
public enum GrupoPreco
{
    A = 0, // Varejo — preço normal
    B = 1, // Revendedor / Empreiteiro
    C = 2, // Atacadista / Grande conta
    // S{N} FIX: Repasse - custo + IPI + 25% (RepassePolicy), calculado na hora, sem
    // campo no produto. Separado do B porque o B tem revendedores com preco proprio.
    R = 3,
}
