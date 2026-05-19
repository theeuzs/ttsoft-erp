// ── ERP.Infrastructure/Services/NotasFiscaisService.cs ───────────────────────
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Serviço de consulta de notas fiscais emitidas.
/// Isola o acesso ao AppDbContext que estava direto no NotasFiscaisController.
///
/// NfseEmitida não possui HasQueryFilter no AppDbContext, portanto o filtro
/// de TenantId é aplicado manualmente no WHERE — mesmo padrão do AuditLog.
/// </summary>
public class NotasFiscaisService : INotasFiscaisService
{
    private readonly AppDbContext   _ctx;
    private readonly IRequestTenant _tenant;

    public NotasFiscaisService(AppDbContext ctx, IRequestTenant tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<PagedResult<NotaFiscalDto>> GetAllAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(page, 1);

        var tenantId = _tenant.TenantId;

        IQueryable<NfseEmitida> query = _ctx.NfseEmitidas
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.DataEmissao)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotaFiscalDto
            {
                Id               = n.Id,
                NumeroNfse       = n.NumeroNfse,
                ReferenciaNfse   = n.ReferenciaNfse,
                DataEmissao      = n.DataEmissao,
                Status           = n.Status.ToString(),
                TomadorNome      = n.TomadorNome,
                TomadorCpfCnpj   = n.TomadorCpfCnpj,
                DescricaoServico = n.DescricaoServico,
                ValorServico     = n.ValorServico,
                ValorISS         = n.ValorISS,
                ValorLiquido     = n.ValorLiquido,
                UrlDanfse        = n.UrlDanfse,
                MensagemErro     = n.MensagemErro,
                VendaId          = n.VendaId
            })
            .ToListAsync(ct);

        return new PagedResult<NotaFiscalDto>
        {
            Items      = items,
            TotalItems = total,
            Page       = page,
            PageSize   = pageSize
        };
    }
}
