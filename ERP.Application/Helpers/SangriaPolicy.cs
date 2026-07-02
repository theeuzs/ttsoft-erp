namespace ERP.Application.Helpers;

/// <summary>
/// S13: Política de sangria — centraliza as regras de validação de retirada de caixa.
/// Antes: 2 validações separadas inline no CaixaService:
///   1. valor &gt; saldoDinheiro (integridade contábil — S8)
///   2. MaxSangriaValue por cargo — não estava sendo validado no fluxo de sangria
/// Padrão herdado do PasswordPolicy (S12).
/// </summary>
public static class SangriaPolicy
{
    /// <summary>
    /// Valida o valor de uma sangria contra o saldo disponível e o limite do cargo.
    /// Retorna (true, null) se válida, (false, mensagemErro) se inválida.
    /// </summary>
    /// <param name="valor">Valor solicitado para sangria.</param>
    /// <param name="saldoDinheiro">Saldo atual em dinheiro no caixa.</param>
    /// <param name="maxSangriaValue">Limite máximo de sangria do cargo (0 = sem limite).</param>
    public static (bool Ok, string? Erro) Validar(
        decimal valor,
        decimal saldoDinheiro,
        decimal maxSangriaValue = 0m)
    {
        if (valor <= 0)
            return (false, "Valor de sangria deve ser maior que zero.");

        // S8 FIX: sangria não pode exceder saldo em dinheiro (integridade contábil)
        if (valor > saldoDinheiro)
            return (false,
                $"Sangria de R$ {valor:F2} excede o saldo em dinheiro do caixa: R$ {saldoDinheiro:F2}.");

        // Limite por cargo — 0 = sem limite (role Administrador/Gerente)
        if (maxSangriaValue > 0 && valor > maxSangriaValue)
            return (false,
                $"Sangria de R$ {valor:F2} excede o limite do seu cargo: R$ {maxSangriaValue:F2}.");

        return (true, null);
    }
}
