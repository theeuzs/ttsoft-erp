using System.Net.Http;
using ERP.Api.BackgroundServices;
using ERP.Application.Fiscal.Focus;
using ERP.Application.Fiscal.Recovery;
using ERP.Infrastructure.HttpClients;
using ERP.Tests.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ERP.Tests.Api;

/// <summary>
/// 4A-5f: o grafo da recuperacao fiscal resolve na composicao REAL da aplicacao (o Program.cs verdadeiro, via
/// ErpApiFactory), e continua inerte. SO TESTE: nenhum codigo de producao muda, e nenhum metodo do orquestrador,
/// da store ou do cliente e CHAMADO; so se resolvem objetos.
///
/// O QUE ESTE TESTE PROVA
///  - Com os registros reais do Program.cs, o orquestrador, a store e o cliente resolvem num escopo, junto com
///    IFiscalService, ISaleService, IUnitOfWork e IRequestTenant REAIS (nenhum deles e substituido pela fabrica).
///  - A resolucao nao envia nenhuma requisicao pelo cliente de recuperacao (um handler contador, com controle
///    positivo, prova isso).
///  - Na aplicacao inteira nenhum hosted service e da recuperacao; os workers fiscais ja existentes seguem registrados.
///  - As opcoes compostas tem D8 desligada e K = 3, e a cadeia de handlers do cliente nao tem retry/resiliencia.
///
/// O QUE ESTE TESTE NAO PROVA (a ErpApiFactory difere de producao, e isto aqui so constroi objetos)
///  - O banco e SQLite em memoria (nao SQL Server) e o contexto rastreia entidades (a producao e NoTracking).
///  - O ambiente e "Testing", o JWT e substituido, e IFocusNfeHttpClient, IStorageService e BrasilApiService sao dubles.
///  - Nada aqui executa o orquestrador: nao ha prova de comportamento com dados reais, da ordem "tenant ANTES de resolver"
///    que o worker da 4A-6 vai precisar, do ciclo de vida do worker, nem do que acontece quando o app dorme (plano F1).
/// </summary>
public class FiscalRecoveryCompositionTests : IClassFixture<ErpApiFactory>
{
    private readonly ErpApiFactory _factory;

    public FiscalRecoveryCompositionTests(ErpApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Assemblies do framework aceitos na cadeia de handlers de um HttpClient "limpo".</summary>
    private static readonly HashSet<string> AssembliesDoFramework = new(StringComparer.Ordinal)
    {
        "Microsoft.Extensions.Http",
        "System.Net.Http"
    };

    /// <summary>Handler que NUNCA deixa uma requisicao sair: conta a tentativa e lanca.</summary>
    private sealed class ContadorDeRequisicoes : HttpMessageHandler
    {
        private int _chamadas;

        public int Chamadas => Volatile.Read(ref _chamadas);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _chamadas);
            throw new InvalidOperationException("Nenhuma requisicao real e permitida neste teste: " + request.RequestUri);
        }
    }

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

    [Fact(DisplayName = "COMPOSICAO REAL: o orquestrador, a store e o cliente de recuperacao resolvem num escopo do Program.cs verdadeiro")]
    public void ComposicaoReal_ResolveOGrafoDaRecuperacao()
    {
        using var escopo = _factory.Services.CreateScope();
        var sp = escopo.ServiceProvider;

        sp.GetRequiredService<NfePendenteRecoveryOrchestrator>().Should().NotBeNull();
        sp.GetRequiredService<IFiscalRecoveryStore>().Should().BeOfType<FiscalRecoveryStore>();
        sp.GetRequiredService<IFocusReferenceClient>().Should().BeOfType<FocusReferenceClient>();
    }

    [Fact(DisplayName = "Resolver o grafo NAO envia nenhuma requisicao (controle positivo: a guarda esta mesmo no caminho do cliente)")]
    public async Task ComposicaoReal_ResolverNaoEnviaRequisicao()
    {
        var contador = new ContadorDeRequisicoes();

        // Mesma composicao real; so o handler PRIMARIO do cliente de recuperacao e trocado por um que nao deixa sair nada.
        using var fabrica = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddHttpClient(nameof(IFocusReferenceClient))
                    .ConfigurePrimaryHttpMessageHandler(() => contador)));

        using var escopo = fabrica.Services.CreateScope();
        var sp = escopo.ServiceProvider;

        sp.GetRequiredService<NfePendenteRecoveryOrchestrator>().Should().NotBeNull();
        sp.GetRequiredService<IFiscalRecoveryStore>().Should().NotBeNull();
        sp.GetRequiredService<IFocusReferenceClient>().Should().NotBeNull();
        using var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IFocusReferenceClient));

        contador.Chamadas.Should().Be(0, "resolver e construir o cliente nao pode gerar trafego para a Focus");

        // CONTROLE: se uma requisicao saisse, a guarda a pegaria. Sem isto, "0 chamadas" poderia ser so um handler fora do caminho.
        Func<Task> enviar = async () => await http.GetAsync("https://focus.invalid/controle");
        await enviar.Should().ThrowAsync<InvalidOperationException>();
        contador.Chamadas.Should().Be(1);
    }

    [Fact(DisplayName = "Na aplicacao INTEIRA nenhum hosted service e da recuperacao; os workers fiscais existentes continuam registrados; D8 desligada e K = 3")]
    public void ComposicaoReal_Inerte_EOpcoesComPadroes()
    {
        var hostedServices = _factory.Services.GetServices<IHostedService>().Select(h => h.GetType()).ToList();

        hostedServices.Should().Contain(typeof(NfeContingencyHostedService), "o worker antigo continua registrado e e quem trabalha hoje");
        hostedServices.Should().Contain(typeof(NfeStatusReconciliationHostedService));
        hostedServices.Where(t => t.Namespace is not null && t.Namespace.Contains("Recovery", StringComparison.OrdinalIgnoreCase))
            .Should().BeEmpty("a recuperacao nao tem worker: o HostedService e o interruptor so existem na 4A-6");

        var opcoes = _factory.Services.GetRequiredService<RecoveryPolicyOptions>();
        opcoes.PermitirRegeneracaoDataEmissao.Should().BeFalse("a D8 so liga depois que o contador aprovar o limite N");
        opcoes.JanelaMaximaRegeneracaoHoras.Should().BeNull();
        opcoes.LimiteDesconhecidos.Should().Be(3);
    }

    [Fact(DisplayName = "Na composicao real, a CADEIA de handlers do cliente de recuperacao so tem handlers do framework (sem retry nem resiliencia)")]
    public void ComposicaoReal_ClienteSemHandlersDeRetry()
    {
        HandlersForaDoFramework(_factory.Services, nameof(IFocusReferenceClient))
            .Should().BeEmpty("o POST de emissao nunca pode ser repetido; nenhum handler global do app pode se interpor");
    }
}
