namespace ERP.Application.DTOs;

/// <summary>Solicita envio de link de recuperação de senha.</summary>
public class ForgotPasswordDto
{
    public string Cnpj     { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
}

/// <summary>Define nova senha usando o token recebido por e-mail.</summary>
public class ResetPasswordDto
{
    public string Token       { get; set; } = string.Empty;
    public string NovaSenha   { get; set; } = string.Empty;
}

/// <summary>S13: Atualiza o e-mail do usuário autenticado.</summary>
public class AtualizarEmailDto
{
    public string Email { get; set; } = string.Empty;
}