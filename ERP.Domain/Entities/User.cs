using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty; 
    public string Username { get; set; } = string.Empty; 
    public string PasswordHash { get; set; } = string.Empty; 
    public bool IsActive { get; set; } = true;

    // ── Proteção contra força bruta ──────────────────────────────────────
    /// <summary>Contador de tentativas de login com falha consecutivas.</summary>
    public int FailedLoginAttempts { get; set; } = 0;
    /// <summary>Data/hora UTC até a qual o login está bloqueado. Null = não bloqueado.</summary>
    public DateTime? LockoutEndUtc { get; set; }

    // NOVO RBAC: Substitui o antigo Enum
    public Guid? RoleId { get; set; }
    public Role? Role { get; set; } = null!; // O null! avisa o compilador que o EF Core vai preencher isso
}