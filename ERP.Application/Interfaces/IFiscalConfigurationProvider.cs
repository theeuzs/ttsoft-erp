// ── ERP.Application/Interfaces/IFiscalConfigurationProvider.cs ─────────────
namespace ERP.Application.Interfaces;

/// <summary>Só os dois campos que a emissão fiscal de verdade consome hoje —
/// nada de CSC/Série especulativos: o Focus NFe cuida disso do lado dele.</summary>
public class FiscalConfiguration
{
    public string TokenFocusNfe { get; set; } = string.Empty;
    public bool UsarAmbienteProducao { get; set; } = false;
}

/// <summary>
/// Abstrai de onde vem a configuração fiscal (token Focus + ambiente).
/// Primeira implementação (JsonFiscalConfigurationProvider) só lê o arquivo
/// local que já existia — nenhuma mudança de comportamento. Uma futura
/// DatabaseFiscalConfigurationProvider troca só isso, sem tocar em
/// IFiscalService nem em nenhuma regra de emissão.
/// </summary>
public interface IFiscalConfigurationProvider
{
    Task<FiscalConfiguration> ObterConfiguracaoAsync();

    /// <summary>Grava/atualiza a configuração — usado tanto pela futura tela
    /// de configuração quanto pela migração do dado existente do JSON pro banco.</summary>
    Task SalvarConfiguracaoAsync(FiscalConfiguration config);
}
