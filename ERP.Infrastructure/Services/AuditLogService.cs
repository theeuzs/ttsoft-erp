// ── ERP.Infrastructure/Services/AuditLogService.cs ───────────────────────────
// Sprint 2A: AuditLogService movido de ERP.Application para ERP.Infrastructure.
//
// Motivo: IAuditLogService agora precisa de GetPagedAsync com OrderByDescending,
// que requer AppDbContext direto. ERP.Application não referencia ERP.Persistence,
// então a única forma de ter IQueryable correto é na camada Infrastructure.
//
// A implementação antiga (em SupplierCategoryBrandService.cs) pode ser removida
// ou deixada como stub que delega aqui — basta atualizar o registro de DI.
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implementação de IAuditLogService com suporte completo a IQueryable paginado.
///
/// AuditLog não possui HasQueryFilter no AppDbContext — filtro de TenantId
/// aplicado manualmente no WHERE.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext   _ctx;
    private readonly IRequestTenant _tenant;
    private readonly IUnitOfWork    _uow;

    public AuditLogService(AppDbContext ctx, IRequestTenant tenant, IUnitOfWork uow)
    {
        _ctx    = ctx;
        _tenant = tenant;
        _uow    = uow;
    }

    /// <summary>
    /// Busca por período + texto livre.
    /// Mantido para compatibilidade com WPF / AuditLogViewModel que usa este método.
    /// FindAsync aplica o WHERE no banco — só OrderBy e Take ficam em memória,
    /// o que é aceitável dado que o filtro de data já limita o conjunto.
    /// </summary>
    public async Task<IEnumerable<AuditLogDto>> SearchAsync(
        DateTime from, DateTime to,
        string? busca = null,
        int     take  = 100)
    {
        var fim  = to.Date.AddDays(1).AddTicks(-1);
        var logs = await _uow.AuditLogs.FindAsync(x =>
            x.Timestamp >= from.Date && x.Timestamp <= fim &&
            (busca == null ||
             (x.UserName   != null && x.UserName.Contains(busca))   ||
             (x.EntityType != null && x.EntityType.Contains(busca)) ||
             (x.Action     != null && x.Action.Contains(busca))));

        return logs
            .OrderByDescending(x => x.Timestamp)
            .Take(take)
            .Select(Map);
    }

    /// <summary>
    /// Versão paginada usada pelo AuditoriaController.
    /// Todo o trabalho (filtro + COUNT + ORDER BY DESC + OFFSET/FETCH) é feito no banco.
    /// </summary>
    public async Task<PagedResult<AuditLogDto>> GetPagedAsync(
        string?   usuario  = null,
        string?   acao     = null,
        DateTime? de       = null,
        DateTime? ate      = null,
        int       pagina   = 1,
        int       tam      = 50,
        CancellationToken ct = default)
    {
        tam    = Math.Clamp(tam, 1, 200);
        pagina = Math.Max(pagina, 1);

        var tenantId = _tenant.TenantId;

        IQueryable<AuditLog> query = _ctx.AuditLogs
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(usuario))
            query = query.Where(l => l.UserName != null && l.UserName.Contains(usuario));

        if (!string.IsNullOrWhiteSpace(acao))
            query = query.Where(l => l.Action != null && l.Action.Contains(acao));

        if (de.HasValue)
            query = query.Where(l => l.Timestamp >= de.Value.Date);

        if (ate.HasValue)
            query = query.Where(l => l.Timestamp < ate.Value.Date.AddDays(1));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((pagina - 1) * tam)
            .Take(tam)
            .Select(l => Map(l))
            .ToListAsync(ct);

        return new PagedResult<AuditLogDto>
        {
            Items      = items,
            TotalItems = total,
            Page       = pagina,
            PageSize   = tam
        };
    }

    private static AuditLogDto Map(AuditLog l) => new()
    {
        Id          = l.Id,
        UserName    = l.UserName,
        Action      = l.Action,
        EntityType  = l.EntityType,
        EntityId    = l.EntityId,
        Timestamp   = l.Timestamp,
        MachineName = l.MachineName,
        OldValues   = l.OldValues,
        NewValues   = l.NewValues
    };
}
