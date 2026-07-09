// ERP.Application/Services/RelatoriosCacheDecorators.cs
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;

namespace ERP.Application.Services;

// ═══════════════════════════════════════════════════════════════════════════════
//  DRE — cache por período
// ═══════════════════════════════════════════════════════════════════════════════
public class DreServiceCached : IDreService
{
    private readonly IDreService  _inner;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public DreServiceCached(IDreService inner, IMemoryCache cache)
        => (_inner, _cache) = (inner, cache);

    public async Task<DreResultadoDto> CalcularAsync(
        DateTime dataInicio, DateTime dataFim, CancellationToken ct = default)
    {
        string key = $"dre:{dataInicio:yyyyMMdd}:{dataFim:yyyyMMdd}";
        if (_cache.TryGetValue(key, out DreResultadoDto? cached) && cached is not null)
            return cached;

        var resultado = await _inner.CalcularAsync(dataInicio, dataFim, ct);
        _cache.Set(key, resultado, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
        return resultado;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  CURVA ABC — cache por período
// ═══════════════════════════════════════════════════════════════════════════════
public class AbcServiceCached : IAbcService
{
    private readonly IAbcService  _inner;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public AbcServiceCached(IAbcService inner, IMemoryCache cache)
        => (_inner, _cache) = (inner, cache);

    public async Task<IReadOnlyList<AbcItemDto>> CalcularAsync(
        DateTime dataInicio, DateTime dataFim, CancellationToken ct = default)
    {
        string key = $"abc:{dataInicio:yyyyMMdd}:{dataFim:yyyyMMdd}";
        if (_cache.TryGetValue(key, out IReadOnlyList<AbcItemDto>? cached) && cached is not null)
            return cached;

        var resultado = await _inner.CalcularAsync(dataInicio, dataFim, ct);
        _cache.Set(key, resultado, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
        return resultado;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  COMISSÃO — cache por período + percentual
// ═══════════════════════════════════════════════════════════════════════════════
public class ComissaoServiceCached : IComissaoService
{
    private readonly IComissaoService _inner;
    private readonly IMemoryCache     _cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public ComissaoServiceCached(IComissaoService inner, IMemoryCache cache)
        => (_inner, _cache) = (inner, cache);

    public async Task<ComissaoResultadoDto> CalcularAsync(
        DateTime dataInicio, DateTime dataFim, decimal percentual, CancellationToken ct = default)
    {
        string key = $"comissao:{dataInicio:yyyyMMdd}:{dataFim:yyyyMMdd}:{percentual}";
        if (_cache.TryGetValue(key, out ComissaoResultadoDto? cached) && cached is not null)
            return cached;

        var resultado = await _inner.CalcularAsync(dataInicio, dataFim, percentual, ct);
        _cache.Set(key, resultado, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
        return resultado;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  MARGEM — cache fixo
// ═══════════════════════════════════════════════════════════════════════════════
public class MargemServiceCached : IMargemService
{
    private readonly IMargemService _inner;
    private readonly IMemoryCache   _cache;
    private const    string         Key = "margem:todos";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    public MargemServiceCached(IMargemService inner, IMemoryCache cache)
        => (_inner, _cache) = (inner, cache);

    public async Task<IReadOnlyList<MargemProdutoDto>> ObterAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(Key, out IReadOnlyList<MargemProdutoDto>? cached) && cached is not null)
            return cached;

        var resultado = await _inner.ObterAsync(ct);
        _cache.Set(Key, resultado, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
        return resultado;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  FLUXO DE CAIXA — cache curto (dados do dia corrente)
// ═══════════════════════════════════════════════════════════════════════════════
public class FluxoCaixaServiceCached : IFluxoCaixaService
{
    private readonly IFluxoCaixaService _inner;
    private readonly IMemoryCache       _cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(1);

    public FluxoCaixaServiceCached(IFluxoCaixaService inner, IMemoryCache cache)
        => (_inner, _cache) = (inner, cache);

    public async Task<FluxoCaixaResultadoDto> ObterAsync(
        DateTime dataInicio, DateTime dataFim, CancellationToken ct = default)
    {
        string key = $"fluxo:{dataInicio:yyyyMMdd}:{dataFim:yyyyMMdd}";
        if (_cache.TryGetValue(key, out FluxoCaixaResultadoDto? cached) && cached is not null)
            return cached;

        var resultado = await _inner.ObterAsync(dataInicio, dataFim, ct);
        _cache.Set(key, resultado, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
        return resultado;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//  INVENTÁRIO — cache curto (lista de produtos pode mudar)
// ═══════════════════════════════════════════════════════════════════════════════
public class InventarioServiceCached : IInventarioService
{
    private readonly IInventarioService _inner;
    private readonly IMemoryCache       _cache;
    private const    string             Key = "inventario:produtos";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public InventarioServiceCached(IInventarioService inner, IMemoryCache cache)
        => (_inner, _cache) = (inner, cache);

    public async Task<IReadOnlyList<InventarioProdutoDto>> ObterProdutosAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(Key, out IReadOnlyList<InventarioProdutoDto>? cached) && cached is not null)
            return cached;

        var resultado = await _inner.ObterProdutosAsync(ct);
        _cache.Set(Key, resultado, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
        return resultado;
    }

    // Não cacheado: é uma consulta parametrizada por um conjunto de IDs que muda
    // a cada chamada (itens contados pelo usuário), diferente da listagem geral
    // acima — cachear por chave de ID-set não traria o mesmo ganho e complicaria
    // a invalidação sem necessidade.
    public Task<IReadOnlyList<InventarioProdutoDto>> ObterProdutosPorIdsAsync(
        IEnumerable<Guid> ids, CancellationToken ct = default)
        => _inner.ObterProdutosPorIdsAsync(ids, ct);

    public async Task AplicarAjustesAsync(IEnumerable<(Guid ProductId, decimal NovoEstoque)> ajustes)
    {
        await _inner.AplicarAjustesAsync(ajustes);
        _cache.Remove(Key); // Invalida após ajuste
    }
}