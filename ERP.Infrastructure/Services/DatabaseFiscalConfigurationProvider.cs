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
            return new FiscalConfiguration();

        string? tokenHomologacao = string.IsNullOrWhiteSpace(config.TokenFocusNfeHomologacaoEncriptado)
            ? null : TokenProtector.Desproteger(config.TokenFocusNfeHomologacaoEncriptado);
        string tokenProducao = TokenProtector.Desproteger(config.TokenFocusNfeProducaoEncriptado);

        return new FiscalConfiguration
        {
            // Achado (21/08) — Focus tem token separado por ambiente por
            // empresa; resolve aqui sozinho, quem consome não precisa saber.
            TokenFocusNfe            = config.UsarAmbienteProducao ? tokenProducao : (tokenHomologacao ?? string.Empty),
            UsarAmbienteProducao     = config.UsarAmbienteProducao,
            Cnpj                     = config.Cnpj,
            TokenFocusNfeProducao    = tokenProducao,
            TokenFocusNfeHomologacao = tokenHomologacao,
        };
    }

    public async Task SalvarConfiguracaoAsync(FiscalConfiguration config)
    {
        // Achado (21/08) — escapou da caçada sistêmica anterior porque o
        // .FirstOrDefaultAsync fica na linha seguinte (busca por regex de
        // linha única não pegou). Mesmo bug: sem AsTracking(), as mutações
        // abaixo não seriam persistidas (AppDbContext roda NoTracking global).
        var existente = await _ctx.TenantFiscalConfigurations.AsTracking()
            .FirstOrDefaultAsync(c => c.TenantId == _tenant.TenantId);

        var tokenProducaoEncriptado = TokenProtector.Proteger(config.TokenFocusNfeProducao ?? string.Empty);
        var tokenHomologacaoEncriptado = string.IsNullOrWhiteSpace(config.TokenFocusNfeHomologacao)
            ? null : TokenProtector.Proteger(config.TokenFocusNfeHomologacao);

        if (existente is null)
        {
            _ctx.TenantFiscalConfigurations.Add(new Domain.Entities.TenantFiscalConfiguration
            {
                TenantId                           = _tenant.TenantId,
                TokenFocusNfeProducaoEncriptado    = tokenProducaoEncriptado,
                TokenFocusNfeHomologacaoEncriptado = tokenHomologacaoEncriptado,
                UsarAmbienteProducao               = config.UsarAmbienteProducao,
                Cnpj                               = config.Cnpj
            });
        }
        else
        {
            existente.TokenFocusNfeProducaoEncriptado    = tokenProducaoEncriptado;
            existente.TokenFocusNfeHomologacaoEncriptado = tokenHomologacaoEncriptado;
            existente.UsarAmbienteProducao               = config.UsarAmbienteProducao;
            existente.Cnpj                               = config.Cnpj;
        }

        await _ctx.SaveChangesAsync();
    }
}