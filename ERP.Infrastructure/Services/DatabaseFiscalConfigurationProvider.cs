// ── ERP.Infrastructure/Services/DatabaseFiscalConfigurationProvider.cs ──────
using ERP.Application.Helpers;
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Etapa 2 da refatoração fiscal — lê TenantFiscalConfiguration do banco em
/// vez do arquivo local. Mesma interface que o JsonFiscalConfigurationProvider
/// (WPF-only) — troca uma pela outra na injeção de dependência sem tocar em
/// nenhuma linha do FiscalService.
/// </summary>
public class DatabaseFiscalConfigurationProvider : IFiscalConfigurationProvider
{
    private readonly AppDbContext _ctx;
    private readonly IRequestTenant _tenant;

    public DatabaseFiscalConfigurationProvider(AppDbContext ctx, IRequestTenant tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<FiscalConfiguration> ObterConfiguracaoAsync()
    {
        var config = await _ctx.TenantFiscalConfigurations.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == _tenant.TenantId);

        if (config is null)
            return new FiscalConfiguration(); // sem config gravada ainda — token vazio, Focus vai recusar com mensagem clara

        return new FiscalConfiguration
        {
            TokenFocusNfe        = TokenProtector.Desproteger(config.TokenFocusNfeEncriptado),
            UsarAmbienteProducao = config.UsarAmbienteProducao
        };
    }

    public async Task SalvarConfiguracaoAsync(FiscalConfiguration config)
    {
        var existente = await _ctx.TenantFiscalConfigurations
            .FirstOrDefaultAsync(c => c.TenantId == _tenant.TenantId);

        var tokenEncriptado = TokenProtector.Proteger(config.TokenFocusNfe);

        if (existente is null)
        {
            _ctx.TenantFiscalConfigurations.Add(new Domain.Entities.TenantFiscalConfiguration
            {
                TenantId              = _tenant.TenantId,
                TokenFocusNfeEncriptado = tokenEncriptado,
                UsarAmbienteProducao  = config.UsarAmbienteProducao
            });
        }
        else
        {
            existente.TokenFocusNfeEncriptado = tokenEncriptado;
            existente.UsarAmbienteProducao    = config.UsarAmbienteProducao;
        }

        await _ctx.SaveChangesAsync();
    }
}
