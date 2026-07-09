// ── ERP.Infrastructure/Services/TintometricoService.cs ───────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class TintometricoService : ITintometricoService
{
    private readonly AppDbContext _ctx;
    public TintometricoService(AppDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<FormulaDto>> GetAllAsync(string? busca, CancellationToken ct = default)
    {
        var query = _ctx.FormulasTintometricas.AsNoTracking()
            .Include(f => f.Product)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(busca))
            query = query.Where(f => f.NomeCor.Contains(busca)
                                  || f.CodigoFabricante.Contains(busca)
                                  || f.Fabricante.Contains(busca)
                                  || f.Product.Name.Contains(busca));

        return await query
            .OrderBy(f => f.Fabricante).ThenBy(f => f.NomeCor)
            .Select(f => new FormulaDto(f))
            .ToListAsync(ct);
    }

    public async Task<FormulaDto?> GetByProductAsync(Guid productId, CancellationToken ct = default)
        => await _ctx.FormulasTintometricas.AsNoTracking()
            .Include(f => f.Product)
            .Where(f => f.ProductId == productId)
            .Select(f => new FormulaDto(f))
            .FirstOrDefaultAsync(ct);

    public async Task UpsertAsync(SalvarFormulaDto dto, CancellationToken ct = default)
    {
        // S16 FIX: validar que ProductId pertence ao tenant atual antes de
        // criar a fórmula. Sem isso, um dto.ProductId de OUTRO tenant passa
        // batido — HasQueryFilter bloqueia a leitura (existente fica null),
        // então o código entrava no ramo "cria nova", gerando uma fórmula
        // órfã (ProductId de B, TenantId de A). Não é vazamento cross-tenant
        // (ninguém lê dado de B), mas quebra GetAllAsync/GetByProductAsync
        // depois: o .Include(f => f.Product) retorna Product null (o
        // HasQueryFilter também bloqueia a leitura do produto de B), e
        // MapToParcelaDto-equivalente aqui (new FormulaDto(f)) tentando
        // acessar f.Product.Name quebra com NullReferenceException — um
        // DoS auto-infligido: usuário derruba a própria listagem de fórmulas.
        var produtoExiste = await _ctx.Products.AnyAsync(p => p.Id == dto.ProductId, ct);
        if (!produtoExiste)
            throw new KeyNotFoundException($"Produto {dto.ProductId} não encontrado.");

        var existente = await _ctx.FormulasTintometricas
            .Where(f => f.ProductId == dto.ProductId)
            .FirstOrDefaultAsync(ct);

        if (existente is null)
        {
            var nova = new FormulaTintometrica
            {
                Id                   = Guid.NewGuid(),
                ProductId            = dto.ProductId,
                Fabricante           = dto.Fabricante,
                CodigoFabricante     = dto.CodigoFabricante,
                NomeCor              = dto.NomeCor,
                Base                 = dto.Base,
                RendimentoM2PorLitro = dto.RendimentoM2PorLitro,
                DemaosRecomendadas   = dto.DemaosRecomendadas,
                CorantesJson         = dto.CorantesJson,
                Observacoes          = dto.Observacoes,
                CreatedAt            = DateTime.UtcNow
            };
            _ctx.FormulasTintometricas.Add(nova);
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

        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid productId, CancellationToken ct = default)
    {
        var f = await _ctx.FormulasTintometricas
            .Where(f => f.ProductId == productId)
            .FirstOrDefaultAsync(ct);

        if (f is null) return false;

        f.IsDeleted = true;
        f.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CalculoTintaResultadoDto?> CalcularAsync(
        Guid productId, decimal areaM2, int demaos, CancellationToken ct = default)
    {
        var f = await _ctx.FormulasTintometricas.AsNoTracking()
            .Include(f => f.Product)
            .Where(f => f.ProductId == productId)
            .FirstOrDefaultAsync(ct);

        if (f is null) return null;
        if (areaM2 <= 0) throw new InvalidOperationException("Área deve ser maior que zero.");

        var d      = demaos > 0 ? demaos : f.DemaosRecomendadas;
        var litros = Math.Ceiling(areaM2 * d / f.RendimentoM2PorLitro * 100) / 100m;

        // Calcula latas: tenta dividir por tamanhos comuns (18L, 3.6L, 900ml)
        var latas18L   = Math.Floor(litros / 18m);
        var resto18    = litros - latas18L * 18m;
        var latas36L   = Math.Floor(resto18 / 3.6m);
        var resto36    = resto18 - latas36L * 3.6m;
        var latas900ml = Math.Ceiling(resto36 / 0.9m);

        return new CalculoTintaResultadoDto(
            Produto:              f.Product.Name,
            NomeCor:              f.NomeCor,
            CodigoFabricante:     f.CodigoFabricante,
            AreaM2:               areaM2,
            Demaos:               d,
            RendimentoM2PorLitro: f.RendimentoM2PorLitro,
            LitrosNecessarios:    litros,
            Latas: new LatasDto(
                Galoes18L:   latas18L,
                Galoes3_6L:  latas36L,
                Latas900ml:  latas900ml,
                TotalLitros: latas18L * 18 + latas36L * 3.6m + latas900ml * 0.9m),
            CustoEstimado: Math.Round(litros * f.Product.SalePrice, 2),
            CorantesJson:  f.CorantesJson);
    }
}