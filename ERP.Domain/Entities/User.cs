using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty; 
    public string Username { get; set; } = string.Empty; 
    public string PasswordHash { get; set; } = string.Empty; 
    public bool IsActive { get; set; } = true;
    /// <summary>1.6.8: Se true, o usuário deve trocar a senha no próximo login.</summary>
    public bool MustChangePassword { get; set; } = false;

    // ── S11: Email para recuperação de senha ──────────────────────────────
    /// <summary>E-mail do usuário — usado para envio de link de recuperação de senha.</summary>
    public string? Email { get; set; }

    // ── S12: Token de confirmação de cadastro (cross-check e-mail RFB) ───
    /// <summary>
    /// Token enviado ao e-mail RFB quando há divergência no cadastro.
    /// Null = conta ativa sem pendência de confirmação.
    /// </summary>
    public string?   ConfirmacaoToken         { get; set; }

    // S13 FIX: expiração do token (48h) — sem isso, squatting vira DoS permanente:
    // atacante pré-registra com e-mail divergente, dono real fica bloqueado para sempre.
    /// <summary>Data/hora UTC em que o ConfirmacaoToken expira (48h após criação).</summary>
    public DateTime? ConfirmacaoTokenExpiraEm { get; set; }

    // ── Proteção contra força bruta ──────────────────────────────────────
    /// <summary>Contador de tentativas de login com falha consecutivas.</summary>
    public int FailedLoginAttempts { get; set; } = 0;
    /// <summary>Data/hora UTC até a qual o login está bloqueado. Null = não bloqueado.</summary>
    public DateTime? LockoutEndUtc { get; set; }

    // NOVO RBAC: Substitui o antigo Enum
    public Guid? RoleId { get; set; }
    public Role? Role { get; set; } = null!;
}