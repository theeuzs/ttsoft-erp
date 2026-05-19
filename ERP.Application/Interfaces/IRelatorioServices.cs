// ERP.Application/Interfaces/IRelatorioServices.cs
using ERP.Application.DTOs;
using System.Threading;

namespace ERP.Application.Interfaces;

public interface IDreService
{
    Task<DreResultadoDto> CalcularAsync(
        DateTime dataInicio, DateTime dataFim,
        CancellationToken ct = default);
}

public interface IAbcService
{
    /// <summary>Retorna os itens em ordem decrescente de faturamento.</summary>
    Task<IReadOnlyList<AbcItemDto>> CalcularAsync(
        DateTime dataInicio, DateTime dataFim,
        CancellationToken ct = default);
}

public interface IComissaoService
{
    Task<ComissaoResultadoDto> CalcularAsync(
        DateTime dataInicio, DateTime dataFim, decimal percentual,
        CancellationToken ct = default);
}

public interface IMargemService
{
    Task<IReadOnlyList<MargemProdutoDto>> ObterAsync(
        CancellationToken ct = default);
}

public interface IFluxoCaixaService
{
    Task<FluxoCaixaResultadoDto> ObterAsync(
        DateTime dataInicio, DateTime dataFim,
        CancellationToken ct = default);
}

public interface IHaverService
{
    Task<decimal> ObterSaldoAsync(Guid customerId, CancellationToken ct = default);
    Task<IReadOnlyList<HaverHistoricoDto>> ObterHistoricoAsync(Guid customerId, CancellationToken ct = default);
    Task LancarAsync(Guid customerId, decimal valor, string tipo, string descricao, string operadorNome);
    Task RegistrarMovimentoVendaAsync(Guid customerId, decimal valor, string tipo, string descricao, string operadorNome);
}

public interface IInventarioService
{
    Task<IReadOnlyList<InventarioProdutoDto>> ObterProdutosAsync(CancellationToken ct = default);
    Task AplicarAjustesAsync(IEnumerable<(Guid ProductId, decimal NovoEstoque)> ajustes);
}

// ── BI Avançado ───────────────────────────────────────────────────────────────
public interface IBIService
{
    Task<IReadOnlyList<SazonalidadeDto>>  ObterSazonalidadeAsync(int meses = 12, CancellationToken ct = default);
    Task<IReadOnlyList<AbcAvancadoDto>>   ObterAbcAvancadoAsync(DateTime inicio, DateTime fim, CancellationToken ct = default);
    Task<DreDetalhadoDto>                 ObterDreDetalhadoAsync(DateTime inicio, DateTime fim, CancellationToken ct = default);
    Task<IReadOnlyList<RankingVendedorDto>> ObterRankingVendedoresAsync(DateTime inicio, DateTime fim, CancellationToken ct = default);
    Task<IReadOnlyList<PrevisaoDemandaDto>> ObterPrevisaoDemandaAsync(CancellationToken ct = default);
}
