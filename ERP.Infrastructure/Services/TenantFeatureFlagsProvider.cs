// ── ERP.Infrastructure/Services/TenantFeatureFlagsProvider.cs ──────────────
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>Mesmo padrão do DatabaseFiscalConfigurationProvider — uma linha
/// por tenant, lê/grava direto no AppDbContext do tenant atual. Funciona
/// tanto rodando dentro do WPF (DI local) quanto na API, sem mudar nada.</summary>
public class TenantFeatureFlagsProvider : ITenantFeatureFlagsProvider
{
    private readonly AppDbContext _ctx;
    private readonly IRequestTenant _tenant;

    public TenantFeatureFlagsProvider(AppDbContext ctx, IRequestTenant tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<TenantFeatureFlagsDto> ObterAsync()
    {
        var flags = await _ctx.TenantFeatureFlags.AsNoTracking()
            .FirstOrDefaultAsync(f => f.TenantId == _tenant.TenantId);

        // Sem linha ainda = as duas features desligadas por padrão — nunca
        // "liga sozinho" pra um tenant que nunca configurou nada.
        if (flags is null)
            return new TenantFeatureFlagsDto { MetasVendasHabilitado = false, PontosFidelidadeHabilitado = false };

        return new TenantFeatureFlagsDto
        {
            MetasVendasHabilitado      = flags.MetasVendasHabilitado,
            PontosFidelidadeHabilitado = flags.PontosFidelidadeHabilitado
        };
    }

    public async Task SalvarAsync(TenantFeatureFlagsDto dto)
    {
        // Achado (21/08, S28) — sem AsTracking(), mutação numa entidade já
        // existente não persiste (AppDbContext roda NoTracking global).
        var existente = await _ctx.TenantFeatureFlags.AsTracking()
            .FirstOrDefaultAsync(f => f.TenantId == _tenant.TenantId);

        if (existente is null)
        {
            _ctx.TenantFeatureFlags.Add(new TenantFeatureFlags
            {
                TenantId                   = _tenant.TenantId,
                MetasVendasHabilitado      = dto.MetasVendasHabilitado,
                PontosFidelidadeHabilitado = dto.PontosFidelidadeHabilitado
            });
        }
        else
        {
            existente.MetasVendasHabilitado      = dto.MetasVendasHabilitado;
            existente.PontosFidelidadeHabilitado = dto.PontosFidelidadeHabilitado;
        }

        await _ctx.SaveChangesAsync();
    }
}
