using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComissaoController : ControllerBase
{
    private readonly AppDbContext _db;

    public ComissaoController(AppDbContext db) => _db = db;

    /// <summary>
    /// Calcula comissões por vendedor no período.
    /// A taxa de comissão vem do campo PercentualComissao do cargo (Role) do usuário.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? inicio = null,
        [FromQuery] DateTime? fim    = null)
    {
        var ini = inicio ?? DateTime.Today.AddMonths(-1);
        var end = fim    ?? DateTime.Today.AddDays(1);

        // Agrupa vendas por vendedor no período
        var vendas = await _db.Sales.AsNoTracking()
            .Where(s => s.SaleDate >= ini && s.SaleDate < end
                     && s.Status != ERP.Domain.Enums.SaleStatus.Cancelada)
            .GroupBy(s => s.SellerName ?? "Sem vendedor")
            .Select(g => new
            {
                Vendedor    = g.Key,
                QtdVendas   = g.Count(),
                TotalVendido = g.Sum(s => s.Total)
            })
            .ToListAsync();

        // Busca percentuais de comissão por usuário/cargo
        var usuarios = await _db.Users.AsNoTracking()
            .Include(u => u.Role)
            .Select(u => new { u.Name, Percentual = u.Role != null ? u.Role.PercentualComissao : 0m })
            .ToListAsync();

        var comissoes = vendas.Select(v =>
        {
            var pct = usuarios
                .FirstOrDefault(u => u.Name == v.Vendedor)?.Percentual ?? 0m;

            return new ComissaoVendedorResult
            {
                Vendedor         = v.Vendedor,
                QtdVendas        = v.QtdVendas,
                TotalVendido     = v.TotalVendido,
                PercentualComissao = pct,
                ValorComissao    = Math.Round(v.TotalVendido * pct / 100m, 2)
            };
        })
        .OrderByDescending(c => c.TotalVendido)
        .ToList();

        return Ok(new
        {
            Periodo        = new { Inicio = ini, Fim = end.AddDays(-1) },
            Vendedores     = comissoes,
            TotalComissoes = comissoes.Sum(c => c.ValorComissao),
            TotalVendido   = comissoes.Sum(c => c.TotalVendido)
        });
    }
}

public class ComissaoVendedorResult
{
    public string  Vendedor           { get; set; } = "";
    public int     QtdVendas          { get; set; }
    public decimal TotalVendido       { get; set; }
    public decimal PercentualComissao { get; set; }
    public decimal ValorComissao      { get; set; }
}
