using ERP.Domain.Common;

namespace ERP.Domain.Entities;

/// <summary>
/// Fórmula tintométrica vinculada a um produto tinta.
/// Armazena fabricante, código, base e corantes para reprodução da cor.
/// Corantes são serializados em JSON para evitar tabela extra — são consultados
/// sempre junto com a fórmula, nunca filtrados individualmente.
/// </summary>
public class FormulaTintometrica : BaseEntity
{
    /// <summary>Produto tinta ao qual a fórmula pertence.</summary>
    public Guid    ProductId        { get; set; }
    public Product Product          { get; set; } = null!;

    /// <summary>Ex: "Suvinil", "Coral", "Renner", "Iquine", "Sherwin-Williams"</summary>
    public string  Fabricante       { get; set; } = string.Empty;

    /// <summary>Código do fabricante. Ex: "SV-0987", "CL-1234"</summary>
    public string  CodigoFabricante { get; set; } = string.Empty;

    /// <summary>Nome comercial da cor. Ex: "Branco Neve", "Azul Serenidade"</summary>
    public string  NomeCor          { get; set; } = string.Empty;

    /// <summary>Base da tinta. Ex: "Branca", "Pastel", "Média", "Profunda", "Transparente"</summary>
    public string  Base             { get; set; } = string.Empty;

    /// <summary>Rendimento em m² por litro por demão. Ex: 10 = 10m²/L/demão</summary>
    public decimal RendimentoM2PorLitro { get; set; } = 10m;

    /// <summary>Número de demãos recomendadas pelo fabricante.</summary>
    public int     DemaosRecomendadas   { get; set; } = 2;

    /// <summary>
    /// Corantes em JSON. Formato: [{"Nome":"Ocre","Ml":12.5},{"Nome":"Amarelo","Ml":3.2}]
    /// ML por litro de base. Nunca filtramos por corante individual — JSON é suficiente.
    /// </summary>
    public string? CorantesJson      { get; set; }

    public string? Observacoes       { get; set; }
}
