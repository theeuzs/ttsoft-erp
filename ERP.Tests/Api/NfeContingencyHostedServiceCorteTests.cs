using ERP.Api.BackgroundServices;
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Fiscal.Recovery;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ERP.Tests.Api;

/// <summary>
/// 4A-6a: o corte do worker ANTIGO (NfeContingencyHostedService) em relacao a recuperacao fiscal nova.
///
/// Os testes de CARACTERIZACAO (lista vazia / sem interruptor / interruptor invalido, falha de uma linha sem interromper as
/// demais, estados que o antigo continua processando) descrevem o comportamento de SEMPRE e tem que passar tanto no worker
/// anterior a 4A-6a quanto no novo; os de CORTE (tenant habilitado, IntervencaoManual, excecao no interruptor) so passam no novo.
///
/// O QUE ESTES TESTES PROVAM: a selecao do worker antigo, via ProcessarTenantAsync, com INfeContingencyService, os servicos de
/// emissao, de venda e fiscal como dubles que CONTAM chamadas. O QUE NAO PROVAM: nada sobre o worker novo (nao existe ainda),
/// sobre dados reais no banco, nem sobre o que acontece quando o App Service dorme (plano F1).
/// </summary>
public class NfeContingencyHostedServiceCorteTests
{
    private const string ChaveNfce = "41260912820608000141650010000033191728969200";

    private sealed class SwitchQueLanca : IFiscalRecoverySwitch
    {
        public bool EstaHabilitadoPara(Guid tenantId) => throw new InvalidOperationException("configuracao corrompida");
    }

    /// <summary>Marcador: faz o container REGISTRAR um interruptor cuja construcao lanca excecao (o interruptor em si nunca e usado).</summary>
    private sealed class SwitchQueNaoConstroi : IFiscalRecoverySwitch
    {
        public bool EstaHabilitadoPara(Guid tenantId) => throw new NotSupportedException();
    }

