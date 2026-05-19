using ERP.Domain.Common;

namespace ERP.Domain.Entities;

public class Role : BaseEntity
{
    // Ex: "Gerente", "Caixa"
    public string Name { get; set; } = string.Empty;

    // Regras de negócio específicas
    public decimal MaxDiscountPercentage { get; set; }
    public decimal MaxSangriaValue { get; set; }

    // ── Sprint E: Comissão por cargo ──────────────────────────────────────────
    /// <summary>Percentual de comissão sobre o total vendido. 0 = sem comissão.</summary>
    public decimal PercentualComissao { get; set; } = 0;

    // Relacionamentos
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    public ICollection<User> Users { get; set; } = new List<User>(); // Ajustado de Usuarios para Users para manter o padrão em inglês
}