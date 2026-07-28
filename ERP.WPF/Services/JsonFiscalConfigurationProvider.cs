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
            TokenFocusNfe        = config.TokenFocusNfe,
            UsarAmbienteProducao = config.UsarAmbienteProducao
        });
    }

    public Task SalvarConfiguracaoAsync(FiscalConfiguration config)
    {
        // Carrega o arquivo inteiro (recibo, logo, PIX, etc.) e só atualiza os
        // dois campos fiscais — não sobrescreve o resto da configuração.
        var reciboConfig = ERP.WPF.Helpers.ConfiguracaoService.Carregar();
        reciboConfig.TokenFocusNfe        = config.TokenFocusNfe;
        reciboConfig.UsarAmbienteProducao = config.UsarAmbienteProducao;
        ERP.WPF.Helpers.ConfiguracaoService.Salvar(reciboConfig);
        return Task.CompletedTask;
    }
}
