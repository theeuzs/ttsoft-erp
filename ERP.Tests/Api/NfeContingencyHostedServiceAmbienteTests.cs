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
/// Trava de ambiente, Estagio 2: o worker ANTIGO. Mesma regra do orquestrador (PendenciaAmbienteGuard), aplicada a cada pendencia, NFC-e e NF-e.
///
/// O QUE PROVA: que uma pendencia divergente, de origem desconhecida (NULL) ou cuja configuracao nao pode ser lida NAO chama nenhum servico de
/// emissao, NAO conta tentativa de falha, NAO e removida nem persistida e gera um aviso deduplicado; que a configuracao e RELIDA para CADA
/// pendencia (nada do contexto de uma nota anterior e reutilizado) e que a chamada usa o token e o ambiente DESSA releitura.
/// O QUE NAO PROVA: o provider real do banco (ver os testes de composicao) nem a rede.
/// </summary>
public class NfeContingencyHostedServiceAmbienteTests
{
    private const string Trecho = "ambiente fiscal incompativel";

    private static FiscalConfiguration Cfg(bool producao, string token = "token-teste") =>
        new() { TokenFocusNfe = token, UsarAmbienteProducao = producao };

    /// <summary>A configuracao fiscal "como o provider a devolveria" a cada leitura: roteirizada (uma configuracao ou uma excecao por leitura).</summary>
    private sealed class ConfigScriptada
    {
        private readonly Queue<object> _fila = new();

        public ConfigScriptada(FiscalConfiguration padrao) { Padrao = padrao; }

        public FiscalConfiguration Padrao { get; set; }

        public int Leituras { get; private set; }

        public ConfigScriptada Enfileirar(params object[] itens)
        {
            foreach (var item in itens) _fila.Enqueue(item);
            return this;
        }

        public Task<FiscalConfiguration> Proxima()
        {
            Leituras++;

            if (_fila.Count == 0)
                return Task.FromResult(Padrao);

            return _fila.Dequeue() switch
            {
                FiscalConfiguration c => Task.FromResult(c),
                Exception ex => Task.FromException<FiscalConfiguration>(ex),
                var outro => throw new InvalidOperationException($"Item invalido na fila da configuracao: {outro?.GetType().Name}.")
            };
        }
    }

