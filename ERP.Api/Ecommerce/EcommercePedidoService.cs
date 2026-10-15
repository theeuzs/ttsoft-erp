using System.Security.Cryptography;
using System.Text;
using ERP.Application.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Entities;
using ERP.Domain.Enums;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Api.Ecommerce;

// S{N} FIX: Theuzs e-commerce - lanca no ERP a venda de um pedido do site (marcado como "Pago" no painel do site).
//
// O que o lancamento faz (mesmo desenho do pedido de marketplace em OrderProcessingService):
//  1. Venda (Sale) da loja para o CLIENTE DE REPASSE (o MEI da Theuzs, EcommerceOptions.ClienteRepasseDocumento),
//     Origem = Marketplace (nao exige caixa aberto), forma de pagamento A Prazo.
//  2. Preco de cada item = REPASSE (custo efetivo x FatorRepasse), a mesma conta do catalogo. Por isso NAO usa
//     SaleService.CreateAsync: ele sempre recalcula pelo preco de tabela da loja (GetPrecoParaGrupo).
//  3. Baixa de estoque atomica (BaixarEstoqueAtomicoAsync), na mesma transacao da venda. Produto filho baixa do pai.
//  4. Conta a Receber do repasse no nome do cliente de repasse (vence em 30 dias): e o "quanto a Theuzs deve a loja".
//  5. NAO emite nota fiscal (diferente do marketplace). Nota, se houver, e emitida a mao pelo ERP.
//
// Idempotencia: o Id da venda e derivado do id do pedido do site (hash). Chamar de novo devolve a venda existente
// e cria a Conta a Receber se ela tiver faltado. Estorno devolve o estoque e cancela a conta e a venda.
//
// Tenant: vem de EcommerceOptions.TenantId (a requisicao do site nao tem JWT). E aplicado no IRequestTenant do
// escopo e no AsyncLocal das consultas ANTES de qualquer acesso ao banco (BaixarEstoqueAtomicoAsync filtra por ele).
//
// Diferenca consciente em relacao ao SaleService: SaleItem.Quantity fica na unidade VENDIDA (a do produto do item),
// nao convertida para a unidade do pai. O estorno daqui converte certo (filho devolve no pai x ConversionFactor).
public sealed class EcommercePedidoService
{
    private readonly AppDbContext _ctx;
    private readonly IUnitOfWork _uow;
    private readonly IRequestTenant _tenant;
    private readonly IContaReceberService _contas;
    private readonly IServiceProvider _sp;
    private readonly EcommerceOptions _opt;
    private readonly ILogger<EcommercePedidoService> _log;

    public EcommercePedidoService(AppDbContext ctx, IUnitOfWork uow, IRequestTenant tenant, IContaReceberService contas,
        IServiceProvider sp, IOptions<EcommerceOptions> opt, ILogger<EcommercePedidoService> log)
    {
        _ctx = ctx;
        _uow = uow;
        _tenant = tenant;
        _contas = contas;
        _sp = sp;
        _opt = opt.Value;
        _log = log;
    }

    private sealed record ProdutoVenda(
        Guid Id,
        string? Sku,
        string? Barcode,
        string Nome,
        bool PermiteNegativo,
        decimal CostPrice,
        decimal OriginalCost,
        decimal IpiPercent,
        Guid? PaiId,
        decimal ConversionFactor,
        bool? PaiPermiteNegativo,
        decimal? CostPricePai,
        decimal? OriginalCostPai,
        decimal? IpiPercentPai);

    private sealed record Plano(ProdutoVenda Produto, int Quantidade, decimal RepasseUnitario, Guid EstoqueId, decimal QuantidadeEstoque, bool PermiteNegativo);

