using ERP.Api.BackgroundServices;
using ERP.Api.Extensions;
using ERP.Application.Fiscal.Focus;
using ERP.Application.Fiscal.Recovery;
using ERP.Application.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using ERP.Tests.Fiscal;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.Tests.Api;

/// <summary>
/// 4A-6b: o worker da recuperacao fiscal. Composicao montada a mao, com os pedacos que importam REAIS: o escopo do container, o IRequestTenant
/// scoped (o worker precisa defini-lo ANTES de resolver o contexto), o AppDbContext em SQLite NoTracking como a producao, a store e o orquestrador.
/// A Focus e roteirizada (nada chama a Focus real) e a configuracao fiscal e por tenant, lida do IRequestTenant do escopo.
///
/// O QUE PROVA: inercia com a lista vazia (nenhum escopo e aberto), selecao restrita aos tenants habilitados, ordem "tenant antes de resolver",
/// config e token POR tenant, partida a frio (o estado vem do banco, nao da memoria), cancelamento, isolamento de falha por tenant, alerta
/// deduplicado, ciclo de vida (Start/Stop) e o registro. O QUE NAO PROVA: o ciclo dormir/acordar do App Service no plano F1 (um novo worker
/// provar a retomada apos UMA NOVA INICIALIZACAO, nao a plataforma), SQL Server, nem o caminho de autorizacao com a persistencia fiscal real.
/// </summary>
public class NfeRecoveryHostedServiceTests : IDisposable
{
    private readonly RecoveryAmbiente _banco = new();
    private readonly List<string> _eventos = new();
    private readonly FocusRoteirizado _focus;
    private ServiceProvider? _provedor;

    /// <summary>Quantas vezes o provider da configuracao fiscal foi consultado (prova a releitura real).</summary>
    private int _leiturasDeConfiguracao;

    /// <summary>Gancho chamado a CADA leitura da configuracao, com o numero da leitura (para mudar a configuracao no meio de um ciclo).</summary>
    private Action<int>? _aoLerConfiguracao;

    public NfeRecoveryHostedServiceTests()
    {
        _focus = new FocusRoteirizado(_eventos);
    }

    public void Dispose()
    {
        _provedor?.Dispose();
        _banco.Dispose();
    }

    // ── Infraestrutura ───────────────────────────────────────────────────

