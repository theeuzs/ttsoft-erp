using ERP.Application.Fiscal.Focus;
using ERP.Application.Fiscal.Recovery;
using ERP.Application.Interfaces;
using ERP.Infrastructure.HttpClients;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Api.Extensions;

/// <summary>
/// Registro da recuperacao fiscal (Etapa 4A-5e). INERTE: este metodo so REGISTRA servicos. Nada resolve o
/// orquestrador e nenhum IHostedService o chama; o interruptor e o worker so existem na 4A-6.
///
/// Tudo de que o orquestrador depende (IUnitOfWork, ISaleService, IFiscalService, IRequestTenant) ja e registrado
/// pelo Program.cs como Scoped e NAO e repetido aqui.
/// </summary>
public static class FiscalRecoveryServiceCollectionExtensions
{
    /// <summary>
    /// Timeout do cliente de recuperacao. Explicito de proposito: o padrao do HttpClient e 100 s (e o cliente
    /// antigo da Focus nao define nenhum), e o worker nao deve ficar preso numa unica chamada.
    /// </summary>
    public static readonly TimeSpan TimeoutDoClienteDeRecuperacao = TimeSpan.FromSeconds(60);

    public static IServiceCollection AddFiscalRecovery(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // As URLs do FocusEndpoints sao ABSOLUTAS (host de producao ou homologacao conforme o tenant), entao nao ha
        // BaseAddress. E SEM nenhum handler: o POST de emissao NUNCA pode ser repetido por uma politica de retry.
        // O teste de registro vigia isso; nao acrescente AddPolicyHandler / AddStandardResilienceHandler aqui.
        services.AddHttpClient<IFocusReferenceClient, FocusReferenceClient>(client =>
        {
            client.Timeout = TimeoutDoClienteDeRecuperacao;
        });

        // Padroes: D8 (regeneracao de DataEmissao) DESLIGADA, K = 3. A superficie de configuracao nasce com o
        // interruptor da 4A-6, e nao antes.
        services.AddSingleton(new RecoveryPolicyOptions());

        services.AddScoped<IFiscalRecoveryStore, FiscalRecoveryStore>();

        // Fabrica explicita: nao depende de como o DI trata o parametro opcional do relogio (fica o padrao: UtcNow).
        services.AddScoped(sp => new NfePendenteRecoveryOrchestrator(
            sp.GetRequiredService<IFiscalRecoveryStore>(),
            sp.GetRequiredService<IFocusReferenceClient>(),
            sp.GetRequiredService<IFiscalService>(),
            sp.GetRequiredService<ISaleService>(),
            sp.GetRequiredService<RecoveryPolicyOptions>()));

        return services;
    }
}