    // ---------------------------------------------------------------------------------------------
    // Lancar
    // ---------------------------------------------------------------------------------------------
    public async Task<LancarPedidoResponse> LancarAsync(string pedidoId, string numero, IReadOnlyList<LinhaPedidoEcommerce> linhas, CancellationToken ct)
    {
        UsarTenantDaLoja();
        var vendaId = IdDaVenda(pedidoId);

        var existente = await _ctx.Sales
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => s.Id == vendaId && s.TenantId == _opt.TenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.SaleNumber, s.Total, s.Status, s.CustomerId })
            .FirstOrDefaultAsync(ct);

        if (existente is not null)
        {
            if (existente.Status == SaleStatus.Cancelada)
                throw new EcommercePedidoException(409, $"A venda {existente.SaleNumber} deste pedido foi cancelada no ERP. Se o pedido voltou a valer, lance a venda pelo ERP.");

            var criou = existente.CustomerId is Guid cid && await GarantirContaRepasseAsync(existente.Id, cid, existente.Total, numero, ct);
            return new LancarPedidoResponse(existente.Id, existente.SaleNumber, existente.Total, true, criou);
        }

        var clienteId = await ClienteRepasseAsync(ct);
        var planos = await MontarPlanosAsync(linhas, ct);

        var sale = new Sale
        {
            Id = vendaId,
            TenantId = _opt.TenantId,
            SaleNumber = NumeroVenda(),
            Origem = SaleOrigin.Marketplace,
            CustomerId = clienteId,
            SellerName = "Site Theuzs",
            SaleDate = FusoBrasilHelper.AgoraNoBrasil(),
            Notes = $"Pedido do site Theuzs {numero}. Preco = repasse (custo x {_opt.FatorRepasse:0.00}).",
        };
        foreach (var p in planos)
        {
            sale.Items.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                TenantId = _opt.TenantId,
                SaleId = vendaId,
                ProductId = p.Produto.Id,
                ProductName = p.Produto.Nome,
                Quantity = p.Quantidade,
                UnitPrice = p.RepasseUnitario,
                DiscountPercent = 0,
                TotalItem = Math.Round(p.RepasseUnitario * p.Quantidade, 2, MidpointRounding.AwayFromZero),
            });
        }
        sale.RecalculateTotals();
        sale.Payments.Add(new SalePayment
        {
            Id = Guid.NewGuid(),
            TenantId = _opt.TenantId,
            SaleId = vendaId,
            PaymentMethod = PaymentMethod.APrazo, // rotulo: o financeiro real e a Conta a Receber abaixo
            Amount = sale.Total,
        });

        var alterados = new HashSet<Guid>();
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // Nova tentativa da estrategia de execucao: comeca limpo (a venda pode ter ficado rastreada da tentativa anterior).
            _ctx.ChangeTracker.Clear();
            alterados.Clear();
            await using var tx = await _uow.BeginTransactionAsync();
            try
            {
                foreach (var p in planos)
                {
                    var baixou = await _uow.Products.BaixarEstoqueAtomicoAsync(p.EstoqueId, p.QuantidadeEstoque, p.PermiteNegativo);
                    if (!baixou)
                        throw new EcommercePedidoException(409,
                            $"Estoque insuficiente no ERP para '{p.Produto.Nome}' (precisa de {p.QuantidadeEstoque:0.##}). Confira o estoque e tente de novo.");
                    alterados.Add(p.EstoqueId);
                }

                _ctx.Sales.Add(sale);
                await _uow.CommitAsync();
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        _log.LogInformation("Ecommerce: pedido {Numero} lancado como venda {Venda} ({Itens} itens, repasse {Total})",
            numero, sale.SaleNumber, planos.Count, sale.Total);

        var contaCriada = await GarantirContaRepasseAsync(vendaId, clienteId, sale.Total, numero, ct);
        await SincronizarEstoqueAsync(alterados);

        return new LancarPedidoResponse(vendaId, sale.SaleNumber, sale.Total, false, contaCriada);
    }

    private async Task<List<Plano>> MontarPlanosAsync(IReadOnlyList<LinhaPedidoEcommerce> linhas, CancellationToken ct)
    {
        var skus = linhas.Select(l => l.Sku.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var produtos = await _ctx.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.TenantId == _opt.TenantId
                        && !p.IsDeleted
                        && p.IsActive
                        && ((p.SKU != null && skus.Contains(p.SKU))
                            || (p.Barcode != null && skus.Contains(p.Barcode))))
            .Select(p => new ProdutoVenda(
                p.Id,
                p.SKU,
                p.Barcode,
                p.Name,
                p.AllowNegativeStock,
                p.CostPrice,
                p.OriginalCost,
                p.IpiPercent,
                p.ParentProductId != null && p.ConversionFactor > 0 ? p.ParentProductId : null,
                p.ConversionFactor,
                p.ParentProduct != null && !p.ParentProduct.IsDeleted ? (bool?)p.ParentProduct.AllowNegativeStock : null,
                p.ParentProduct != null ? (decimal?)p.ParentProduct.CostPrice : null,
                p.ParentProduct != null ? (decimal?)p.ParentProduct.OriginalCost : null,
                p.ParentProduct != null ? (decimal?)p.ParentProduct.IpiPercent : null))
            .ToListAsync(ct);

        // Mesma regra do catalogo: SKU tem prioridade sobre o codigo de barras.
        var porSku = new Dictionary<string, ProdutoVenda>(StringComparer.OrdinalIgnoreCase);
        var porBarcode = new Dictionary<string, ProdutoVenda>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in produtos)
        {
            if (!string.IsNullOrWhiteSpace(p.Sku)) porSku.TryAdd(p.Sku.Trim(), p);
            if (!string.IsNullOrWhiteSpace(p.Barcode)) porBarcode.TryAdd(p.Barcode.Trim(), p);
        }

        var planos = new List<Plano>(linhas.Count);
        foreach (var linha in linhas)
        {
            var sku = linha.Sku.Trim();
            if (!porSku.TryGetValue(sku, out var p) && !porBarcode.TryGetValue(sku, out p))
                throw new EcommercePedidoException(409, $"SKU {sku} nao encontrado no ERP (inativo ou excluido?).");

            var ehFilho = p.PaiId is not null;
            if (ehFilho && p.PaiPermiteNegativo is null)
                throw new EcommercePedidoException(409, $"'{p.Nome}' e produto filho sem produto pai valido no ERP.");

            var custo = EcommerceCatalogService.CustoEfetivo(p.CostPrice, p.OriginalCost, p.IpiPercent, ehFilho, p.ConversionFactor,
                p.CostPricePai, p.OriginalCostPai, p.IpiPercentPai);
            if (custo <= 0)
                throw new EcommercePedidoException(409, $"'{p.Nome}' esta sem custo no ERP. Cadastre o custo antes de lancar.");

            var repasse = Math.Round(custo * _opt.FatorRepasse, 2, MidpointRounding.AwayFromZero);
            planos.Add(ehFilho
                ? new Plano(p, linha.Quantidade, repasse, p.PaiId!.Value, linha.Quantidade * p.ConversionFactor, p.PaiPermiteNegativo!.Value)
                : new Plano(p, linha.Quantidade, repasse, p.Id, linha.Quantidade, p.PermiteNegativo));
        }
        return planos;
    }

    // ---------------------------------------------------------------------------------------------
    // Estornar (pedido cancelado ou voltou para antes de "Pago" no painel do site)
    // ---------------------------------------------------------------------------------------------
    public async Task<EstornarPedidoResponse> EstornarAsync(string pedidoId, string motivo, CancellationToken ct)
    {
        UsarTenantDaLoja();
        var vendaId = IdDaVenda(pedidoId);

        var sale = await _ctx.Sales
            .IgnoreQueryFilters()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == vendaId && s.TenantId == _opt.TenantId && !s.IsDeleted, ct);

        if (sale is null) return new EstornarPedidoResponse(false, "Este pedido nao tem venda no ERP. Nada a estornar.");
        if (sale.Status == SaleStatus.Cancelada) return new EstornarPedidoResponse(true, $"A venda {sale.SaleNumber} ja estava cancelada.");
        if (sale.Status == SaleStatus.NotaEmitida)
            throw new EcommercePedidoException(409, $"A venda {sale.SaleNumber} tem nota fiscal emitida. Cancele a nota e a venda pelo ERP.");

        var contas = await _ctx.ContasReceber
            .IgnoreQueryFilters()
            .Where(c => c.SaleId == vendaId && c.TenantId == _opt.TenantId && !c.IsDeleted && c.Status != "Cancelado")
            .ToListAsync(ct);
        if (contas.Any(c => c.ValorRecebido > 0 || c.Status != "Pendente"))
            throw new EcommercePedidoException(409, $"O repasse da venda {sale.SaleNumber} ja teve baixa no ERP. Estorne pelo ERP (Contas a Receber e venda).");

        var ids = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var estrutura = await _ctx.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.TenantId == _opt.TenantId)
            .Select(p => new { p.Id, PaiId = p.ParentProductId != null && p.ConversionFactor > 0 ? p.ParentProductId : null, p.ConversionFactor })
            .ToDictionaryAsync(p => p.Id, ct);

        var devolucoes = sale.Items
            .Select(i => estrutura.TryGetValue(i.ProductId, out var e) && e.PaiId is Guid pai
                ? (EstoqueId: pai, Quantidade: i.Quantity * e.ConversionFactor)
                : (EstoqueId: i.ProductId, Quantidade: i.Quantity))
            .GroupBy(d => d.EstoqueId)
            .Select(g => (EstoqueId: g.Key, Quantidade: g.Sum(x => x.Quantidade)))
            .ToList();

        var agora = DateTime.UtcNow;
        var texto = string.IsNullOrWhiteSpace(motivo) ? "Pedido cancelado no site Theuzs" : motivo.Trim();
        foreach (var c in contas)
        {
            c.Status = "Cancelado";
            c.MotivoCancelamento = texto;
            c.UpdatedAt = agora;
        }
        sale.Cancel(texto);

        await _uow.ExecuteInTransactionAsync(async () =>
        {
            await using var tx = await _uow.BeginTransactionAsync();
            try
            {
                foreach (var d in devolucoes)
                    await _ctx.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE Products SET Stock = Stock + {d.Quantidade} WHERE Id = {d.EstoqueId} AND TenantId = {_opt.TenantId}", ct);

                await _uow.CommitAsync();
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });

        _log.LogInformation("Ecommerce: venda {Venda} estornada (pedido do site {PedidoId})", sale.SaleNumber, pedidoId);
        await SincronizarEstoqueAsync(devolucoes.Select(d => d.EstoqueId));
        return new EstornarPedidoResponse(true, $"Venda {sale.SaleNumber} cancelada, estoque devolvido e repasse cancelado.");
    }

    // ---------------------------------------------------------------------------------------------
    // Apoio
    // ---------------------------------------------------------------------------------------------
    private void UsarTenantDaLoja()
    {
        _tenant.TenantId = _opt.TenantId;
        if (string.IsNullOrEmpty(_tenant.UserName)) _tenant.UserName = "Site Theuzs";
        AppDbContext.SetQueryTenantId(_opt.TenantId);
    }

    /// <summary>Id da venda derivado do pedido: o mesmo pedido sempre gera o mesmo Id (nao duplica em nova tentativa).</summary>
    private Guid IdDaVenda(string pedidoId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"theuzs-site:{_opt.TenantId:N}:{pedidoId}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string NumeroVenda() => $"VND{DateTime.UtcNow:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";

    private static string SoDigitos(string? s) => new((s ?? string.Empty).Where(char.IsDigit).ToArray());

    private async Task<Guid> ClienteRepasseAsync(CancellationToken ct)
    {
        var doc = SoDigitos(_opt.ClienteRepasseDocumento);
        if (doc.Length is not (11 or 14))
            throw new EcommercePedidoException(503, "Configure Ecommerce__ClienteRepasseDocumento (CNPJ do MEI da Theuzs) na API.");

        var id = await _ctx.Customers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(c => c.TenantId == _opt.TenantId && !c.IsDeleted && c.Document != null
                        && c.Document.Replace(".", "").Replace("/", "").Replace("-", "").Replace(" ", "") == doc)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        return id ?? throw new EcommercePedidoException(409, "Cliente de repasse (CNPJ do MEI da Theuzs) nao encontrado nos clientes da loja.");
    }

    /// <summary>Cria a Conta a Receber do repasse se ainda nao existir. Nunca derruba o lancamento: falha vira log e nova tentativa depois.</summary>
    private async Task<bool> GarantirContaRepasseAsync(Guid vendaId, Guid clienteId, decimal valor, string numero, CancellationToken ct)
    {
        try
        {
            var existe = await _ctx.ContasReceber
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(c => c.SaleId == vendaId && c.TenantId == _opt.TenantId && !c.IsDeleted, ct);
            if (existe) return true;

            await _contas.GerarContaAPrazoAsync(clienteId, vendaId, valor, $"Repasse do site Theuzs - Pedido {numero}");
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Ecommerce: venda {VendaId} lancada, mas a Conta a Receber do repasse falhou. Lance de novo pelo painel do site.", vendaId);
            return false;
        }
    }

    /// <summary>Mesmo comportamento do SaleService: avisa os canais (Mercado Livre etc.) que o estoque mudou. Best-effort.</summary>
    private async Task SincronizarEstoqueAsync(IEnumerable<Guid> produtos)
    {
        var sync = _sp.GetService<IEstoqueSyncService>();
        if (sync is null) return;
        foreach (var id in produtos.Distinct())
        {
            try { await sync.SincronizarProdutoAsync(id); }
            catch (Exception ex) { _log.LogWarning(ex, "Ecommerce: sincronizacao de estoque falhou para {ProdutoId}", id); }
        }
    }
}

/// <summary>Erro esperado do lancamento (vira resposta HTTP com a mensagem para o painel do site).</summary>
public sealed class EcommercePedidoException : Exception
{
    public int Status { get; }
    public EcommercePedidoException(int status, string mensagem) : base(mensagem) => Status = status;
}
