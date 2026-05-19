using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Calcula sugestões de compra baseadas no giro real dos últimos 30 dias.
/// Lógica: giro_diario = total_vendido_30d / 30
///         dias_ruptura = estoque_atual / giro_diario
///         qtd_sugerida = IdealStock - EstoqueAtual  (ou giro * 45 se IdealStock = 0)
/// </summary>
public class SugestaoComprasService : ISugestaoComprasService
{
    private readonly AppDbContext         _ctx;
    private readonly IPedidoCompraService _pedidoService;

    public SugestaoComprasService(AppDbContext ctx, IPedidoCompraService pedidoService)
    {
        _ctx          = ctx;
        _pedidoService = pedidoService;
    }

    public async Task<IEnumerable<SugestaoCompraDto>> GetSugestoesAsync()
    {
        var inicio30d = DateTime.Today.AddDays(-30);

        // Giro dos últimos 30 dias por produto
        var giros = await _ctx.SaleItems.AsNoTracking()
            .Where(i => i.Sale.SaleDate >= inicio30d
                     && i.Sale.Status != SaleStatus.Cancelada)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, TotalVendido = g.Sum(i => i.Quantity) })
            .ToListAsync();

        var prodIds = giros.Select(g => g.ProductId).ToList();

        // Produtos que venderam no período + produtos abaixo do mínimo (mesmo sem venda)
        var produtosCriticosIds = await _ctx.Products.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted && p.Stock <= p.MinStock && p.MinStock > 0)
            .Select(p => p.Id)
            .ToListAsync();

        var todosIds = prodIds.Union(produtosCriticosIds).Distinct().ToList();

        var produtos = await _ctx.Products.AsNoTracking()
            .Include(p => p.Supplier)
            .Where(p => todosIds.Contains(p.Id) && p.IsActive && !p.IsDeleted)
            .ToListAsync();

        var sugestoes = produtos.Select(p =>
        {
            var giro = giros.FirstOrDefault(g => g.ProductId == p.Id);
            var totalVendido30d = giro?.TotalVendido ?? 0;
            var giroDiario = totalVendido30d / 30m;

            var diasRuptura = giroDiario > 0
                ? (int)(p.Stock / giroDiario)
                : (p.Stock <= p.MinStock ? 0 : 999);

            // Quantidade sugerida: repõe até o IdealStock (ou 45 dias de cobertura se não configurado)
            var qtdIdeal = p.IdealStock > 0
                ? p.IdealStock
                : giroDiario * 45;
            var qtdSugerida = Math.Max(0, Math.Round(qtdIdeal - p.Stock, 2));

            return new SugestaoCompraDto
            {
                ProductId          = p.Id,
                Nome               = p.Name,
                SKU                = p.SKU,
                SupplierId         = p.SupplierId,
                FornecedorNome     = p.Supplier?.Name ?? "Fornecedor não definido",
                EstoqueAtual       = p.Stock,
                EstoqueMinimo      = p.MinStock,
                MediaVendas30Dias  = Math.Round(giroDiario, 3),
                DiasParaRuptura    = diasRuptura,
                QuantidadeSugerida = qtdSugerida,
                CustoUnitario      = p.CostPrice
            };
        })
        .Where(s => s.DiasParaRuptura <= 45 || s.EstoqueAtual <= s.EstoqueMinimo)
        .Where(s => s.QuantidadeSugerida > 0)
        .OrderBy(s => s.DiasParaRuptura)
        .ToList();

        return sugestoes;
    }

    public async Task<IEnumerable<Guid>> GerarPedidosCompraAsync(GerarPedidoCompraDto dto)
    {
        if (!dto.Itens.Any())
            throw new ArgumentException("Nenhum item informado.");

        // Agrupa por fornecedor — cada um gera um pedido separado
        var grupos = dto.Itens.GroupBy(i => i.SupplierId);
        var pedidosGerados = new List<Guid>();

        foreach (var grupo in grupos)
        {
            var fornecedor = grupo.First();
            var nomeForn = grupo.First().SupplierId.HasValue
                ? (await _ctx.Suppliers.AsNoTracking()
                    .Where(s => s.Id == grupo.First().SupplierId!.Value)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync() ?? "Fornecedor")
                : "Fornecedor não definido";

            var pedidoDto = new CreatePedidoCompraDto
            {
                SupplierId    = grupo.Key,
                FornecedorNome = nomeForn,
                DataPrevista  = DateTime.Today.AddDays(7),
                Observacoes   = dto.Observacoes ?? $"Gerado automaticamente — sugestão de compras {DateTime.Today:dd/MM/yyyy}",
                CriadoPor     = dto.CriadoPor,
                Itens         = grupo.Select(i => new CreatePedidoCompraItemDto
                {
                    ProductId    = i.ProductId,
                    ProductName  = i.ProductName,
                    Quantidade   = i.Quantidade,
                    PrecoUnitario = i.PrecoUnitario
                }).ToList()
            };

            var pedido = await _pedidoService.CriarAsync(pedidoDto);
            pedidosGerados.Add(pedido.Id);
        }

        return pedidosGerados;
    }
}
