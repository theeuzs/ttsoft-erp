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

    /// <summary>
    /// S18 FIX (13/08) — "barra cravada": quando o produto vende em pacote
    /// fechado (ex: barra de 6m por R$59,90 o pacote inteiro), replica
    /// exatamente a mesma conta que o WPF já faz em CartItem.Total
    /// (PdvViewModel.cs) — preço do pacote × pacotes fechados + sobra
    /// fracionária × preço normal. Confirmado com o dono do sistema
    /// (13/08): WholesalePrice é preço do PACOTE inteiro, não preço por
    /// unidade — usar WholesalePrice direto como preço unitário (como o
    /// SaleService fazia antes) multiplicava o valor errado pela
    /// quantidade toda vez que batia o mínimo de atacado (ex: 6 barras
    /// de R$59,90 cobradas como se fossem 6 × R$59,90 = R$359,40 em vez
    /// de R$59,90 pela barra inteira de 6 metros).
    /// </summary>
    public static (decimal Total, decimal PrecoUnitarioEquivalente) CalcularTotalAtacado(
        decimal quantity, decimal wholesaleMinQuantity, decimal wholesalePrice,
        decimal normalUnitPrice, decimal discountPercent)
    {
        decimal qtdPacotes    = System.Math.Floor(quantity / wholesaleMinQuantity);
        decimal sobraUnidades = quantity % wholesaleMinQuantity;

        decimal totalBruto = (qtdPacotes * wholesalePrice) + (sobraUnidades * normalUnitPrice);
        decimal total       = totalBruto * (1m - discountPercent / 100m);

        // Preço unitário "equivalente" — só pra exibição/coerência com o
        // que o cupom e o payload fiscal mostram (quantidade × preço tem
        // que bater com Total). Não é o preço real de venda por unidade,
        // que só existe como conceito pro pacote fechado.
        decimal precoUnitarioEquivalente = quantity > 0
            ? System.Math.Round(totalBruto / quantity, 2, System.MidpointRounding.AwayFromZero)
            : normalUnitPrice;

        return (total, precoUnitarioEquivalente);
    }
}