    private sealed class Ambiente
    {
        public Ambiente(ConfigScriptada config, params NfePendente[] linhas)
        {
            TenantId = Guid.NewGuid();
            Config = config;
            foreach (var linha in linhas)
                linha.TenantId = TenantId;

            Contingencia.Setup(c => c.ObterNotasPendentesAsync()).ReturnsAsync(linhas.OrderBy(l => l.DataFalha).ToList());

            Nfce.Setup(s => s.EmitirNfceAsync(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((true, "Autorizada", "https://focus/danfe.html", "https://focus/nfce.xml", "41260912820608000141650010000033191728969200", "1234"));
            Nfe.Setup(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync((true, "Autorizada", "https://focus/danfe.html", "https://focus/nfe.xml", "41260912820608000141550010000033191728969200", "1234"));
            Configuracao.Setup(c => c.ObterConfiguracaoAsync()).Returns(() => config.Proxima());

            var services = new ServiceCollection();
            services.AddScoped<IRequestTenant, ERP.Api.Services.RequestTenant>();
            services.AddLogging();
            services.AddSingleton(Contingencia.Object);
            services.AddSingleton(Nfce.Object);
            services.AddSingleton(Nfe.Object);
            services.AddSingleton(Vendas.Object);
            services.AddSingleton(Fiscal.Object);
            services.AddSingleton(Configuracao.Object);

            Provedor = services.BuildServiceProvider();
            Worker = new NfeContingencyHostedService(Provedor.GetRequiredService<IServiceScopeFactory>(), Logger);
        }

        public Guid TenantId { get; }
        public ConfigScriptada Config { get; }
        public ServiceProvider Provedor { get; }
        public LoggerQueCaptura<NfeContingencyHostedService> Logger { get; } = new();
        public NfeContingencyHostedService Worker { get; }
        public Mock<INfeContingencyService> Contingencia { get; } = new();
        public Mock<INfceEmissionService> Nfce { get; } = new();
        public Mock<INfeEmissionService> Nfe { get; } = new();
        public Mock<ISaleService> Vendas { get; } = new();
        public Mock<IFiscalService> Fiscal { get; } = new();
        public Mock<IFiscalConfigurationProvider> Configuracao { get; } = new();

        public Task Rodar() => Worker.ProcessarTenantAsync(TenantId, CancellationToken.None);

        public void NenhumaEmissao()
        {
            Nfce.Verify(s => s.EmitirNfceAsync(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
            Nfe.Verify(s => s.EmitirNfeA4Async(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        /// <summary>Nada na fila mudou: nao e tentativa de falha, nao foi removida nem persistida, nao houve rejeicao.</summary>
        public void NadaMudouNaFila()
        {
            Contingencia.Verify(c => c.RegistrarFalhaTentativaAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never());
            Contingencia.Verify(c => c.RemoverNotaPendenteAsync(It.IsAny<Guid>()), Times.Never());
            Fiscal.Verify(f => f.PersistirEmissaoAutorizadaAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
            Vendas.Verify(v => v.AtualizarDadosNfceAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never());
        }
    }

    private static NfePendente Linha(string tipo, string referencia, bool? criadaEmProducao, int minutosAtras = 10) => new()
    {
        Id = Guid.NewGuid(),
        VendaId = Guid.NewGuid(),
        TipoNota = tipo,
        Estado = NfePendenteEstados.Ativa,
        PayloadJson = "{}",
        Referencia = referencia,
        DataFalha = new DateTime(2026, 10, 8, 12, 0, 0).AddMinutes(-minutosAtras),
        CriadaEmProducao = criadaEmProducao
    };

    // ═════════════════════════════════════════════════════════════════════
    //  Divergente / desconhecido: nada e emitido, nada muda
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "HOMOLOGACAO -> PRODUCAO: NFC-e e NF-e nascidas em homologacao, configuracao agora producao: NENHUMA emissao, nenhuma tentativa contada, aviso")]
    public async Task HomologacaoParaProducao_NaoEmite()
    {
        var a = new Ambiente(new ConfigScriptada(Cfg(producao: true)),
            Linha("NFCE", "ref-nfce", false), Linha("NFE", "ref-nfe", false));

        await a.Rodar();

        a.NenhumaEmissao();
        a.NadaMudouNaFila();
        a.Logger.Avisos(Trecho).Should().Be(1);
    }

    [Fact(DisplayName = "PRODUCAO -> HOMOLOGACAO: nenhuma emissao")]
    public async Task ProducaoParaHomologacao_NaoEmite()
    {
        var a = new Ambiente(new ConfigScriptada(Cfg(producao: false)),
            Linha("NFCE", "ref-nfce", true), Linha("NFE", "ref-nfe", true));

        await a.Rodar();

        a.NenhumaEmissao();
        a.NadaMudouNaFila();
        a.Logger.Avisos(Trecho).Should().Be(1);
    }

    [Theory(DisplayName = "ORIGEM DESCONHECIDA (NULL): bloqueada em qualquer ambiente configurado")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OrigemDesconhecida_NaoEmite(bool configuradoEmProducao)
    {
        var a = new Ambiente(new ConfigScriptada(Cfg(configuradoEmProducao)),
            Linha("NFCE", "ref-nfce", null), Linha("NFE", "ref-nfe", null));

        await a.Rodar();

        a.NenhumaEmissao();
        a.NadaMudouNaFila();
        a.Logger.Avisos(Trecho).Should().Be(1);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Compativel: usa a RELEITURA
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "COMPATIVEL: emite com o token e o ambiente da RELEITURA da pendencia, nao com os lidos no inicio do tenant; sem aviso")]
    public async Task Compativel_UsaATokenEOAmbienteDaReleitura()
    {
        var config = new ConfigScriptada(Cfg(false)).Enfileirar(Cfg(false, "token-do-inicio"), Cfg(false, "token-da-releitura"));
        var a = new Ambiente(config, Linha("NFCE", "ref-nfce", false));

        await a.Rodar();

        a.Nfce.Verify(s => s.EmitirNfceAsync("ref-nfce", It.IsAny<FocusNfceRequest>(), "token-da-releitura", false), Times.Once());
        a.Nfce.Verify(s => s.EmitirNfceAsync(It.IsAny<string>(), It.IsAny<FocusNfceRequest>(), "token-do-inicio", It.IsAny<bool>()), Times.Never());
        config.Leituras.Should().Be(2, "uma leitura para o tenant e uma para a pendencia");
        a.Logger.Avisos(Trecho).Should().Be(0);
    }

    [Fact(DisplayName = "RELEITURA POR PENDENCIA: a configuracao muda entre duas notas do mesmo ciclo; a primeira e emitida e a segunda e bloqueada")]
    public async Task ReleituraPorPendencia_NaoReutilizaOContextoDaNotaAnterior()
    {
        var config = new ConfigScriptada(Cfg(false)).Enfileirar(Cfg(false), Cfg(false), Cfg(true));   // tenant, nota 1, nota 2
        var a = new Ambiente(config,
            Linha("NFCE", "ref-1", false, minutosAtras: 20),
            Linha("NFCE", "ref-2", false, minutosAtras: 10));

        await a.Rodar();

        a.Nfce.Verify(s => s.EmitirNfceAsync("ref-1", It.IsAny<FocusNfceRequest>(), "token-teste", false), Times.Once());
        a.Nfce.Verify(s => s.EmitirNfceAsync("ref-2", It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        config.Leituras.Should().Be(3, "tenant + uma releitura por pendencia");
        a.Logger.Avisos(Trecho).Should().Be(1);
    }

    [Fact(DisplayName = "FALHA NA RELEITURA de uma nota: so ela e bloqueada (sem emitir e sem contar tentativa); a seguinte e processada")]
    public async Task FalhaNaReleitura_BloqueiaSoEssaNota()
    {
        var config = new ConfigScriptada(Cfg(false)).Enfileirar(Cfg(false), new InvalidOperationException("banco fora do ar"), Cfg(false));
        var a = new Ambiente(config,
            Linha("NFCE", "ref-1", false, minutosAtras: 20),
            Linha("NFCE", "ref-2", false, minutosAtras: 10));

        await a.Rodar();

        a.Nfce.Verify(s => s.EmitirNfceAsync("ref-1", It.IsAny<FocusNfceRequest>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        a.Nfce.Verify(s => s.EmitirNfceAsync("ref-2", It.IsAny<FocusNfceRequest>(), "token-teste", false), Times.Once());
        a.Contingencia.Verify(c => c.RegistrarFalhaTentativaAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never(),
            "falha de leitura da configuracao nao e falha de comunicacao com a SEFAZ");
        a.Logger.Avisos(Trecho).Should().Be(1);
    }

    [Fact(DisplayName = "Token em branco na releitura: a nota nao e tentada e nao conta falha")]
    public async Task TokenEmBrancoNaReleitura_NaoEmite()
    {
        var config = new ConfigScriptada(Cfg(false)).Enfileirar(Cfg(false), Cfg(false, token: ""));
        var a = new Ambiente(config, Linha("NFCE", "ref-1", false));

        await a.Rodar();

        a.NenhumaEmissao();
        a.NadaMudouNaFila();
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Aviso deduplicado
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "AVISO DEDUPLICADO: tres passadas seguidas geram um unico aviso por tenant (no maximo um a cada 30 minutos)")]
    public async Task Aviso_Deduplicado()
    {
        var a = new Ambiente(new ConfigScriptada(Cfg(producao: true)), Linha("NFCE", "ref-nfce", false));

        await a.Rodar();
        await a.Rodar();
        await a.Rodar();

        a.NenhumaEmissao();
        a.Logger.Avisos(Trecho).Should().Be(1);
    }
}
