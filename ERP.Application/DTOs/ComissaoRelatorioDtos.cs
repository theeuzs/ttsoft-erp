// ── ERP.Application/DTOs/ComissaoRelatorioDtos.cs ────────────────────────────
namespace ERP.Application.DTOs;

// Nomeado "...Relatorio..." para não colidir com ComissaoVendedorDto e
// ComissaoResultadoDto (RelatoriosDtos.cs), que pertencem a um cálculo
// diferente (percentual único manual, usado pelo WPF) — ver comentário
// em IComissaoRelatorioService.cs.

public record ComissaoVendedorRelatorioDto(
    string  Vendedor,
    int     QtdVendas,
    decimal TotalVendido,
    decimal PercentualComissao,
    decimal ValorComissao);

public record ComissaoRelatorioDto(
    DateTime Inicio,
    DateTime Fim,
    IReadOnlyList<ComissaoVendedorRelatorioDto> Vendedores,
    decimal TotalComissoes,
    decimal TotalVendido);