    private sealed class Ambiente
    {
        public Ambiente(Guid tenantId, IFiscalRecoverySwitch? interruptor, params NfePendente[] linhas)
        {
            TenantId = tenantId;

            foreach (var linha in linhas)
                linha.TenantId = TenantId;

            Contingencia.Setup(c => c.ObterNotasPendentesAsync()).ReturnsAsync(linhas.OrderBy(l => l.DataFalha).ToList());

            Nfce.Setup(s => s.EmitirNfceAsync(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((true, "Autorizada", "https://focus/danfe.html", "https://focus/nfce.xml", ChaveNfce, "1234"));
            Nfe.Setup(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((true, "Autorizada", "https://focus/danfe.html", "https://focus/nfe.xml", ChaveNfce, "1234"));
            Configuracao.Setup(c => c.ObterConfiguracaoAsync())
                .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

            var services = new ServiceCollection();
            services.AddScoped<IRequestTenant, ERP.Api.Services.RequestTenant>();
            services.AddLogging();
            services.AddSingleton(Contingencia.Object);
            services.AddSingleton(Nfce.Object);
            services.AddSingleton(Nfe.Object);
            services.AddSingleton(Vendas.Object);
            services.AddSingleton(Fiscal.Object);
            services.AddSingleton(Configuracao.Object);
            if (interruptor is SwitchQueNaoConstroi)
                services.AddSingleton<IFiscalRecoverySwitch>(_ => throw new InvalidOperationException("falha ao construir o interruptor"));
            else if (interruptor is not null)
                services.AddSingleton(interruptor);

            var provedor = services.BuildServiceProvider();
            Worker = new NfeContingencyHostedService(
                provedor.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<NfeContingencyHostedService>.Instance);
        }

        public Guid TenantId { get; }
        public Mock<INfeContingencyService> Contingencia { get; } = new();
        public Mock<INfceEmissionService> Nfce { get; } = new();
        public Mock<INfeEmissionService> Nfe { get; } = new();
        public Mock<ISaleService> Vendas { get; } = new();
        public Mock<IFiscalService> Fiscal { get; } = new();
        public Mock<IFiscalConfigurationProvider> Configuracao { get; } = new();
        public NfeContingencyHostedService Worker { get; }

        public Task Rodar() => Worker.ProcessarTenantAsync(TenantId, CancellationToken.None);

        public void NenhumaNfceEmitida() =>
            Nfce.Verify(s => s.EmitirNfceAsync(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());

        public void NenhumaNfeEmitida() =>
            Nfe.Verify(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());

        public void Removida(NfePendente linha, int vezes = 1) =>
            Contingencia.Verify(c => c.RemoverNotaPendenteAsync(linha.Id), Times.Exactly(vezes));
    }

    private static NfePendente Linha(string tipo, string estado = NfePendenteEstados.Ativa, string? referencia = null) => new()
    {
        Id = Guid.NewGuid(),
        VendaId = Guid.NewGuid(),
        TipoNota = tipo,
        Estado = estado,
        CriadaEmProducao = false,   // trava de ambiente: a configuracao destes testes e homologacao
        PayloadJson = "{}",
        Referencia = referencia ?? $"ref-{Guid.NewGuid():N}",
        DataFalha = new DateTime(2026, 10, 8, 9, 0, 0)
    };

    private static IFiscalRecoverySwitch? Interruptor(string modo, Guid tenantId) => modo switch
    {
        "sem" => null,                                                   // nenhum interruptor registrado
        "vazio" => new FiscalRecoverySwitch(""),                         // registrado, lista vazia
        "habilitado" => new FiscalRecoverySwitch(tenantId.ToString()),   // este tenant habilitado
        _ => throw new ArgumentException(modo)
    };

    // ═════════════════════════════════════════════════════════════════════
    //  CARACTERIZACAO: o comportamento de sempre (tem que passar antes e depois da 4A-6a)
    // ═════════════════════════════════════════════════════════════════════

    [Theory(DisplayName = "CARACTERIZACAO: sem interruptor, ou com lista vazia, ou com lista INVALIDA, o worker antigo processa NFC-e e NF-e como sempre")]
    [InlineData("sem")]
    [InlineData("vazio")]
    [InlineData("invalido")]
    public async Task ListaVaziaOuInvalida_ComportamentoLegado(string modo)
    {
        var nfce = Linha("NFCE");
        var nfe = Linha("NFE");
        var tenant = Guid.NewGuid();
        var interruptor = modo == "invalido" ? new FiscalRecoverySwitch("lixo") : Interruptor(modo, tenant);
        var a = new Ambiente(tenant, interruptor, nfce, nfe);

        await a.Rodar();

        a.Nfce.Verify(s => s.EmitirNfceAsync(nfce.Referencia, It.IsAny<FocusNfceRequest>(), "token-teste", false), Times.Once());
        a.Nfe.Verify(s => s.EmitirNfeA4Async(nfe.Referencia, It.IsAny<FocusNfceRequest>(), "token-teste", false), Times.Once());
        a.Removida(nfce);
        a.Removida(nfe);
        a.Fiscal.Verify(f => f.PersistirEmissaoAutorizadaAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }

    [Theory(DisplayName = "CARACTERIZACAO: com a lista INVALIDA que CONTEM o tenant, a NFC-e dele continua com o worker antigo (nada e habilitado por engano)")]
    [InlineData("TENANT,lixo")]
    [InlineData("TENANT,")]
    [InlineData("*")]
    [InlineData("TENANT;TENANT")]
    [InlineData("todos")]
    public async Task ListaInvalidaComOTenant_NaoHabilita(string modelo)
    {
        var nfce = Linha("NFCE");
        var tenant = Guid.NewGuid();
        var interruptor = new FiscalRecoverySwitch(modelo.Replace("TENANT", tenant.ToString()));
        interruptor.ConfiguracaoValida.Should().BeFalse("premissa do teste: a lista e invalida");
        var a = new Ambiente(tenant, interruptor, nfce);

        await a.Rodar();

        a.Nfce.Verify(s => s.EmitirNfceAsync(nfce.Referencia, It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        a.Removida(nfce);
    }

    [Fact(DisplayName = "CARACTERIZACAO: a falha de UMA pendencia nao interrompe as demais (cada linha tem o seu try/catch)")]
    public async Task FalhaDeUmaPendencia_NaoInterrompeAsDemais()
    {
        var quebra = Linha("NFE", referencia: "ref-quebra");
        var segue = Linha("NFE", referencia: "ref-segue");
        quebra.DataFalha = new DateTime(2026, 10, 8, 8, 0, 0);   // a mais antiga: processada primeiro
        var a = new Ambiente(Guid.NewGuid(), null, quebra, segue);
        a.Nfe.Setup(s => s.EmitirNfeA4Async("ref-quebra", It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        await a.Rodar();

        a.Contingencia.Verify(c => c.RegistrarFalhaTentativaAsync(quebra.Id, "boom"), Times.Once());
        a.Removida(quebra, vezes: 0);
        a.Removida(segue);
    }

    [Fact(DisplayName = "CARACTERIZACAO: AguardandoCorrecao e agenda futura NAO sao filtradas pelo worker antigo (so IntervencaoManual e terminal)")]
    public async Task AguardandoCorrecao_ContinuaComOWorkerAntigo()
    {
        var aguardando = Linha("NFE", estado: NfePendenteEstados.AguardandoCorrecao);
        aguardando.ProximaTentativaEm = DateTime.UtcNow.AddHours(1);
        var a = new Ambiente(Guid.NewGuid(), null, aguardando);

        await a.Rodar();

        a.Nfe.Verify(s => s.EmitirNfeA4Async(aguardando.Referencia, It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        a.Removida(aguardando);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  CORTE: o que muda na 4A-6a
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "CORTE: tenant HABILITADO: o worker antigo ignora a NFC-e dele, mas continua tratando a NF-e normalmente")]
    public async Task TenantHabilitado_IgnoraNfce_ProcessaNfe()
    {
        var nfce = Linha("NFCE");
        var nfe = Linha("NFE");
        var tenant = Guid.NewGuid();
        var a = new Ambiente(tenant, new FiscalRecoverySwitch(tenant.ToString()), nfce, nfe);

        await a.Rodar();

        a.NenhumaNfceEmitida();
        a.Nfe.Verify(s => s.EmitirNfeA4Async(nfe.Referencia, It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        a.Removida(nfe);
        a.Removida(nfce, vezes: 0);
        a.Contingencia.Verify(c => c.RegistrarFalhaTentativaAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never());
    }

    [Fact(DisplayName = "CORTE: outro tenant habilitado nao afeta este: a NFC-e dele continua com o worker antigo")]
    public async Task OutroTenantHabilitado_NaoAfetaEste()
    {
        var nfce = Linha("NFCE");
        var a = new Ambiente(Guid.NewGuid(), new FiscalRecoverySwitch(Guid.NewGuid().ToString()), nfce);

        await a.Rodar();

        a.Nfce.Verify(s => s.EmitirNfceAsync(nfce.Referencia, It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        a.Removida(nfce);
    }

    [Theory(DisplayName = "CORTE: IntervencaoManual NUNCA e processada pelo worker antigo (sem interruptor, com lista vazia ou com o tenant habilitado)")]
    [InlineData("sem")]
    [InlineData("vazio")]
    [InlineData("habilitado")]
    public async Task IntervencaoManual_NuncaProcessada(string modo)
    {
        var nfceManual = Linha("NFCE", estado: NfePendenteEstados.IntervencaoManual);
        var nfeManual = Linha("NFE", estado: NfePendenteEstados.IntervencaoManual);
        var tenant = Guid.NewGuid();
        var a = new Ambiente(tenant, Interruptor(modo, tenant), nfceManual, nfeManual);

        await a.Rodar();

        a.NenhumaNfceEmitida();
        a.NenhumaNfeEmitida();
        a.Removida(nfceManual, vezes: 0);
        a.Removida(nfeManual, vezes: 0);
        a.Contingencia.Verify(c => c.RegistrarFalhaTentativaAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never());
        a.Vendas.Verify(v => v.AtualizarDadosNfceAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never());
        a.Configuracao.Verify(c => c.ObterConfiguracaoAsync(), Times.Never());   // nada a fazer: nem chega a ler o token
    }

    [Fact(DisplayName = "CORTE: se o interruptor LANCA excecao ao ser consultado, as NFC-e ficam de fora do ciclo, a NF-e segue e nada estoura")]
    public async Task ExcecaoNoInterruptor_NfceDeFora_NfeSegue_SemLancar()
    {
        var nfce = Linha("NFCE");
        var nfe = Linha("NFE");
        var a = new Ambiente(Guid.NewGuid(), new SwitchQueLanca(), nfce, nfe);

        Func<Task> act = async () => await a.Rodar();

        await act.Should().NotThrowAsync();
        a.NenhumaNfceEmitida();
        a.Nfe.Verify(s => s.EmitirNfeA4Async(nfe.Referencia, It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        a.Removida(nfe);
        a.Removida(nfce, vezes: 0);
    }

    [Fact(DisplayName = "CORTE: se o interruptor nem consegue ser CONSTRUIDO (resolver lanca), as NFC-e ficam de fora, a NF-e segue e nada estoura")]
    public async Task InterruptorQueNaoConstroi_NfceDeFora_NfeSegue()
    {
        var nfce = Linha("NFCE");
        var nfe = Linha("NFE");
        var a = new Ambiente(Guid.NewGuid(), new SwitchQueNaoConstroi(), nfce, nfe);

        Func<Task> act = async () => await a.Rodar();

        await act.Should().NotThrowAsync();
        a.NenhumaNfceEmitida();
        a.Nfe.Verify(s => s.EmitirNfeA4Async(nfe.Referencia, It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Once());
        a.Removida(nfe);
        a.Removida(nfce, vezes: 0);
    }

    [Fact(DisplayName = "CORTE: o interruptor e construido (e validado) no PRIMEIRO ciclo, mesmo com a fila vazia, e uma unica vez")]
    public async Task PrimeiroCiclo_ConstroiOInterruptor_MesmoComFilaVazia()
    {
        var construcoes = 0;
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase($"corte_{Guid.NewGuid():N}"));
        services.AddScoped<IRequestTenant, ERP.Api.Services.RequestTenant>();
        services.AddLogging();
        services.AddSingleton<IFiscalRecoverySwitch>(_ =>
        {
            Interlocked.Increment(ref construcoes);
            return new FiscalRecoverySwitch("");
        });
        using var provedor = services.BuildServiceProvider();
        var worker = new NfeContingencyHostedService(
            provedor.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<NfeContingencyHostedService>.Instance);

        await worker.ProcessarTodosOsTenantsAsync(CancellationToken.None);   // banco vazio: nenhuma pendencia
        await worker.ProcessarTodosOsTenantsAsync(CancellationToken.None);

        construcoes.Should().Be(1, "validado no primeiro ciclo, nao so quando aparece a primeira pendencia; singleton depois");
    }

    [Fact(DisplayName = "CORTE: tenant habilitado com SO NFC-e na fila: o worker antigo nao faz nada e nem le a configuracao fiscal")]
    public async Task TenantHabilitado_SoNfce_NaoFazNada()
    {
        var nfce = Linha("NFCE");
        var tenant = Guid.NewGuid();
        var a = new Ambiente(tenant, new FiscalRecoverySwitch(tenant.ToString()), nfce);

        await a.Rodar();

        a.NenhumaNfceEmitida();
        a.NenhumaNfeEmitida();
        a.Configuracao.Verify(c => c.ObterConfiguracaoAsync(), Times.Never());
        a.Removida(nfce, vezes: 0);
    }
}
