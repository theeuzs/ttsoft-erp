using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TintometricoController : ControllerBase
{
    private readonly AppDbContext _db;
    public TintometricoController(AppDbContext db) => _db = db;

    /// <summary>Lista todas as fórmulas do tenant com nome do produto.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? busca = null,
        CancellationToken ct = default)
    {
        var query = _db.FormulasTintometricas.AsNoTracking()
            .Include(f => f.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
            query = query.Where(f => f.NomeCor.Contains(busca)
                                  || f.CodigoFabricante.Contains(busca)
                                  || f.Fabricante.Contains(busca)
                                  || f.Product.Name.Contains(busca));

        var lista = await query
            .OrderBy(f => f.Fabricante).ThenBy(f => f.NomeCor)
            .Select(f => new FormulaDto(f))
            .ToListAsync(ct);

        return Ok(lista);
    }

    /// <summary>Busca fórmula por produto.</summary>
    [HttpGet("produto/{productId:guid}")]
    public async Task<IActionResult> GetByProduct(Guid productId, CancellationToken ct = default)
    {
        var f = await _db.FormulasTintometricas.AsNoTracking()
            .Include(f => f.Product)
            .Where(f => f.ProductId == productId)
            .Select(f => new FormulaDto(f))
            .FirstOrDefaultAsync(ct);

        return f is null ? NotFound() : Ok(f);
    }

    /// <summary>Cria ou atualiza fórmula de um produto (upsert).</summary>
    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] SalvarFormulaDto dto, CancellationToken ct = default)
    {
        var existente = await _db.FormulasTintometricas
            .Where(f => f.ProductId == dto.ProductId)
            .FirstOrDefaultAsync(ct);

        if (existente is null)
        {
            var nova = new FormulaTintometrica
            {
                Id                  = Guid.NewGuid(),
                ProductId           = dto.ProductId,
                Fabricante          = dto.Fabricante,
                CodigoFabricante    = dto.CodigoFabricante,
                NomeCor             = dto.NomeCor,
                Base                = dto.Base,
                RendimentoM2PorLitro = dto.RendimentoM2PorLitro,
                DemaosRecomendadas  = dto.DemaosRecomendadas,
                CorantesJson        = dto.CorantesJson,
                Observacoes         = dto.Observacoes,
                CreatedAt           = DateTime.UtcNow
            };
            _db.FormulasTintometricas.Add(nova);
        }
        else
        {
            existente.Fabricante           = dto.Fabricante;
            existente.CodigoFabricante     = dto.CodigoFabricante;
            existente.NomeCor              = dto.NomeCor;
            existente.Base                 = dto.Base;
            existente.RendimentoM2PorLitro = dto.RendimentoM2PorLitro;
            existente.DemaosRecomendadas   = dto.DemaosRecomendadas;
            existente.CorantesJson         = dto.CorantesJson;
            existente.Observacoes          = dto.Observacoes;
            existente.UpdatedAt            = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { mensagem = "Fórmula salva." });
    }

    /// <summary>Remove fórmula de um produto.</summary>
    [HttpDelete("produto/{productId:guid}")]
    public async Task<IActionResult> Delete(Guid productId, CancellationToken ct = default)
    {
        var f = await _db.FormulasTintometricas
            .Where(f => f.ProductId == productId)
            .FirstOrDefaultAsync(ct);

        if (f is null) return NotFound();

        f.IsDeleted = true;
        f.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(new { mensagem = "Fórmula removida." });
    }

    /// <summary>
    /// Calcula quantidade de tinta para uma área, baseado na fórmula cadastrada.
    /// Retorna litros necessários, número de latas e custo estimado.
    /// </summary>
    [HttpGet("calcular/{productId:guid}")]
    public async Task<IActionResult> Calcular(
        Guid    productId,
        [FromQuery] decimal areaM2,
        [FromQuery] int     demaos = 0,
        CancellationToken ct = default)
    {
        var f = await _db.FormulasTintometricas.AsNoTracking()
            .Include(f => f.Product)
            .Where(f => f.ProductId == productId)
            .FirstOrDefaultAsync(ct);

        if (f is null) return NotFound(new { erro = "Produto sem fórmula cadastrada." });
        if (areaM2 <= 0) return BadRequest(new { erro = "Área deve ser maior que zero." });

        var d       = demaos > 0 ? demaos : f.DemaosRecomendadas;
        var litros  = Math.Ceiling(areaM2 * d / f.RendimentoM2PorLitro * 100) / 100m;

        // Calcula latas: tenta dividir por tamanhos comuns (18L, 3.6L, 900ml)
        var latas18L   = Math.Floor(litros / 18m);
        var resto18    = litros - latas18L * 18m;
        var latas36L   = Math.Floor(resto18 / 3.6m);
        var resto36    = resto18 - latas36L * 3.6m;
        var latas900ml = Math.Ceiling(resto36 / 0.9m);

        return Ok(new
        {
            Produto             = f.Product.Name,
            NomeCor             = f.NomeCor,
            CodigoFabricante    = f.CodigoFabricante,
            AreaM2              = areaM2,
            Demaos              = d,
            RendimentoM2PorLitro = f.RendimentoM2PorLitro,
            LitrosNecessarios   = litros,
            Latas = new
            {
                Galoes18L       = latas18L,
                Galoes3_6L      = latas36L,
                Latas900ml      = latas900ml,
                TotalLitros     = latas18L * 18 + latas36L * 3.6m + latas900ml * 0.9m
            },
            CustoEstimado       = Math.Round(litros * f.Product.SalePrice, 2),
            CorantesJson        = f.CorantesJson
        });
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public class FormulaDto
{
    public FormulaDto() { }
    public FormulaDto(FormulaTintometrica f)
    {
        Id                  = f.Id;
        ProductId           = f.ProductId;
        ProductName         = f.Product?.Name ?? "";
        SalePrice           = f.Product?.SalePrice ?? 0;
        Fabricante          = f.Fabricante;
        CodigoFabricante    = f.CodigoFabricante;
        NomeCor             = f.NomeCor;
        Base                = f.Base;
        RendimentoM2PorLitro = f.RendimentoM2PorLitro;
        DemaosRecomendadas  = f.DemaosRecomendadas;
        CorantesJson        = f.CorantesJson;
        Observacoes         = f.Observacoes;
    }

    public Guid    Id                  { get; set; }
    public Guid    ProductId           { get; set; }
    public string  ProductName         { get; set; } = "";
    public decimal SalePrice           { get; set; }
    public string  Fabricante          { get; set; } = "";
    public string  CodigoFabricante    { get; set; } = "";
    public string  NomeCor             { get; set; } = "";
    public string  Base                { get; set; } = "";
    public decimal RendimentoM2PorLitro { get; set; }
    public int     DemaosRecomendadas  { get; set; }
    public string? CorantesJson        { get; set; }
    public string? Observacoes         { get; set; }
}

public class SalvarFormulaDto
{
    public Guid    ProductId           { get; set; }
    public string  Fabricante          { get; set; } = "";
    public string  CodigoFabricante    { get; set; } = "";
    public string  NomeCor             { get; set; } = "";
    public string  Base                { get; set; } = "";
    public decimal RendimentoM2PorLitro { get; set; } = 10m;
    public int     DemaosRecomendadas  { get; set; } = 2;
    public string? CorantesJson        { get; set; }
    public string? Observacoes         { get; set; }
}
