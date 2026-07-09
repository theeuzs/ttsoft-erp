// TENANT FIX: AppDbContext.GetGlobalTenantId() → IRequestTenant.TenantId.
//        Antes: GetGlobalTenantId() retorna Guid.Empty na API (só é setado pelo
//        WPF no login). CriarAsync gravava a transferência com TenantId=Guid.Empty
//        — como TransferenciaEstoque tem HasQueryFilter, a transferência recém-criada
//        desaparecia da visão de quem acabou de criá-la. Mesmo problema em
//        AjustarEstoqueBranchAsync ao criar um ProductBranchStock novo.
//        _tenant é injetado no construtor (escopo da requisição HTTP ambiente),
//        não resolvido de dentro do `scope` de _sp.CreateScope() — mesmo cuidado
//        já aplicado em NfseEmissionService.
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace ERP.Infrastructure.Services;

public interface ITransferenciaService
{
    Task<TransferenciaEstoque> CriarAsync(CriarTransferenciaDto dto);
    Task<TransferenciaEstoque> ConfirmarAsync(Guid id, string operador);
    Task CancelarAsync(Guid id, string motivo);
    Task<IEnumerable<TransferenciaEstoque>> GetByFilialAsync(Guid branchId);
    Task<IEnumerable<Branch>> GetFilialAsync();
}

public class CriarTransferenciaDto
{
    public Guid   OrigemId    { get; set; }
    public Guid   DestinoId   { get; set; }
    public string OperadorNome { get; set; } = string.Empty;
    public string? Observacao  { get; set; }
    public List<(Guid ProductId, decimal Quantidade)> Itens { get; set; } = new();
}

public class TransferenciaService : ITransferenciaService
{
    private readonly IServiceProvider _sp;
    private readonly IRequestTenant   _tenant;

    public TransferenciaService(IServiceProvider sp, IRequestTenant tenant)
    {
        _sp     = sp;
        _tenant = tenant;
    }

    public async Task<TransferenciaEstoque> CriarAsync(CriarTransferenciaDto dto)
    {
        if (dto.OrigemId == dto.DestinoId)
            throw new InvalidOperationException("Origem e destino não podem ser iguais.");
        if (!dto.Itens.Any())
            throw new InvalidOperationException("A transferência deve ter ao menos um item.");

        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tenantId = _tenant.TenantId;

        var transferencia = new TransferenciaEstoque
        {
            TenantId      = tenantId,
            OrigemId      = dto.OrigemId,
            DestinoId     = dto.DestinoId,
            OperadorNome  = dto.OperadorNome,
            Observacao    = dto.Observacao,
            Status        = StatusTransferencia.Rascunho,
            DataTransferencia = DateTime.Now
        };

        foreach (var (prodId, qtd) in dto.Itens)
        {
            transferencia.Itens.Add(new TransferenciaItem
            {
                TenantId   = tenantId,
                ProductId  = prodId,
                Quantidade = qtd
            });
        }

        ctx.Transferencias.Add(transferencia);
        await ctx.SaveChangesAsync();
        return transferencia;
    }

    public async Task<TransferenciaEstoque> ConfirmarAsync(Guid id, string operador)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var transf = await ctx.Transferencias
            .Include(t => t.Itens)
            .AsTracking()
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException("Transferência não encontrada.");

        if (transf.Status != StatusTransferencia.Rascunho &&
            transf.Status != StatusTransferencia.Enviada)
            throw new InvalidOperationException("Transferência não pode ser confirmada no status atual.");

        // Atualiza estoque por filial (ProductBranchStock)
        var tenantId = _tenant.TenantId;
        foreach (var item in transf.Itens)
        {
            // Debita origem
            await AjustarEstoqueBranchAsync(ctx, tenantId, item.ProductId, transf.OrigemId, -item.Quantidade);
            // Credita destino
            await AjustarEstoqueBranchAsync(ctx, tenantId, item.ProductId, transf.DestinoId, item.Quantidade);

            Log.Information("Transferência {Id}: produto {Prod} {Qtd} un de {Orig} → {Dest}",
                id, item.ProductId, item.Quantidade, transf.OrigemId, transf.DestinoId);
        }

        transf.Status      = StatusTransferencia.Confirmada;
        transf.UpdatedAt   = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        return transf;
    }

    public async Task CancelarAsync(Guid id, string motivo)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var transf = await ctx.Transferencias.AsTracking()
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new KeyNotFoundException("Transferência não encontrada.");

        if (transf.Status == StatusTransferencia.Confirmada)
            throw new InvalidOperationException("Transferência confirmada não pode ser cancelada.");

        transf.Status    = StatusTransferencia.Cancelada;
        transf.Observacao = $"{transf.Observacao}\n[CANCELADA: {motivo}]";
        transf.UpdatedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();
    }

    public async Task<IEnumerable<TransferenciaEstoque>> GetByFilialAsync(Guid branchId)
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.Transferencias
            .Include(t => t.Origem)
            .Include(t => t.Destino)
            .Include(t => t.Itens).ThenInclude(i => i.Product)
            .Where(t => t.OrigemId == branchId || t.DestinoId == branchId)
            .OrderByDescending(t => t.DataTransferencia)
            .ToListAsync();
    }

    public async Task<IEnumerable<Branch>> GetFilialAsync()
    {
        using var scope = _sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await ctx.Branches.Where(b => b.IsActive).OrderBy(b => b.Name).ToListAsync();
    }

    private static async Task AjustarEstoqueBranchAsync(
        AppDbContext ctx, Guid tenantId, Guid productId, Guid branchId, decimal delta)
    {
        var stock = await ctx.ProductBranchStocks
            .AsTracking()
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == branchId);

        if (stock == null)
        {
            stock = new ProductBranchStock
            {
                TenantId  = tenantId,
                ProductId = productId,
                BranchId  = branchId,
                Quantity  = delta
            };
            ctx.ProductBranchStocks.Add(stock);
        }
        else
        {
            stock.Quantity += delta;
        }
    }
}