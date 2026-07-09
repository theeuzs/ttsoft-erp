// ── ERP.Application/DTOs/ConciliacaoDtos.cs ──────────────────────────────────
namespace ERP.Application.DTOs;

public record ConciliacaoResultadoDto(
    int      TotalLinhas,
    int      Conciliados,
    int      NaoConciliados,
    decimal  TotalExtrato,
    decimal  TotalConciliado,
    DateTime Inicio,
    DateTime Fim,
    IReadOnlyList<ItemConciliacaoDto> Itens);

public record ItemConciliacaoDto(
    DateTime DataExtrato,
    decimal  ValorExtrato,
    string   DescExtrato,
    bool     Conciliado,
    string?  NumeroVenda,
    Guid?    VendaId,
    decimal? ValorVenda,
    string?  VendedorVenda,
    decimal? Diferenca);
