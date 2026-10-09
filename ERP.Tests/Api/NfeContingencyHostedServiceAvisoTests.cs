using ERP.Api.BackgroundServices;
using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Fiscal.Recovery;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Tests.Fiscal;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ERP.Tests.Api;

/// <summary>
/// 4A-6b (K4): o worker ANTIGO avisa (Warning, deduplicado) quando deixa NFC-e "para a recuperacao" e o worker da recuperacao NAO deu sinal de
/// vida recentemente, e fica em silencio quando a recuperacao esta viva, na carencia de uma subida, ou quando nao deixou nada.
///
/// O QUE PROVA: o criterio do aviso, via ProcessarTenantAsync, com um logger que captura as mensagens. O QUE NAO PROVA: nada sobre a entrega do aviso
/// (e so um log), nem sobre a retomada do worker novo.
/// </summary>
public class NfeContingencyHostedServiceAvisoTests
{
    private static readonly TimeSpan Carencia = TimeSpan.FromMinutes(5);
    private const string Trecho = "NAO deu sinal de vida";

    private sealed class Relogio
    {
        public DateTimeOffset Agora { get; set; } = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        public Func<DateTimeOffset> Ler => () => Agora;
    }

    private sealed class Ambiente
    {
        public Ambiente(Guid tenantId, IFiscalRecoverySwitch? interruptor, IFiscalRecoveryHeartbeat? batimento, params NfePendente[] linhas)
        {
            TenantId = tenantId;
            foreach (var linha in linhas)
                linha.TenantId = tenantId;

            var contingencia = new Mock<INfeContingencyService>();
            contingencia.Setup(c => c.ObterNotasPendentesAsync()).ReturnsAsync(linhas.OrderBy(l => l.DataFalha).ToList());

            var nfce = new Mock<INfceEmissionService>();
            nfce.Setup(s => s.EmitirNfceAsync(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((true, "Autorizada", "https://focus/danfe.html", "https://focus/nfce.xml", "41260912820608000141650010000033191728969200", "1234"));

            var configuracao = new Mock<IFiscalConfigurationProvider>();
            configuracao.Setup(c => c.ObterConfiguracaoAsync())
                .ReturnsAsync(new FiscalConfiguration { TokenFocusNfe = "token-teste", UsarAmbienteProducao = false });

            var services = new ServiceCollection();
            services.AddScoped<IRequestTenant, ERP.Api.Services.RequestTenant>();
            services.AddLogging();
            services.AddSingleton(contingencia.Object);
            services.AddSingleton(nfce.Object);
            services.AddSingleton(new Mock<INfeEmissionService>().Object);
            services.AddSingleton(new Mock<ISaleService>().Object);
            services.AddSingleton(new Mock<IFiscalService>().Object);
            services.AddSingleton(configuracao.Object);
            if (interruptor is not null) services.AddSingleton(interruptor);
            if (batimento is not null) services.AddSingleton(batimento);

            var provedor = services.BuildServiceProvider();
            Worker = new NfeContingencyHostedService(provedor.GetRequiredService<IServiceScopeFactory>(), Logger);
        }

        public Guid TenantId { get; }
        public LoggerQueCaptura<NfeContingencyHostedService> Logger { get; } = new();
        public NfeContingencyHostedService Worker { get; }

        public Task Rodar() => Worker.ProcessarTenantAsync(TenantId, CancellationToken.None);
    }

    private static NfePendente Linha(string tipo) => new()
    {
        Id = Guid.NewGuid(),
        VendaId = Guid.NewGuid(),
        TipoNota = tipo,
        Estado = NfePendenteEstados.Ativa,
        PayloadJson = "{}",
        Referencia = $"ref-{Guid.NewGuid():N}",
        DataFalha = new DateTime(2026, 10, 8, 9, 0, 0)
    };

    [Fact(DisplayName = "AVISO: tenant habilitado, NFC-e deixada de fora e NENHUM batimento registrado no container: avisa uma vez")]
    public async Task SemBatimentoNoContainer_Avisa()
    {
        var tenant = Guid.NewGuid();
        var a = new Ambiente(tenant, new FiscalRecoverySwitch(tenant.ToString()), null, Linha("NFCE"));

        await a.Rodar();

        a.Logger.Avisos(Trecho).Should().Be(1);
    }

    [Fact(DisplayName = "AVISO: batimento criado ha mais que a carencia e que NUNCA bateu (o worker novo nao esta rodando): avisa")]
    public async Task BatimentoNuncaBateu_AposCarencia_Avisa()
    {
        var tenant = Guid.NewGuid();
        var relogio = new Relogio();
        var batimento = new FiscalRecoveryHeartbeat(relogio.Ler);
        relogio.Agora = relogio.Agora.AddMinutes(10);
        var a = new Ambiente(tenant, new FiscalRecoverySwitch(tenant.ToString()), batimento, Linha("NFCE"));

        await a.Rodar();

        a.Logger.Avisos(Trecho).Should().Be(1);
    }

    [Fact(DisplayName = "SEM AVISO: o worker da recuperacao bateu ha pouco (esta vivo)")]
    public async Task BatimentoRecente_NaoAvisa()
    {
        var tenant = Guid.NewGuid();
        var relogio = new Relogio();
        var batimento = new FiscalRecoveryHeartbeat(relogio.Ler);
        relogio.Agora = relogio.Agora.AddMinutes(10);
        batimento.RegistrarCiclo();
        relogio.Agora = relogio.Agora.AddSeconds(60);
        var a = new Ambiente(tenant, new FiscalRecoverySwitch(tenant.ToString()), batimento, Linha("NFCE"));

        await a.Rodar();

        a.Logger.Avisos(Trecho).Should().Be(0);
    }

    [Fact(DisplayName = "SEM AVISO: logo depois de uma subida (dentro da carencia), mesmo sem batimento ainda")]
    public async Task DentroDaCarencia_NaoAvisa()
    {
        var tenant = Guid.NewGuid();
        var relogio = new Relogio();
        var batimento = new FiscalRecoveryHeartbeat(relogio.Ler);
        relogio.Agora = relogio.Agora.Add(Carencia - TimeSpan.FromSeconds(30));
        var a = new Ambiente(tenant, new FiscalRecoverySwitch(tenant.ToString()), batimento, Linha("NFCE"));

        await a.Rodar();

        a.Logger.Avisos(Trecho).Should().Be(0);
    }

    [Fact(DisplayName = "AVISO DEDUPLICADO: duas passadas seguidas geram um unico aviso (no maximo um a cada 30 minutos)")]
    public async Task Deduplica()
    {
        var tenant = Guid.NewGuid();
        var a = new Ambiente(tenant, new FiscalRecoverySwitch(tenant.ToString()), null, Linha("NFCE"));

        await a.Rodar();
        await a.Rodar();
        await a.Rodar();

        a.Logger.Avisos(Trecho).Should().Be(1);
    }

    [Fact(DisplayName = "SEM AVISO: tenant NAO habilitado: a NFC-e segue com o worker antigo e nada e 'deixado de fora'")]
    public async Task TenantNaoHabilitado_NaoAvisa()
    {
        var a = new Ambiente(Guid.NewGuid(), new FiscalRecoverySwitch(Guid.NewGuid().ToString()), null, Linha("NFCE"));

        await a.Rodar();

        a.Logger.Avisos(Trecho).Should().Be(0);
    }

    [Fact(DisplayName = "SEM AVISO: tenant habilitado mas SO com NF-e na fila: nenhuma NFC-e foi deixada de fora")]
    public async Task SoNfe_NaoAvisa()
    {
        var tenant = Guid.NewGuid();
        var a = new Ambiente(tenant, new FiscalRecoverySwitch(tenant.ToString()), null, Linha("NFE"));

        await a.Rodar();

        a.Logger.Avisos(Trecho).Should().Be(0);
    }
}
