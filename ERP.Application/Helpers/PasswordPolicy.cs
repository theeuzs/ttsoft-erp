namespace ERP.Application.Helpers;

/// <summary>
/// S12: Política de senha centralizada — elimina inconsistência entre
/// CadastroController (12 chars), AuthController.ResetPassword (8 chars) e
/// AuthService.ChangePasswordAsync (8 chars sem dígito obrigatório).
/// Antes: cada ponto validava com critérios diferentes — downgrade trivial via reset.
/// Agora: alterar aqui propaga para todos os fluxos.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 12;

    /// <summary>
    /// Valida a senha contra a política definida.
    /// Retorna (true, null) se válida, (false, mensagemErro) se inválida.
    /// </summary>
    public static (bool Ok, string? Erro) Validar(string? senha)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < MinLength)
            return (false, $"Senha deve ter no mínimo {MinLength} caracteres.");

        if (!senha.Any(char.IsDigit))
            return (false, "Senha deve conter pelo menos um número.");

        return (true, null);
    }
}