    /// <summary>Um IServiceScopeFactory que FALHA se alguem tentar abrir um escopo: prova a inercia com a lista vazia.</summary>
    private sealed class EscopoProibido : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("Nenhum escopo deveria ser aberto com a lista de tenants vazia.");
    }

    private sealed class WorkerQueFalhaPara : NfeRecoveryHostedService
    {
        private readonly Guid _tenantQueFalha;

        public WorkerQueFalhaPara(Guid tenantQueFalha, IServiceScopeFactory escopos, IFiscalRecoverySwitch interruptor)
            : base(escopos, NullLogger<NfeRecoveryHostedService>.Instance, interruptor, new FiscalRecoveryHeartbeat())
        {
            _tenantQueFalha = tenantQueFalha;
        }

        internal override Task ProcessarTenantAsync(Guid tenantId, CancellationToken ct) =>
            tenantId == _tenantQueFalha
                ? throw new InvalidOperationException("falha simulada deste tenant")
                : base.ProcessarTenantAsync(tenantId, ct);
    }

    private void Construir(Dictionary<Guid, (string Token, bool Producao)> configuracoes)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IRequestTenant, ERP.Api.Services.RequestTenant>();
        services.AddScoped<AppDbContext>(sp => _banco.NovoContexto(sp.GetRequiredService<IRequestTenant>()));
        services.AddScoped<IUnitOfWork>(sp => new ERP.Infrastructure.UnitOfWork.UnitOfWork(
            sp.GetRequiredService<AppDbContext>(),
            Mock.Of<IProductRepository>(), Mock.Of<ICustomerRepository>(), Mock.Of<ISaleRepository>(),
            Mock.Of<ICategoryRepository>(), Mock.Of<IUserRepository>(),
            sp.GetRequiredService<IRequestTenant>()));
        services.AddScoped<IFiscalRecoveryStore, FiscalRecoveryStore>();
        services.AddSingleton<IFocusReferenceClient>(_focus);
        services.AddSingleton(Mock.Of<IFiscalService>());
        services.AddSingleton(Mock.Of<ISaleService>());
        services.AddSingleton(new RecoveryPolicyOptions());

        // A configuracao e POR TENANT e e lida do IRequestTenant do escopo, no momento da chamada: se o worker nao tiver definido o tenant antes, vem vazia.
        services.AddScoped<IFiscalConfigurationProvider>(sp =>
        {
            var tenant = sp.GetRequiredService<IRequestTenant>();
            var mock = new Mock<IFiscalConfigurationProvider>();
            mock.Setup(c => c.ObterConfiguracaoAsync()).Returns(() =>
            {
                _leiturasDeConfiguracao++;
                _aoLerConfiguracao?.Invoke(_leiturasDeConfiguracao);

                return Task.FromResult(configuracoes.TryGetValue(tenant.TenantId, out var c)
                    ? new FiscalConfiguration { TokenFocusNfe = c.Token, UsarAmbienteProducao = c.Producao }
                    : new FiscalConfiguration());
            });
            return mock.Object;
        });

        services.AddScoped(sp => new NfePendenteRecoveryOrchestrator(
            sp.GetRequiredService<IFiscalRecoveryStore>(),
            sp.GetRequiredService<IFocusReferenceClient>(),
            sp.GetRequiredService<IFiscalService>(),
            sp.GetRequiredService<ISaleService>(),
            sp.GetRequiredService<RecoveryPolicyOptions>()));

        _provedor = services.BuildServiceProvider();
    }

    private IServiceScopeFactory Escopos => _provedor!.GetRequiredService<IServiceScopeFactory>();

    private NfeRecoveryHostedService NovoWorker(
        IFiscalRecoverySwitch interruptor,
        FiscalRecoveryHeartbeat? batimento = null,
        ILogger<NfeRecoveryHostedService>? logger = null,
        Func<DateTimeOffset>? relogio = null,
        IServiceScopeFactory? escopos = null,
        TimeSpan? atrasoInicial = null,
        TimeSpan? intervalo = null) =>
        new(escopos ?? Escopos, logger ?? NullLogger<NfeRecoveryHostedService>.Instance, interruptor,
            batimento ?? new FiscalRecoveryHeartbeat(), atrasoInicial, intervalo, relogio);

    private Guid Semear(Guid tenantId, DateTime? proximaTentativaEm = null, string estado = NfePendenteEstados.Ativa, string tipo = "NFCE", bool? criadaEmProducao = false)
    {
        using var ctx = _banco.NovoContexto(tenantId);

        var nota = new NfePendente
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendaId = Guid.NewGuid(),
            TipoNota = tipo,
            Estado = estado,
            ProximaTentativaEm = proximaTentativaEm,
            PayloadJson = RecoveryFixtures.PayloadNfce,
            Referencia = Guid.NewGuid().ToString(),
            DataFalha = FusoBrasilHelper.AgoraNoBrasil().AddMinutes(-30),
            CriadaEmProducao = criadaEmProducao   // as configuracoes destes testes sao homologacao
        };

        ctx.NfePendentes.Add(nota);
        ctx.SaveChanges();
        return nota.Id;
    }

    private static Dictionary<Guid, (string Token, bool Producao)> Configs(params (Guid Tenant, string Token)[] itens) =>
        itens.ToDictionary(i => i.Tenant, i => (i.Token, false));

    // ═════════════════════════════════════════════════════════════════════
    //  Inercia e ciclo de vida
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "INERCIA: com a lista vazia, um ciclo so bate o coracao: NENHUM escopo, nenhum banco, nenhuma chamada a Focus")]
    public async Task ListaVazia_NaoAbreEscopo_NaoChamaNada_MasBate()
    {
        var batimento = new FiscalRecoveryHeartbeat();
        var worker = NovoWorker(new FiscalRecoverySwitch(""), batimento, escopos: new EscopoProibido());

        Func<Task> act = async () => await worker.ExecutarCicloAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        batimento.TotalDeCiclos.Should().Be(1, "o batimento prova que o worker esta vivo, mesmo sem trabalho");
        _focus.Chamadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "CICLO DE VIDA: Start e Stop com a lista vazia: bate o coracao repetidamente, nunca abre escopo e para limpo")]
    public async Task StartStop_ListaVazia_BateEParaLimpo()
    {
        var batimento = new FiscalRecoveryHeartbeat();
        var worker = NovoWorker(new FiscalRecoverySwitch(""), batimento, escopos: new EscopoProibido(),
            atrasoInicial: TimeSpan.FromMilliseconds(10), intervalo: TimeSpan.FromMilliseconds(20));

        await worker.StartAsync(CancellationToken.None);
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (batimento.TotalDeCiclos < 3 && DateTime.UtcNow < limite)
            await Task.Delay(10);
        await worker.StopAsync(CancellationToken.None);

        batimento.TotalDeCiclos.Should().BeGreaterThanOrEqualTo(3);
        var aoParar = batimento.TotalDeCiclos;
        await Task.Delay(120);
        batimento.TotalDeCiclos.Should().Be(aoParar, "depois do StopAsync o worker nao bate mais");
    }

    [Fact(DisplayName = "REGISTRO: AddFiscalRecoveryWorker adiciona exatamente UM hosted service, o NfeRecoveryHostedService")]
    public void Registro_UmWorker()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddFiscalRecovery();
        var antes = services.Count(d => d.ServiceType == typeof(IHostedService));

        services.AddFiscalRecoveryWorker();

        antes.Should().Be(0, "AddFiscalRecovery sozinho nunca registra worker");
        services.Count(d => d.ServiceType == typeof(IHostedService)).Should().Be(1);
        using var provedor = services.BuildServiceProvider();
        provedor.GetServices<IHostedService>().Should().ContainSingle().Which.Should().BeOfType<NfeRecoveryHostedService>();
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Selecao e ordem do tenant
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "HABILITADOS A e B: so eles sao processados, cada um com o SEU token (tenant definido ANTES de resolver); C, nao habilitado, fica intocado")]
    public async Task Habilitados_CadaUmComSeuToken_NaoHabilitadoIntocado()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        Construir(Configs((a, "token-A"), (b, "token-B"), (c, "token-C")));
        var idA = Semear(a); var idB = Semear(b); var idC = Semear(c);
        var refA = _banco.Ler(idA).Referencia; var refB = _banco.Ler(idB).Referencia;
        _focus.EnfileirarGet(Resp.Processando(), Resp.Processando());
        var worker = NovoWorker(new FiscalRecoverySwitch($"{a},{b}"));

        await worker.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().HaveCount(2).And.OnlyContain(x => x.Metodo == "GET");
        _focus.Chamadas.Should().Contain(x => x.Referencia == refA && x.Token == "token-A");
        _focus.Chamadas.Should().Contain(x => x.Referencia == refB && x.Token == "token-B");
        foreach (var id in new[] { idA, idB })
        {
            var n = _banco.Ler(id);
            n.TentativasConsulta.Should().Be(1);
            n.ProximaTentativaEm.Should().NotBeNull();
        }
        var intocada = _banco.Ler(idC);
        intocada.TentativasConsulta.Should().Be(0);
        intocada.ProximaTentativaEm.Should().BeNull();
    }

    [Fact(DisplayName = "NAO DESCOBERTAS: agenda no futuro, IntervencaoManual, NF-e e linha de tenant nao habilitado: nenhuma chamada, nenhuma coluna alterada")]
    public async Task NaoDescobertas_NadaAcontece()
    {
        var a = Guid.NewGuid(); var outro = Guid.NewGuid();
        Construir(Configs((a, "token-A"), (outro, "token-O")));
        var futura = Semear(a, proximaTentativaEm: DateTime.UtcNow.AddHours(1));
        var manual = Semear(a, estado: NfePendenteEstados.IntervencaoManual);
        var nfe = Semear(a, tipo: "NFE");
        var deOutro = Semear(outro);
        var fotos = new[] { futura, manual, nfe, deOutro }.ToDictionary(id => id, id => FotoPendencia.De(_banco.Ler(id)));
        var worker = NovoWorker(new FiscalRecoverySwitch(a.ToString()));

        await worker.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().BeEmpty();
        foreach (var (id, foto) in fotos)
            FotoPendencia.De(_banco.Ler(id)).Should().Be(foto);
    }

    [Fact(DisplayName = "TENANT SEM TOKEN: o orquestrador marca AguardandoCorrecao SEM nenhuma chamada a Focus (a configuracao e por tenant)")]
    public async Task TenantSemToken_AguardandoCorrecao_SemHttp()
    {
        var d = Guid.NewGuid();
        Construir(new Dictionary<Guid, (string Token, bool Producao)>());   // nenhum tenant tem configuracao
        var id = Semear(d);
        var worker = NovoWorker(new FiscalRecoverySwitch(d.ToString()));

        await worker.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().BeEmpty();
        _banco.Ler(id).Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Partida a frio, falha isolada, cancelamento, alerta
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "PARTIDA A FRIO: um worker NOVO processa a pendencia vencida no PRIMEIRO ciclo; o estado vem do banco, nao da memoria")]
    public async Task PartidaAFrio_PrimeiroCicloProcessaOVencido()
    {
        var a = Guid.NewGuid();
        Construir(Configs((a, "token-A")));
        var id = Semear(a);
        _focus.EnfileirarGet(Resp.Processando());
        var interruptor = new FiscalRecoverySwitch(a.ToString());

        _ = NovoWorker(interruptor);                               // um worker que nunca chega a rodar (o processo "dormiu")
        var depoisDeAcordar = NovoWorker(interruptor);             // outro, de memoria zerada
        await depoisDeAcordar.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().ContainSingle();
        _banco.Ler(id).TentativasConsulta.Should().Be(1);

        var outroDepois = NovoWorker(interruptor);                 // memoria zerada de novo: a agenda gravada no banco manda
        await outroDepois.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().ContainSingle("a agenda futura esta gravada no banco; um worker novo nao a refaz");
    }

    [Fact(DisplayName = "FALHA ISOLADA: se um tenant lanca excecao, os demais sao processados e o ciclo nao estoura")]
    public async Task FalhaDeUmTenant_NaoImpedeOsDemais()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Construir(Configs((a, "token-A"), (b, "token-B")));
        var idA = Semear(a); var idB = Semear(b);
        _focus.EnfileirarGet(Resp.Processando());
        var worker = new WorkerQueFalhaPara(a, Escopos, new FiscalRecoverySwitch($"{a},{b}"));

        Func<Task> act = async () => await worker.ExecutarCicloAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        _banco.Ler(idA).TentativasConsulta.Should().Be(0, "o tenant que falhou nao foi tocado");
        _banco.Ler(idB).TentativasConsulta.Should().Be(1, "o outro tenant foi processado");
    }

    [Fact(DisplayName = "CANCELAMENTO no meio do ciclo: propaga, nao vira erro de processamento e nao altera nenhuma pendencia")]
    public async Task Cancelamento_PropagaESemEstrago()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Construir(Configs((a, "token-A"), (b, "token-B")));
        var idA = Semear(a); var idB = Semear(b);
        var fotoA = FotoPendencia.De(_banco.Ler(idA)); var fotoB = FotoPendencia.De(_banco.Ler(idB));
        using var cts = new CancellationTokenSource();
        _focus.EnfileirarGet((Func<CancellationToken, Task<FocusResponse>>)(async ct =>
        {
            cts.Cancel();   // o cancelamento chega DURANTE a chamada
            await Task.Delay(Timeout.Infinite, ct);
            return null!;
        }));
        var worker = NovoWorker(new FiscalRecoverySwitch($"{a},{b}"));

        Func<Task> act = async () => await worker.ExecutarCicloAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _focus.Chamadas.Should().ContainSingle("o segundo tenant nem chega a comecar");
        FotoPendencia.De(_banco.Ler(idA)).Should().Be(fotoA);
        FotoPendencia.De(_banco.Ler(idB)).Should().Be(fotoB);
    }

    [Fact(DisplayName = "ALERTA deduplicado: IntervencaoManual gera um aviso por tenant a cada 30 min, nao um por ciclo")]
    public async Task Alerta_Deduplicado()
    {
        var a = Guid.NewGuid();
        Construir(Configs((a, "token-A")));
        var logger = new LoggerQueCaptura<NfeRecoveryHostedService>();
        var agora = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        var worker = NovoWorker(new FiscalRecoverySwitch(a.ToString()), logger: logger, relogio: () => agora);
        _focus.EnfileirarGet(Resp.NaoEncontrado(), Resp.NaoEncontrado(), Resp.NaoEncontrado());   // D8 desligada: 404 vira IntervencaoManual

        Semear(a);
        await worker.ExecutarCicloAsync(CancellationToken.None);
        logger.Avisos("ATENCAO").Should().Be(1);

        agora = agora.AddMinutes(10);
        Semear(a);
        await worker.ExecutarCicloAsync(CancellationToken.None);
        logger.Avisos("ATENCAO").Should().Be(1, "dentro de 30 minutos nao repete o alerta do mesmo tenant");

        agora = agora.AddMinutes(31);
        Semear(a);
        await worker.ExecutarCicloAsync(CancellationToken.None);
        logger.Avisos("ATENCAO").Should().Be(2, "passados 30 minutos, alerta de novo");
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Trava de ambiente no worker (Estagio 2)
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "RELEITURA REAL: cada validacao consulta o provider da configuracao de novo (uma ao montar o contexto, outra imediatamente antes do GET)")]
    public async Task TravaDeAmbiente_ReleituraReal_ConsultaOProviderACadaValidacao()
    {
        var a = Guid.NewGuid();
        Construir(Configs((a, "token-A")));
        Semear(a);
        _focus.EnfileirarGet(Resp.Processando());
        var worker = NovoWorker(new FiscalRecoverySwitch(a.ToString()));

        await worker.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().ContainSingle();
        _leiturasDeConfiguracao.Should().Be(2, "nenhuma configuracao capturada antes e reutilizada");
    }

    [Fact(DisplayName = "FLAG TROCADO DEPOIS DE CRIADA: pendencia nascida em homologacao e tenant agora em producao: NENHUMA chamada; ao voltar a homologacao, retoma")]
    public async Task TravaDeAmbiente_FlagTrocado_BloqueiaERetoma()
    {
        var a = Guid.NewGuid();
        var configs = Configs((a, "token-A"));
        Construir(configs);
        var id = Semear(a);                              // nasceu em homologacao
        configs[a] = ("token-A", true);                  // o tenant foi para producao
        var worker = NovoWorker(new FiscalRecoverySwitch(a.ToString()));

        await worker.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().BeEmpty();
        var bloqueada = _banco.Ler(id);
        bloqueada.Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        bloqueada.UltimaDecisao.Should().StartWith("AmbienteDivergente:").And.Contain("configurado agora: Producao");
        bloqueada.TentativasConsulta.Should().Be(0);

        configs[a] = ("token-A", false);                 // volta a homologacao
        _banco.ModificarPorFora(id, n => n.ProximaTentativaEm = DateTime.UtcNow.AddMinutes(-1));
        _focus.EnfileirarGet(Resp.Processando());
        await worker.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().ContainSingle().Which.IsProducao.Should().BeFalse();
        _banco.Ler(id).TentativasConsulta.Should().Be(1);
    }

    [Fact(DisplayName = "CONTEXTO VELHO no worker: a configuracao muda DEPOIS de o tenant ser lido e ANTES do GET: bloqueia (zero chamadas)")]
    public async Task TravaDeAmbiente_ConfiguracaoMudaDepoisDeMontarOContexto_Bloqueia()
    {
        var a = Guid.NewGuid();
        var configs = Configs((a, "token-A"));
        Construir(configs);
        var id = Semear(a);                              // nasceu em homologacao
        _aoLerConfiguracao = leitura => { if (leitura == 2) configs[a] = ("token-A", true); };   // so a releitura ve producao
        var worker = NovoWorker(new FiscalRecoverySwitch(a.ToString()));

        await worker.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().BeEmpty("o contexto foi montado para homologacao, mas a releitura ja diz producao");
        _banco.Ler(id).UltimaDecisao.Should().Contain("a chamada usaria Homologacao").And.Contain("configurado agora: Producao");
        _leiturasDeConfiguracao.Should().Be(2);
    }

    [Fact(DisplayName = "CONFIGURACAO AUSENTE na releitura (a linha do tenant some depois de o contexto ser montado): ilegivel, bloqueia; NAO e lida como 'homologacao confirmada'")]
    public async Task TravaDeAmbiente_ConfiguracaoAusenteNaReleitura_Bloqueia()
    {
        var a = Guid.NewGuid();
        var configs = Configs((a, "token-A"));
        Construir(configs);
        var id = Semear(a);                              // nasceu em homologacao, e a configuracao ainda diz homologacao no inicio
        _aoLerConfiguracao = leitura => { if (leitura == 2) configs.Remove(a); };   // a releitura nao encontra mais a configuracao
        var worker = NovoWorker(new FiscalRecoverySwitch(a.ToString()));

        await worker.ExecutarCicloAsync(CancellationToken.None);

        _focus.Chamadas.Should().BeEmpty("sem configuracao nao ha como confirmar o ambiente");
        var bloqueada = _banco.Ler(id);
        bloqueada.Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        bloqueada.UltimaDecisao.Should().StartWith("AmbienteIndeterminado:");
        bloqueada.TentativasConsulta.Should().Be(0);
    }
}
