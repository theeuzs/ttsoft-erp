// ERP.Infrastructure/Repositories/Repository.cs
// ══════════════════════════════════════════════════════════════════════════════
// CORREÇÃO DEFINITIVA — ChangeTracker + Auditoria no EF Core 8
//
// O problema: UseQueryTrackingBehavior(NoTracking) no App.xaml.cs fazia o
// FindAsync retornar entidades DETACHED. O AutoMapper mutava um fantasma,
// o ChangeTracker não detectava nada, e SaveChangesAsync gerava ZERO SQL.
//
// A solução: Separar explicitamente os caminhos de LEITURA (NoTracking)
// e ESCRITA (AsTracking), sem depender do comportamento implícito do FindAsync.
// ══════════════════════════════════════════════════════════════════════════════
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ERP.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _ctx;
    protected readonly DbSet<T> _set;

    public Repository(AppDbContext ctx)
    {
        _ctx = ctx;
        _set = ctx.Set<T>();
    }

    // ── LEITURA PURA (para exibição, relatórios, PDV) ────────────────────
    // AsNoTracking explícito — mesmo com NoTracking global, documentar a
    // intenção evita surpresas se alguém mudar a config no futuro.
    public virtual async Task<T?> GetByIdAsync(Guid id)
        => await _set
            .AsNoTracking()
            .FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);

    // ── LEITURA PARA ESCRITA (Update + Auditoria) ────────────────────────
    // AsTracking() SOBRESCREVE o NoTracking global e garante que:
    //   1. Entry.State == Unchanged com OriginalValues capturados do banco
    //   2. Qualquer mutação via AutoMapper é detectada pelo ChangeTracker
    //   3. SaveChangesAsync gera UPDATE apenas dos campos alterados
    //   4. GerarLogsAuditoria() consegue comparar antes × depois
    //
    // ⚠️  NÃO usar FindAsync aqui — no EF Core 8 com NoTracking global,
    //      FindAsync respeita o QueryTrackingBehavior e retorna Detached
    //      quando a entidade não está no cache local (limpo pelo Clear()).
    public virtual async Task<T?> GetByIdTrackedAsync(Guid id)
        => await _set
            .AsTracking()
            .FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);

    // Com NoTracking explícito — vai direto no SQL Server (dados frescos entre PCs)
    public virtual async Task<IEnumerable<T>> GetAllAsync()
        => await _set.AsNoTracking().ToListAsync();

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => await _set.AsNoTracking().Where(predicate).ToListAsync();

    public async Task<(IEnumerable<T> Items, int Total)> GetPagedAsync(
        int page, int pageSize,
        Expression<Func<T, bool>>? filter = null,
        Expression<Func<T, object>>? orderBy = null)
    {
        IQueryable<T> query = _set.AsNoTracking();
        if (filter != null) query = query.Where(filter);
        int total = await query.CountAsync();
        if (orderBy != null) query = query.OrderBy(orderBy);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task AddAsync(T entity) => await _set.AddAsync(entity);

    // ⚠️  Update() marca TODAS as propriedades como Modified e esmaga
    //      os OriginalValues. Usar SOMENTE para soft-delete ou cenários
    //      onde a auditoria granular não importa.
    public void Update(T entity) => _set.Update(entity);
    public void Remove(T entity) => _set.Remove(entity);

    /// <summary>
    /// Atualiza CurrentValues mantendo OriginalValues para auditoria correta.
    /// Aceita entidade ou DTO — o EF extrai os campos com nomes coincidentes.
    /// </summary>
    public void SetValues(T existente, object novosValores)
        => _ctx.Entry(existente).CurrentValues.SetValues(novosValores);
}