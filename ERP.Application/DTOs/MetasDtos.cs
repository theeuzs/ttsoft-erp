// ── ERP.Application/DTOs/MetasDtos.cs ────────────────────────────────────────
namespace ERP.Application.DTOs;

/// <summary>Payload de entrada para criar ou atualizar uma meta de vendas.</summary>
public class MetaVendasDto
{
    public string  VendedorNome { get; set; } = "Geral";
    public int     Mes          { get; set; }
    public int     Ano          { get; set; }
    public decimal ValorMeta    { get; set; }
    public string? Descricao    { get; set; }
}

/// <summary>Resultado de uma meta com progresso real calculado no banco.</summary>
public class MetaProgressoDto
{
    public Guid    Id           { get; init; }
    public string  VendedorNome { get; init; } = string.Empty;
    public int     Mes          { get; init; }
    public int     Ano          { get; init; }
    public decimal ValorMeta    { get; init; }
    public string? Descricao    { get; init; }
    public decimal Realizado    { get; init; }
    public decimal Percentual   { get; init; }
    public decimal Restante     { get; init; }
    public bool    Atingida     { get; init; }
}
