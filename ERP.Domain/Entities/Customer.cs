using ERP.Domain.Common;
using ERP.Domain.Enums;

namespace ERP.Domain.Entities;

public class Customer : BaseEntity
{
    public string Document { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? StateRegistration { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }

    // Endereço
    public string? ZipCode { get; set; }
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Complement { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }

    // Financeiro
    public decimal HaverBalance { get; set; } = 0;

    // ── Sprint C: Tabela de preços por grupo ──────────────────────────────────
    /// <summary>
    /// Grupo de preço do cliente (A=Varejo, B=Revendedor, C=Atacadista).
    /// Define qual coluna de preço é aplicada automaticamente no PDV.
    /// </summary>
    public GrupoPreco GrupoPreco { get; set; } = GrupoPreco.A;

    // ── Sprint D: Crediário ────────────────────────────────────────────────────
    /// <summary>Limite máximo de crédito a prazo. 0 = sem limite definido.</summary>
    public decimal LimiteCredito { get; set; } = 0;

    /// <summary>Saldo devedor atual (soma de contas a receber pendentes).</summary>
    public decimal SaldoDevedor { get; set; } = 0;

    /// <summary>Saldo de crédito disponível = LimiteCredito - SaldoDevedor.</summary>
    public decimal CreditoDisponivel => LimiteCredito > 0
        ? Math.Max(0, LimiteCredito - SaldoDevedor)
        : decimal.MaxValue;

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
}
