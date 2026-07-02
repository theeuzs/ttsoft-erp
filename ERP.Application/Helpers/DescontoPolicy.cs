namespace ERP.Application.Helpers;

/// <summary>
/// S13: Política de desconto — centraliza as regras de validação de percentual de desconto.
/// Antes: validação inline no SaleService (if DiscountPercent &lt; 0 / &gt; MaxDiscountPercentage).
/// Padrão herdado do PasswordPolicy (S12).
/// </summary>
public static class DescontoPolicy
{
    /// <summary>
    /// Valida o percentual de desconto de um item contra o limite máximo permitido pelo cargo.
    /// Retorna (true, null) se válido, (false, mensagemErro) se inválido.
    /// </summary>
    /// <param name="discountPercent">Desconto solicitado (0–100).</param>
    /// <param name="maxDiscountPercentage">Limite máximo do cargo do operador.</param>
    /// <param name="productName">Nome do produto (para mensagem de erro).</param>
    public static (bool Ok, string? Erro) Validar(
        decimal discountPercent,
        decimal maxDiscountPercentage,
        string? productName = null)
    {
        if (discountPercent < 0)
            return (false, $"Desconto não pode ser negativo{(productName != null ? $" ({productName})" : "")}.");

        if (discountPercent > 100)
            return (false, $"Desconto não pode ultrapassar 100%{(productName != null ? $" ({productName})" : "")}.");

        if (discountPercent > maxDiscountPercentage)
            return (false,
                $"Desconto de {discountPercent:F2}% excede o limite do seu cargo ({maxDiscountPercentage:F2}%)" +
                $"{(productName != null ? $" no produto '{productName}'" : "")}.");

        return (true, null);
    }

    /// <summary>
    /// Calcula o valor total de um item após desconto.
    /// Total = preço × quantidade × (1 - desconto/100)
    /// </summary>
    public static decimal CalcularTotal(decimal unitPrice, decimal quantity, decimal discountPercent)
        => unitPrice * quantity * (1m - discountPercent / 100m);
}
