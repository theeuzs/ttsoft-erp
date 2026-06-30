namespace ERP.Application.DTOs;

/// <summary>Solicita envio de link de recuperação de senha.</summary>
public class ForgotPasswordDto
{
    /// <summary>CNPJ da empresa (14 dígitos) — identifica o tenant.</summary>
    public string Cnpj     { get; set; } = string.Empty;
    /// <summary>E-mail do usuário cadastrado.</summary>
    public string Email    { get; set; } = string.Empty;
}

/// <summary>Define nova senha usando o token recebido por e-mail.</summary>
public class ResetPasswordDto
{
    /// <summary>Token JWT de reset (enviado por e-mail, válido por 1h).</summary>
    public string Token       { get; set; } = string.Empty;
    /// <summary>Nova senha (mínimo 8 chars + 1 número).</summary>
    public string NovaSenha   { get; set; } = string.Empty;
}
