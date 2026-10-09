using System.Net.Http;
using ERP.Api.Extensions;
using ERP.Application.Fiscal.Focus;
using ERP.Application.Fiscal.Recovery;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;
using ERP.Infrastructure.HttpClients;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ERP.Tests.Api;

/// <summary>
/// 4A-5e: o registro da recuperacao fiscal resolve e continua INERTE. Testa o efeito do AddFiscalRecovery() ISOLADO
/// (numa colecao propria), sem afirmar nada sobre a aplicacao inteira: o Program.cs ja registra outros workers fiscais
/// por outros caminhos.
/// </summary>
public class FiscalRecoveryRegistrationTests
{
    /// <summary>Assemblies do framework que podem aparecer na cadeia de handlers de um HttpClient "limpo".</summary>
    private static readonly HashSet<string> AssembliesDoFramework = new(StringComparer.Ordinal)
    {
        "Microsoft.Extensions.Http",   // handlers de ciclo de vida e de log da HttpClientFactory
        "System.Net.Http"              // o handler primario
    };

    /// <summary>As dependencias que o Program.cs ja registra como Scoped, aqui substituidas por dubles.</summary>
    private static ServiceCollection NovaColecaoComDependencias()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new Mock<IUnitOfWork>().Object);
        services.AddScoped(_ => new Mock<IFiscalService>().Object);
        services.AddScoped(_ => new Mock<ISaleService>().Object);
        return services;
    }

    private static ServiceProvider Construir(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    /// <summary>Tipos da cadeia de handlers do cliente nomeado que NAO sao do framework (ex.: retry/resiliencia).</summary>
    private static List<Type> HandlersForaDoFramework(IServiceProvider provider, string nomeDoCliente)
    {
        var cadeia = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nomeDoCliente);
        var fora = new List<Type>();

        for (HttpMessageHandler? atual = cadeia; atual is not null; atual = (atual as DelegatingHandler)?.InnerHandler)
        {
            var assembly = atual.GetType().Assembly.GetName().Name ?? string.Empty;
            if (!AssembliesDoFramework.Contains(assembly))
                fora.Add(atual.GetType());
        }

        return fora;
    }

    private static ServiceLifetime Lifetime(IServiceCollection services, Type tipo) =>
        services.Single(d => d.ServiceType == tipo).Lifetime;

    private sealed class HandlerDeControle : DelegatingHandler
    {
    }

    [Fact(DisplayName = "AddFiscalRecovery ISOLADO nao adiciona nenhum IHostedService (a recuperacao nao se inicia sozinha)")]
    public void Registro_NaoAdicionaHostedService()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();   // linha de base do framework: so conta o que o NOSSO registro acrescenta
        var antes = services.Count(d => d.ServiceType == typeof(IHostedService));

        services.AddFiscalRecovery();

        services.Count(d => d.ServiceType == typeof(IHostedService)).Should().Be(antes);
        services.Where(d => d.ImplementationType is not null
                         && d.ImplementationType.Namespace is not null
                         && d.ImplementationType.Namespace.StartsWith("ERP", StringComparison.Ordinal)
                         && typeof(IHostedService).IsAssignableFrom(d.ImplementationType))
                .Should().BeEmpty("nenhum tipo do ERP registrado por aqui pode ser um hosted service");
    }

    [Fact(DisplayName = "Lifetimes: opcoes Singleton; store e orquestrador Scoped; um unico registro de cada")]
    public void Registro_Lifetimes()
    {
        var services = new ServiceCollection();

        services.AddFiscalRecovery();

        Lifetime(services, typeof(RecoveryPolicyOptions)).Should().Be(ServiceLifetime.Singleton);
        Lifetime(services, typeof(IFiscalRecoveryStore)).Should().Be(ServiceLifetime.Scoped);
        Lifetime(services, typeof(NfePendenteRecoveryOrchestrator)).Should().Be(ServiceLifetime.Scoped);
    }

    [Fact(DisplayName = "O orquestrador resolve por um escopo; e o mesmo dentro do escopo e DISTINTO entre escopos")]
    public void Orquestrador_ResolvePorEscopo_EDistintoEntreEscopos()
    {
        var services = NovaColecaoComDependencias();
        services.AddFiscalRecovery();
        using var provider = Construir(services);

        NfePendenteRecoveryOrchestrator a1, a2, b;
        IFiscalRecoveryStore storeA, storeB;

        using (var escopoA = provider.CreateScope())
        {
            a1 = escopoA.ServiceProvider.GetRequiredService<NfePendenteRecoveryOrchestrator>();
            a2 = escopoA.ServiceProvider.GetRequiredService<NfePendenteRecoveryOrchestrator>();
            storeA = escopoA.ServiceProvider.GetRequiredService<IFiscalRecoveryStore>();
        }

        using (var escopoB = provider.CreateScope())
        {
            b = escopoB.ServiceProvider.GetRequiredService<NfePendenteRecoveryOrchestrator>();
            storeB = escopoB.ServiceProvider.GetRequiredService<IFiscalRecoveryStore>();
        }

        a1.Should().BeSameAs(a2);
        a1.Should().NotBeSameAs(b);
        storeA.Should().NotBeSameAs(storeB);
        storeA.Should().BeOfType<FiscalRecoveryStore>();
    }

    [Fact(DisplayName = "O cliente de recuperacao tem timeout de 60 s e NAO ganha BaseAddress (as URLs do FocusEndpoints sao absolutas)")]
    public void Cliente_Timeout60s_SemBaseAddress()
    {
        var services = NovaColecaoComDependencias();
        services.AddFiscalRecovery();
        using var provider = Construir(services);
        using var escopo = provider.CreateScope();

        escopo.ServiceProvider.GetRequiredService<IFocusReferenceClient>().Should().BeOfType<FocusReferenceClient>();

        // O cliente tipado usa o HttpClient nomeado "IFocusReferenceClient" (nome do tipo do contrato).
        using var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IFocusReferenceClient));
        http.Timeout.Should().Be(TimeSpan.FromSeconds(60));
        http.Timeout.Should().Be(FiscalRecoveryServiceCollectionExtensions.TimeoutDoClienteDeRecuperacao);
        http.BaseAddress.Should().BeNull();
    }

    [Fact(DisplayName = "A CADEIA de handlers do cliente de recuperacao so tem handlers do framework: nenhum retry nem resiliencia")]
    public void Cliente_SemHandlersDeRetry()
    {
        var services = NovaColecaoComDependencias();
        services.AddFiscalRecovery();
        using var provider = Construir(services);

        HandlersForaDoFramework(provider, nameof(IFocusReferenceClient))
            .Should().BeEmpty("o POST de emissao NUNCA pode ser repetido por um handler de retry/resiliencia");

        provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>()
            .Get(nameof(IFocusReferenceClient))
            .HttpMessageHandlerBuilderActions
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "CONTROLE: o detector de handlers PEGA um handler customizado (o teste acima nao e vazio)")]
    public void Controle_DetectorPegaHandlerCustomizado()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("controle").AddHttpMessageHandler(() => new HandlerDeControle());
        using var provider = services.BuildServiceProvider();

        var fora = HandlersForaDoFramework(provider, "controle");

        fora.Should().ContainSingle().Which.Should().Be(typeof(HandlerDeControle));
    }

    [Fact(DisplayName = "RecoveryPolicyOptions: singleton, com os padroes (D8 desligada, K = 3, backoff 2/2/4/8/15/30 min)")]
    public void Opcoes_SingletonComPadroes()
    {
        var services = NovaColecaoComDependencias();
        services.AddFiscalRecovery();
        using var provider = Construir(services);

        var daRaiz = provider.GetRequiredService<RecoveryPolicyOptions>();
        RecoveryPolicyOptions deUmEscopo;
        using (var escopo = provider.CreateScope())
            deUmEscopo = escopo.ServiceProvider.GetRequiredService<RecoveryPolicyOptions>();

        deUmEscopo.Should().BeSameAs(daRaiz, "e singleton");
        daRaiz.PermitirRegeneracaoDataEmissao.Should().BeFalse("a D8 so liga depois que o contador aprovar");
        daRaiz.JanelaMaximaRegeneracaoHoras.Should().BeNull();
        daRaiz.LimiteDesconhecidos.Should().Be(3);
        daRaiz.Backoff.Should().Equal(
            TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(4),
            TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30));
        daRaiz.EsperaProcessando.Should().Be(TimeSpan.FromMinutes(2));
        daRaiz.EsperaMaximaProcessando.Should().Be(TimeSpan.FromHours(6));
        daRaiz.EsperaDesconhecido.Should().Be(TimeSpan.FromMinutes(2));
        daRaiz.EsperaAguardandoCorrecao.Should().Be(TimeSpan.FromMinutes(30));
    }
}