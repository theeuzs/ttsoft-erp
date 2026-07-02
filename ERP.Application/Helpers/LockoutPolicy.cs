namespace ERP.Application.Helpers;

/// <summary>
/// S13: Política de lockout de conta — centraliza as regras de bloqueio por força bruta.
/// Antes: constantes MaxTentativas=5 e MinutosBloqueio=15 eram private const no AuthService,
/// invisíveis para testes isolados e sem possibilidade de override por config.
/// Padrão herdado do PasswordPolicy (S12).
/// </summary>
public static class LockoutPolicy
{
    /// <summary>Número de tentativas com falha antes de bloquear a conta.</summary>
    public const int MaxTentativas = 5;

    /// <summary>Duração do bloqueio em minutos após atingir MaxTentativas.</summary>
    public const int MinutosBloqueio = 15;

    /// <summary>
    /// Calcula o próximo estado de lockout dado o número de tentativas atual.
    /// Retorna (novasTentativas, lockoutAte) onde lockoutAte = null se ainda não bloqueado.
    /// </summary>
    public static (int NovasTentativas, DateTime? LockoutAte) Calcular(int tentativasAtuais)
    {
        var novas   = tentativasAtuais + 1;
        var lockout = novas >= MaxTentativas
            ? DateTime.UtcNow.AddMinutes(MinutosBloqueio)
            : (DateTime?)null;
        return (novas, lockout);
    }

    /// <summary>Retorna a mensagem de erro adequada ao estado atual de lockout.</summary>
    public static string MensagemErro(int novasTentativas, DateTime? lockoutAte)
    {
        if (lockoutAte.HasValue)
            return $"Usuário ou senha incorretos. Conta bloqueada por {MinutosBloqueio} minutos.";

        var restantes = MaxTentativas - novasTentativas;
        return $"Usuário ou senha incorretos. ({restantes} tentativa(s) restante(s))";
    }

    /// <summary>Verifica se a conta está bloqueada no momento.</summary>
    public static bool EstaBloqueada(DateTime? lockoutEndUtc)
        => lockoutEndUtc.HasValue && lockoutEndUtc > DateTime.UtcNow;

    /// <summary>Retorna minutos restantes de bloqueio (arredondado p/ cima).</summary>
    public static int MinutosRestantes(DateTime lockoutEndUtc)
        => (int)Math.Ceiling((lockoutEndUtc - DateTime.UtcNow).TotalMinutes);
}
