// ── ERP.WPF/Services/JsonFiscalConfigurationProvider.cs ─────────────────────
using ERP.Application.Interfaces;

namespace ERP.WPF.Services;

/// <summary>
/// Implementação que só existe no WPF — lê o arquivo local (config_recibo.json)
/// exatamente como já acontecia antes da refatoração. Uma futura
/// DatabaseFiscalConfigurationProvider troca só isso, registrada tanto na API
/// quanto no WPF, sem tocar em nenhuma linha do FiscalService.
/// </summary>
public class JsonFiscalConfigurationProvider : IFiscalConfigurationProvider
{
    public Task<FiscalConfiguration> ObterConfiguracaoAsync()
    {
        var config = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
        return Task.FromResult(new FiscalConfiguration
        {
            // Achado (21/08) — Focus tem token separado por ambiente por
            // empresa; resolve aqui sozinho, quem consome não precisa saber.
            TokenFocusNfe            = config.UsarAmbienteProducao
                ? config.TokenFocusNfeProducao
                : config.TokenFocusNfeHomologacao,
            UsarAmbienteProducao     = config.UsarAmbienteProducao,
            // Achado (20/08) — faltava aqui, mesmo já existindo no arquivo
            // local desde a correção anterior. MD-e (NfeRecebidaService) e
            // qualquer outro consumidor de IFiscalConfigurationProvider
            // nunca recebiam o CNPJ, mesmo com ele salvo certinho.
            Cnpj                     = config.Cnpj,
            TokenFocusNfeProducao    = config.TokenFocusNfeProducao,
            TokenFocusNfeHomologacao = config.TokenFocusNfeHomologacao,
        });
    }

    public Task SalvarConfiguracaoAsync(FiscalConfiguration config)
    {
        var reciboConfig = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
        reciboConfig.TokenFocusNfeProducao    = config.TokenFocusNfeProducao ?? string.Empty;
        reciboConfig.TokenFocusNfeHomologacao = config.TokenFocusNfeHomologacao ?? string.Empty;
        reciboConfig.UsarAmbienteProducao     = config.UsarAmbienteProducao;
        reciboConfig.Cnpj                     = config.Cnpj;
        ERP.WPF.Helpers.ConfiguracaoService.Salvar(reciboConfig);
        return Task.CompletedTask;
    }
}