using ERP.Application.Interfaces;
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BiController : ControllerBase
{
    private readonly IBIService _bi;
    public BiController(IBIService bi) => _bi = bi;

    /// <summary>Faturamento mensal dos últimos N meses (padrão: 12).</summary>
    [HttpGet("sazonalidade")]
    public async Task<IActionResult> Sazonalidade(
        [FromQuery] int meses = 12, CancellationToken ct = default)
        => Ok(await _bi.ObterSazonalidadeAsync(meses, ct));

    /// <summary>Curva ABC avançada com margem e classificação A/B/C.</summary>
    [HttpGet("abc")]
    public async Task<IActionResult> Abc(
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        CancellationToken ct = default)
        => Ok(await _bi.ObterAbcAvancadoAsync(inicio, fim, ct));

    /// <summary>DRE detalhado com linhas de despesa categorizadas.</summary>
    [HttpGet("dre-detalhado")]
    public async Task<IActionResult> DreDetalhado(
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        CancellationToken ct = default)
        => Ok(await _bi.ObterDreDetalhadoAsync(inicio, fim, ct));

    /// <summary>Ranking de vendedores por faturamento.</summary>
    [HttpGet("ranking-vendedores")]
    public async Task<IActionResult> RankingVendedores(
        [FromQuery] DateTime inicio, [FromQuery] DateTime fim,
        CancellationToken ct = default)
        => Ok(await _bi.ObterRankingVendedoresAsync(inicio, fim, ct));

    /// <summary>Previsão de demanda com sugestão de compra para produtos críticos.</summary>
    [HttpGet("previsao-demanda")]
    public async Task<IActionResult> PrevisaoDemanda(CancellationToken ct = default)
        => Ok(await _bi.ObterPrevisaoDemandaAsync(ct));
}
