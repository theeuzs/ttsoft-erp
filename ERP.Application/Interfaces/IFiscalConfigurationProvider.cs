// ── ERP.Application/Interfaces/IFiscalConfigurationProvider.cs ─────────────
namespace ERP.Application.Interfaces;

/// <summary>Só os dois campos que a emissão fiscal de verdade consome hoje —
/// nada de CSC/Série especulativos: o Focus NFe cuida disso do lado dele.</summary>
public class FiscalConfiguration
{
    /// <summary>Já resolvido pro ambiente atual (produção ou homologação,
    /// conforme UsarAmbienteProducao) — é isso que toda chamada à Focus usa.
    /// Quem só quer emitir/consultar nota não precisa saber que tem dois
    /// tokens por trás.</summary>
    public string TokenFocusNfe { get; set; } = string.Empty;
    public bool UsarAmbienteProducao { get; set; } = false;

    /// <summary>Necessário pro MD-e (consulta por CNPJ). Achado (20/08):
    /// esse campo ficou órfão em 3 lugares diferentes ao mesmo tempo — não
    /// existia no ReciboConfig, não era carregado/salvo no ConfiguracoesViewModel,
    /// e o JsonFiscalConfigurationProvider nunca repassava ele adiante mesmo
    /// depois de existir no arquivo. Os três corrigidos juntos.</summary>
    public string? Cnpj { get; set; }

    // Achado (21/08) — Focus NFe usa token SEPARADO por ambiente por
    // empresa. Esses dois brutos só existem pra tela de Configurações
    // carregar/salvar os dois de uma vez; ObterConfiguracaoAsync() já
    // resolve TokenFocusNfe a partir daqui, sozinho.
    public string? TokenFocusNfeProducao { get; set; }
    public string? TokenFocusNfeHomologacao { get; set; }
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