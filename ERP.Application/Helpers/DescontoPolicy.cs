namespace ERP.Application.Helpers;

/// <summary>
/// S13: Política de desconto — centraliza as regras de validação de percentual de desconto.
/// Antes: validação inline no SaleService (if DiscountPercent &lt; 0 / &gt; MaxDiscountPercentage).
/// Padrão herdado do PasswordPolicy (S12).
/// </summary>
public static class DescontoPolicy
{
    // Cultura pt-BR para formatação de percentuais nas mensagens de erro.
    // Garante "10,00%" independente da cultura do servidor/CI (que usa "." como separador).
    private static readonly System.Globalization.CultureInfo _ptBR =
        System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Valida o percentual de desconto de um item contra o limite máximo permitido pelo cargo.
    /// Retorna (true, null) se válido, (false, mensagemErro) se inválido.
    /// </summary>
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
                $"Desconto de {discountPercent.ToString("F2", _ptBR)}% excede o limite do seu cargo " +
                $"({maxDiscountPercentage.ToString("F2", _ptBR)}%)" +
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