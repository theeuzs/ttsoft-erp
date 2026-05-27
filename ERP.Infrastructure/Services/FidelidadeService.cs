using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public class FidelidadeService : IFidelidadeService
{
    private readonly AppDbContext   _db;
    private readonly IRequestTenant _tenant;

    // ── FASE 0 FIX: injetar IRequestTenant do mesmo scope da requisição ──────
    // Antes: FidelidadeService não filtrava por TenantId, qualquer usuário
    // autenticado conseguia ver/alterar pontos de clientes de outros tenants.
    // Agora: HasQueryFilter em PontosFidelidade filtra por TenantId automaticamente
    // (configurado em AppDbContext.OnModelCreating após a correção).
    public FidelidadeService(AppDbContext db, IRequestTenant tenant)
    {
        _db     = db;
        _tenant = tenant;
    }

    // Regras de negócio (podem virar configuração futura)
    private const decimal RealPorPonto  = 1m;     // R$ 1,00 = 1 ponto
    private const decimal ValorPorPonto = 0.01m;  // 1 ponto = R$ 0,01 de desconto

    public async Task<int> GetSaldoAsync(Guid customerId)
    {
        // HasQueryFilter em PontosFidelidade já filtra por TenantId.
        // Se o cliente não é deste tenant, retorna 0 (sem vazar dado).
        var creditos = await _db.Set<PontosFidelidade>()
            .Where(p => p.CustomerId == customerId && p.Tipo == "Credito")
            .SumAsync(p => (int?)p.Pontos) ?? 0;

        var debitos = await _db.Set<PontosFidelidade>()
            .Where(p => p.CustomerId == customerId && p.Tipo == "Debito")
            .SumAsync(p => (int?)p.Pontos) ?? 0;

        return Math.Max(0, creditos - debitos);
    }

    public async Task AcumularPontosAsync(Guid customerId, Guid saleId, decimal totalVenda)
    {
        var pontos = (int)Math.Floor(totalVenda / RealPorPonto);
        if (pontos <= 0) return;

        _db.Set<PontosFidelidade>().Add(new PontosFidelidade
        {
            CustomerId = customerId,
            SaleId     = saleId,
            Tipo       = "Credito",
            Pontos     = pontos,
            Descricao  = $"Compra — venda #{saleId.ToString()[..8].ToUpper()}",
            Data       = DateTime.UtcNow
            // TenantId é preenchido por PreencherTenantIdEUpdatedAt via GetTenantId()
        });

        await _db.SaveChangesAsync();
    }

    public async Task<decimal> ResgatarPontosAsync(Guid customerId, int pontos, string descricao = "Resgate PDV")
    {
        var saldo = await GetSaldoAsync(customerId);
        if (pontos > saldo)
            throw new InvalidOperationException($"Saldo insuficiente: {saldo} pts disponíveis, {pontos} pts solicitados.");

        _db.Set<PontosFidelidade>().Add(new PontosFidelidade
        {
            CustomerId = customerId,
            Tipo       = "Debito",
            Pontos     = pontos,
            Descricao  = descricao,
            Data       = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return pontos * ValorPorPonto;
    }

    public async Task<List<MovimentoPontosDto>> GetHistoricoAsync(Guid customerId, int pagina = 1, int pageSize = 20)
    {
        // HasQueryFilter já garante que só pontos deste tenant são retornados
        return await _db.Set<PontosFidelidade>()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.Data)
            .Skip((pagina - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new MovimentoPontosDto(
                p.Id,
                p.Tipo,
                p.Pontos,
                p.Descricao,
                p.Data,
                p.Sale != null ? p.Sale.SaleNumber : null
            ))
            .ToListAsync();
    }
}
