using System.Net;
using System.Net.Http;
using System.Text;
using ERP.Application.Fiscal;
using ERP.Application.Fiscal.Recovery;
using ERP.Application.Interfaces;
using ERP.Infrastructure.HttpClients;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// 4A-6c-0: o registro das tentativas visto de DENTRO da recuperacao, com o orquestrador REAL, a store REAL (SQLite) e o FocusReferenceClient REAL
/// (so o HttpMessageHandler e falso). O QUE PROVA: (1) uma pendencia bloqueada pela guarda de ambiente NAO gera nenhuma linha do cliente e nenhuma
/// chamada ao handler (a evidencia direta que o piloto vai procurar); (2) uma pendencia compativel gera exatamente 'iniciada' e 'concluida' com o status
/// e a referencia da linha, e nenhum POST; (3) as linhas do cliente herdam o PendenciaId (correlacao via LogContext).
/// O QUE NAO PROVA: a configuracao de logs de producao (Serilog em Information), nem os outros clientes da Focus (que nao sao instrumentados).
/// </summary>
public sealed class NfePendenteRecoveryInstrumentacaoTests
{
    private static readonly DateTimeOffset Agora0 = new(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);

    private static readonly PersistenciaAutorizacaoResultado Completo = new(true, true, true);

    private sealed class SinkDeTeste : ILogEventSink
    {
        private readonly object _trava = new();

        public List<LogEvent> Eventos { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            lock (_trava)
                Eventos.Add(logEvent);
        }
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Metodos { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Metodos.Add(request.Method.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent(FocusFixtures.NaoEncontrado, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class Cenario : IDisposable
    {
        public Cenario(ILogger<FocusReferenceClient> loggerDoCliente)
        {
            Cliente = new FocusReferenceClient(new HttpClient(Handler), loggerDoCliente);
            Fiscal.Setup(f => f.PersistirEmissaoAutorizadaComResultadoAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(Completo));
        }

        public RecoveryAmbiente Amb { get; } = new();
        public RelogioDeTeste Relogio { get; } = new(Agora0);
        public Handler Handler { get; } = new();
        public FocusReferenceClient Cliente { get; }
        public Mock<IFiscalService> Fiscal { get; } = new();
        public Mock<ISaleService> Vendas { get; } = new();

        public async Task<RecoveryCycleResult> CicloAsync(RecoveryTenantContext contexto)
        {
            using var alvo = Amb.NovaStore();
            var orquestrador = new NfePendenteRecoveryOrchestrator(
                alvo.Store, Cliente, Fiscal.Object, Vendas.Object, new RecoveryPolicyOptions(), Relogio.Ler);

            return await orquestrador.ProcessarTenantAsync(contexto);
        }

        public void Dispose() => Amb.Dispose();
    }

    [Fact(DisplayName = "BLOQUEADA PELA GUARDA: nenhuma linha do cliente da Focus e nenhuma chamada ao handler (a evidencia direta do piloto)")]
    public async Task LinhaBloqueada_NaoGeraNenhumEventoDoCliente()
    {
        var logCliente = new LoggerQueCapturaEstado<FocusReferenceClient>();
        using var c = new Cenario(logCliente);
        var origemProducao = c.Amb.Semear(n => n.CriadaEmProducao = true);          // nasceu em producao
        var origemDesconhecida = c.Amb.Semear(n => n.CriadaEmProducao = null);       // sem origem registrada

        var r = await c.CicloAsync(AmbienteLido.Fixo("token-de-teste", false));      // o tenant esta em homologacao

        r.BloqueadasPorAmbiente.Should().Be(2);
        logCliente.Entradas.Should().BeEmpty("a guarda bloqueia ANTES de qualquer tentativa do cliente");
        c.Handler.Metodos.Should().BeEmpty();
        c.Amb.Ler(origemProducao).Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        c.Amb.Ler(origemDesconhecida).Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
    }

    [Fact(DisplayName = "COMPATIVEL: exatamente 'iniciada' e 'concluida' (status 404, host de homologacao, referencia da linha) e NENHUM POST")]
    public async Task LinhaCompativel_GeraIniciadaEConcluida()
    {
        var logCliente = new LoggerQueCapturaEstado<FocusReferenceClient>();
        using var c = new Cenario(logCliente);
        var id = c.Amb.Semear(n => n.CriadaEmProducao = false);
        var referencia = c.Amb.Ler(id).Referencia;

        var r = await c.CicloAsync(AmbienteLido.Fixo("token-de-teste", false));

        r.BloqueadasPorAmbiente.Should().Be(0);
        logCliente.Entradas.Should().HaveCount(2);
        logCliente.Entradas[0].Template.Should().StartWith("Focus tentativa iniciada");
        logCliente.Entradas[1].Template.Should().StartWith("Focus tentativa concluida");
        foreach (var e in logCliente.Entradas)
        {
            e.Propriedades["Referencia"].Should().Be(referencia);
            e.Propriedades["Host"].Should().Be("homologacao.focusnfe.com.br");
            e.Propriedades["Ambiente"].Should().Be("homologacao");
            e.Propriedades["Operacao"].Should().Be("GET");
        }

        logCliente.Entradas[1].Propriedades["StatusHttp"].Should().Be(404);
        c.Handler.Metodos.Should().Equal("GET");
        c.Amb.Ler(id).TentativasPost.Should().Be(0);
        c.Amb.Ler(id).TentativasConsulta.Should().Be(1);
    }

    [Fact(DisplayName = "CORRELACAO: as linhas do cliente herdam o PendenciaId do LogContext aberto pelo orquestrador (e carregam a Referencia)")]
    public async Task LinhasDoCliente_HerdamOPendenciaId()
    {
        var sink = new SinkDeTeste();
        using var logger = new LoggerConfiguration().MinimumLevel.Verbose().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        using var fabrica = new SerilogLoggerFactory(logger);
        using var c = new Cenario(fabrica.CreateLogger<FocusReferenceClient>());
        var id = c.Amb.Semear(n => n.CriadaEmProducao = false);
        var referencia = c.Amb.Ler(id).Referencia;

        await c.CicloAsync(AmbienteLido.Fixo("token-de-teste", false));

        var eventos = sink.Eventos.Where(e => e.MessageTemplate.Text.StartsWith("Focus tentativa")).ToList();
        eventos.Should().HaveCount(2);
        foreach (var evento in eventos)
        {
            ((ScalarValue)evento.Properties["PendenciaId"]).Value.Should().Be(id);
            ((ScalarValue)evento.Properties["Referencia"]).Value.Should().Be(referencia);
        }
    }
}
