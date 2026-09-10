using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementação do serviço de Produtos Agregados.
/// FICA EM ERP.Infrastructure (não ERP.Application) — acessa AppDbContext
/// diretamente. ERP.Application não referencia ERP.Persistence.
/// </summary>
public class ProdutoAgregadoService : IProdutoAgregadoService
{
    private readonly AppDbContext _ctx;
    private readonly IRequestTenant _tenant;
    public ProdutoAgregadoService(AppDbContext ctx, IRequestTenant tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<IEnumerable<ProdutoAgregadoDto>> GetSugestoesAsync(Guid produtoPrincipalId)
    {
        return await _ctx.ProdutosAgregados
            .AsNoTracking()
            .Where(pa => pa.ProdutoPrincipalId == produtoPrincipalId
                      && pa.ProdutoRelacionado.IsActive
                      && pa.ProdutoRelacionado.Stock > 0)
            .OrderBy(pa => pa.Ordem).ThenBy(pa => pa.ProdutoRelacionado.Name)
            .Take(6)
            .Select(pa => new ProdutoAgregadoDto(
                pa.Id, pa.ProdutoRelacionadoId,
                pa.ProdutoRelacionado.Name, pa.ProdutoRelacionado.Barcode,
                pa.ProdutoRelacionado.Unit, pa.ProdutoRelacionado.SalePrice,
                pa.ProdutoRelacionado.Stock, pa.ProdutoRelacionado.ImageUrl, pa.Ordem))
            .ToListAsync();
    }

    public async Task<IEnumerable<ProdutoAgregadoDto>> GetAgregadosAsync(Guid produtoPrincipalId)
    {
        return await _ctx.ProdutosAgregados
            .AsNoTracking()
            .Where(pa => pa.ProdutoPrincipalId == produtoPrincipalId)
            .OrderBy(pa => pa.Ordem).ThenBy(pa => pa.ProdutoRelacionado.Name)
            .Select(pa => new ProdutoAgregadoDto(
                pa.Id, pa.ProdutoRelacionadoId,
                pa.ProdutoRelacionado.Name, pa.ProdutoRelacionado.Barcode,
                pa.ProdutoRelacionado.Unit, pa.ProdutoRelacionado.SalePrice,
                pa.ProdutoRelacionado.Stock, pa.ProdutoRelacionado.ImageUrl, pa.Ordem))
            .ToListAsync();
    }

    public async Task SalvarAgregadosAsync(Guid produtoPrincipalId, IEnumerable<SalvarAgregadoItemDto> itens)
    {
        var lista = itens.ToList();
        if (lista.Any(i => i.ProdutoRelacionadoId == produtoPrincipalId))
            throw new InvalidOperationException("Um produto não pode ser agregado a si mesmo.");

        var atuais    = await _ctx.ProdutosAgregados.AsTracking().Where(pa => pa.ProdutoPrincipalId == produtoPrincipalId).ToListAsync();
        var novosIds  = lista.Select(i => i.ProdutoRelacionadoId).ToHashSet();
        var ateaisIds = atuais.Select(a => a.ProdutoRelacionadoId).ToHashSet();

        _ctx.ProdutosAgregados.RemoveRange(atuais.Where(a => !novosIds.Contains(a.ProdutoRelacionadoId)));

        // Achado (10/09) — "atuais" vinha sem AsTracking() (AppDbContext é
        // NoTracking global), então mudar Ordem numa linha existente nunca
        // persistia. Corrigido acima, adicionando .AsTracking() na consulta.
        foreach (var atual in atuais.Where(a => novosIds.Contains(a.ProdutoRelacionadoId)))
        {
            var item = lista.First(i => i.ProdutoRelacionadoId == atual.ProdutoRelacionadoId);
            if (atual.Ordem != item.Ordem) atual.Ordem = item.Ordem;
        }

        // Achado (10/09) — BUG PRINCIPAL: TenantId nunca era setado aqui.
        // A linha nova salvava com TenantId vazio (Guid.Empty), e como toda
        // consulta subsequente filtra por tenant (HasQueryFilter), a
        // sugestão recém-adicionada nunca mais aparecia — parecia "sumir",
        // mas na real ficava salva no banco, invisível pro tenant certo.
        await _ctx.ProdutosAgregados.AddRangeAsync(lista
            .Where(i => !ateaisIds.Contains(i.ProdutoRelacionadoId))
            .Select(i => new ProdutoAgregado
            {
                TenantId             = _tenant.TenantId,
                ProdutoPrincipalId   = produtoPrincipalId,
                ProdutoRelacionadoId = i.ProdutoRelacionadoId,
                Ordem                = i.Ordem
            }));

        await _ctx.SaveChangesAsync();
    }

    public async Task RemoverAgregadoAsync(Guid produtoPrincipalId, Guid produtoRelacionadoId)
    {
        var ag = await _ctx.ProdutosAgregados.FirstOrDefaultAsync(pa =>
            pa.ProdutoPrincipalId == produtoPrincipalId &&
            pa.ProdutoRelacionadoId == produtoRelacionadoId);
        if (ag is null) return;
        _ctx.ProdutosAgregados.Remove(ag);
        await _ctx.SaveChangesAsync();
    }
}