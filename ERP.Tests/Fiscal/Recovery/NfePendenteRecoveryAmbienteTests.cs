using System.Linq.Expressions;
using ERP.Application.Fiscal;
using ERP.Application.Fiscal.Recovery;
using ERP.Application.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// Trava de ambiente, Estagio 2: o ORQUESTRADOR. Orquestrador real, store real (SQLite NoTracking), Focus roteirizada; o unico elemento
/// roteirizado alem da Focus e o leitor "ambiente configurado agora" (o que o worker passa no contexto).
///
/// O QUE PROVA: que uma pendencia so e consultada e reenviada no ambiente em que nasceu; que a configuracao e RELIDA imediatamente antes de CADA
/// GET e de CADA POST (inclusive o POST depois da regeneracao e o GET da reconciliacao), sem reutilizar leitura anterior; que divergencia,
/// origem desconhecida e falha de leitura nao fazem NENHUMA chamada a Focus, nao contam tentativa, preservam os contadores e deixam a pendencia em
/// AguardandoCorrecao com motivo claro; que uma linha divergente nao afeta uma compativel no mesmo ciclo; que a pendencia retoma quando o ambiente
/// volta a bater. O QUE NAO PROVA: o provider real nem o worker (ver NfeRecoveryHostedService*Tests) e SQL Server.
/// </summary>
public sealed class NfePendenteRecoveryAmbienteTests
{
    private static readonly DateTimeOffset Agora0 = new(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);

    private static readonly RecoveryPolicyOptions D8Ligada =
        new() { PermitirRegeneracaoDataEmissao = true, JanelaMaximaRegeneracaoHoras = 6 };

    private static readonly PersistenciaAutorizacaoResultado Completo = new(true, true, true);

    private static Expression<Func<IFiscalService, Task<PersistenciaAutorizacaoResultado>>> QualquerPersistir() =>
        f => f.PersistirEmissaoAutorizadaComResultadoAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>());

    private static Expression<Func<ISaleService, Task>> QualquerRejeitar() =>
        s => s.AtualizarDadosNfceAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>());

    private sealed class Cenario : IDisposable
    {
        public Cenario()
        {
            Focus = new FocusRoteirizado(Eventos);
            Fiscal.Setup(QualquerPersistir()).Returns(Task.FromResult(Completo));
            Vendas.Setup(QualquerRejeitar()).Returns(Task.CompletedTask);
        }

        public RecoveryAmbiente Amb { get; } = new();
        public List<string> Eventos { get; } = new();
        public RelogioDeTeste Relogio { get; } = new(Agora0);
        public FocusRoteirizado Focus { get; }
        public Mock<IFiscalService> Fiscal { get; } = new();
        public Mock<ISaleService> Vendas { get; } = new();
        public RecoveryPolicyOptions Opcoes { get; set; } = new();

        /// <summary>O ambiente que a configuracao do tenant tem "agora" (e quantas vezes foi lida). Padrao: producao.</summary>
        public AmbienteLido Ambiente { get; set; } = new(true);

        /// <summary>O ambiente que as chamadas do ciclo usariam (host e token do contexto, montado no inicio do ciclo). Padrao: producao.</summary>
        public bool ChamadaEmProducao { get; set; } = true;

        public RecoveryTenantContext Contexto => Ambiente.Contexto("token-de-teste", ChamadaEmProducao);

        public async Task<RecoveryCycleResult> CicloAsync(RecoveryTenantContext? contexto = null, CancellationToken ct = default)
        {
            using var alvo = Amb.NovaStore();
            var orquestrador = new NfePendenteRecoveryOrchestrator(alvo.Store, Focus, Fiscal.Object, Vendas.Object, Opcoes, Relogio.Ler);

            return await orquestrador.ProcessarTenantAsync(contexto ?? Contexto, ct);
        }

        public IEnumerable<string> Metodos => Focus.Chamadas.Select(x => x.Metodo);

        public void Dispose() => Amb.Dispose();
    }

    /// <summary>A pendencia foi bloqueada de forma segura: AguardandoCorrecao, +30 min, sem tentativa de POST, e o motivo certo.</summary>
    private static void FoiBloqueada(Cenario c, Guid id, string comecoDoMotivo)
    {
        var n = c.Amb.Ler(id);

        n.Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        n.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 30, 0));
        n.TentativasPost.Should().Be(0, "nenhum POST foi tentado");
        n.UltimaDecisao.Should().Contain(comecoDoMotivo);
        n.UltimaDecisao!.Length.Should().BeLessThanOrEqualTo(FiscalRecoveryStore.TamanhoMaximoUltimaDecisao);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Os quatro casos pedidos: nenhuma chamada a Focus, nenhuma tentativa contada
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "HOMOLOGACAO -> PRODUCAO: ZERO chamadas a Focus, AguardandoCorrecao +30 min, tentativa NAO contada, contadores preservados")]
    public async Task HomologacaoParaProducao_NaoChamaAFocus()
    {
        using var c = new Cenario();   // chamada e configuracao: producao
        var id = c.Amb.Semear(n => { n.CriadaEmProducao = false; n.FalhasTransitoriasSeguidas = 2; n.FalhasDesconhecidasSeguidas = 1; });

        var r = await c.CicloAsync();

        c.Focus.Chamadas.Should().BeEmpty();
        r.AmbienteDivergente.Should().Be(1);
        r.ErrosDeProcessamento.Should().Be(0, "a divergencia nao e erro de processamento");
        r.FalhasDePersistencia.Should().Be(0);
        r.Autorizadas.Should().Be(0);

        FoiBloqueada(c, id, "AmbienteDivergente:");
        var n = c.Amb.Ler(id);
        n.UltimaDecisao.Should().StartWith("AmbienteDivergente:").And.Contain("pendencia criada em Homologacao").And.Contain("configurado agora: Producao");
        n.TentativasConsulta.Should().Be(0, "uma chamada bloqueada nao e tentativa");
        n.UltimaConsultaEm.Should().BeNull();
        n.UltimoPostEm.Should().BeNull();
        n.FalhasTransitoriasSeguidas.Should().Be(2);
        n.FalhasDesconhecidasSeguidas.Should().Be(1);
        n.PayloadJson.Should().Be(RecoveryFixtures.PayloadNfce, "nada foi regenerado nem regravado");
    }

    [Fact(DisplayName = "PRODUCAO -> HOMOLOGACAO: ZERO chamadas a Focus, AguardandoCorrecao +30 min")]
    public async Task ProducaoParaHomologacao_NaoChamaAFocus()
    {
        using var c = new Cenario { Ambiente = new AmbienteLido(false), ChamadaEmProducao = false };
        var id = c.Amb.Semear(n => n.CriadaEmProducao = true);

        var r = await c.CicloAsync();

        c.Focus.Chamadas.Should().BeEmpty();
        r.AmbienteDivergente.Should().Be(1);
        FoiBloqueada(c, id, "AmbienteDivergente:");
        c.Amb.Ler(id).UltimaDecisao.Should().Contain("pendencia criada em Producao").And.Contain("configurado agora: Homologacao");
    }

    [Theory(DisplayName = "COMPATIVEL: chama a Focus uma unica vez, no ambiente da pendencia, e le o ambiente uma unica vez antes do GET")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Compativel_ChamaAFocus(bool producao)
    {
        using var c = new Cenario { Ambiente = new AmbienteLido(producao), ChamadaEmProducao = producao };
        c.Amb.Semear(n => n.CriadaEmProducao = producao);
        c.Focus.EnfileirarGet(Resp.Autorizada());

        var r = await c.CicloAsync();

        c.Focus.Chamadas.Should().ContainSingle().Which.IsProducao.Should().Be(producao);
        r.Autorizadas.Should().Be(1);
        r.AmbienteDivergente.Should().Be(0);
        c.Ambiente.Leituras.Should().Be(1, "uma leitura imediatamente antes do unico GET");
    }

    [Fact(DisplayName = "ORIGEM DESCONHECIDA (NULL): ZERO chamadas, AguardandoCorrecao pedindo classificacao manual")]
    public async Task OrigemDesconhecida_NaoChamaAFocus()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear(n => n.CriadaEmProducao = null);

        var r = await c.CicloAsync();

        c.Focus.Chamadas.Should().BeEmpty();
        r.AmbienteDivergente.Should().Be(1);
        FoiBloqueada(c, id, "AmbienteDesconhecido:");
        c.Amb.Ler(id).UltimaDecisao.Should().Contain("classificar manualmente");
    }

    // ═════════════════════════════════════════════════════════════════════
    //  A configuracao muda ENTRE as operacoes
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "A configuracao muda DEPOIS do GET: a regeneracao e bloqueada ANTES de gravar o payload novo; nenhum POST")]
    public async Task ConfiguracaoMudaDepoisDoGet_BloqueiaAntesDeRegravar()
    {
        using var c = new Cenario { Opcoes = D8Ligada, Ambiente = new AmbienteLido(true).Enfileirar(true, false) };
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Autorizada());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET");
        r.AmbienteDivergente.Should().Be(1);
        c.Ambiente.Leituras.Should().Be(2, "antes do GET e antes de regenerar");
        FoiBloqueada(c, id, "AmbienteDivergente:");

        var n = c.Amb.Ler(id);
        n.PayloadJson.Should().Be(RecoveryFixtures.PayloadNfce, "o pre-voo bloqueia ANTES de qualquer gravacao do payload");
        n.TentativasConsulta.Should().Be(1, "o GET aconteceu e conta");
    }

    [Fact(DisplayName = "A configuracao muda IMEDIATAMENTE ANTES DO POST (depois do pre-voo): o POST e BLOQUEADO; so houve o GET")]
    public async Task ConfiguracaoMudaImediatamenteAntesDoPost_BloqueiaOPost()
    {
        using var c = new Cenario { Opcoes = D8Ligada, Ambiente = new AmbienteLido(true).Enfileirar(true, true, false) };
        var id = c.Amb.Semear(n => { n.FalhasTransitoriasSeguidas = 1; n.FalhasDesconhecidasSeguidas = 1; });
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Autorizada());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET");
        c.Focus.Chamadas.Should().NotContain(x => x.Metodo == "POST");
        r.AmbienteDivergente.Should().Be(1);
        r.Autorizadas.Should().Be(0);
        c.Ambiente.Leituras.Should().Be(3, "GET, pre-voo da regeneracao e POST: uma leitura nova para cada");
        c.Fiscal.Verify(QualquerPersistir(), Times.Never());
        FoiBloqueada(c, id, "AmbienteDivergente:");

        var n = c.Amb.Ler(id);
        n.UltimaDecisao.Should().Contain("DataEmissao original=", "a regeneracao ja tinha gravado a data nova antes da guarda final");
        n.PayloadJson.Should().NotBe(RecoveryFixtures.PayloadNfce, "documentado: o payload ja tinha sido regenerado; o POST e que nao saiu");
        n.TentativasConsulta.Should().Be(1);
        n.TentativasPost.Should().Be(0, "o POST nao saiu e nao e contado");
        n.UltimoPostEm.Should().BeNull();
        n.FalhasTransitoriasSeguidas.Should().Be(0, "os contadores gravados pela decisao do GET (404 conhecido) sao os que ficam");
        n.FalhasDesconhecidasSeguidas.Should().Be(0, "a divergencia nao restaura os contadores de ANTES da gravacao do GET");
    }

    [Fact(DisplayName = "A RECONCILIACAO tambem e guardada: o GET extra depois de already_processed e bloqueado quando a configuracao muda")]
    public async Task Reconciliacao_GetExtraEBloqueado()
    {
        using var c = new Cenario { Opcoes = D8Ligada, Ambiente = new AmbienteLido(true).Enfileirar(true, true, true, false) };
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado(), Resp.Autorizada());
        c.Focus.EnfileirarPost(Resp.JaProcessado());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET", "POST");   // o segundo GET (reconciliacao) NAO saiu
        r.Autorizadas.Should().Be(0);
        r.AmbienteDivergente.Should().Be(1);
        c.Ambiente.Leituras.Should().Be(4, "GET, pre-voo, POST e GET da reconciliacao");
        c.Amb.Ler(id).TentativasPost.Should().Be(1, "o POST que realmente saiu e contado");
        c.Amb.Ler(id).Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
    }

    [Fact(DisplayName = "RELEITURA REAL: um fluxo completo (GET 404, regeneracao, POST) le o ambiente 3 vezes, uma antes de cada operacao")]
    public async Task ReleituraReal_UmaPorOperacao()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Autorizada());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET", "POST");
        r.Autorizadas.Should().Be(1);
        c.Ambiente.Leituras.Should().Be(3);
    }

    [Fact(DisplayName = "CONTEXTO VELHO: pendencia de homologacao, configuracao relida = homologacao, mas a chamada usaria PRODUCAO: BLOQUEIA (zero chamadas)")]
    public async Task ContextoVelho_Bloqueia()
    {
        using var c = new Cenario { Ambiente = new AmbienteLido(false), ChamadaEmProducao = true };
        var id = c.Amb.Semear(n => n.CriadaEmProducao = false);

        var r = await c.CicloAsync();

        c.Focus.Chamadas.Should().BeEmpty("uma comparacao de dois valores deixaria passar este caso e chamaria a PRODUCAO");
        r.AmbienteDivergente.Should().Be(1);
        FoiBloqueada(c, id, "AmbienteDivergente:");
        c.Amb.Ler(id).UltimaDecisao.Should().Contain("a chamada usaria Producao");
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Falha fechada
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "FALHA NA LEITURA antes do GET: ZERO chamadas, AmbienteIndeterminado, nao e erro de processamento")]
    public async Task FalhaNaLeitura_AntesDoGet_Bloqueia()
    {
        using var c = new Cenario { Ambiente = new AmbienteLido(true).Enfileirar(new InvalidOperationException("banco fora do ar")) };
        var id = c.Amb.Semear();

        var r = await c.CicloAsync();

        c.Focus.Chamadas.Should().BeEmpty();
        r.AmbienteDivergente.Should().Be(1);
        r.ErrosDeProcessamento.Should().Be(0);
        r.FalhasDePersistencia.Should().Be(0);
        FoiBloqueada(c, id, "AmbienteIndeterminado:");
    }

    [Fact(DisplayName = "FALHA NA LEITURA antes do POST: o POST NAO sai (so o GET)")]
    public async Task FalhaNaLeitura_AntesDoPost_BloqueiaOPost()
    {
        using var c = new Cenario
        {
            Opcoes = D8Ligada,
            Ambiente = new AmbienteLido(true).Enfileirar(true, true, new TimeoutException("leitura expirou"))
        };
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Autorizada());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET");
        r.AmbienteDivergente.Should().Be(1);
        c.Amb.Ler(id).UltimaDecisao.Should().Contain("AmbienteIndeterminado:");
    }

    [Fact(DisplayName = "O leitor de ambiente e OBRIGATORIO: nao existe contexto sem ele")]
    public void ContextoSemLeitor_NaoExiste()
    {
        Action act = () => _ = new RecoveryTenantContext("token", true, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact(DisplayName = "CANCELAMENTO durante a releitura: propaga, nao vira divergencia nem erro, nenhuma chamada e a linha fica intacta")]
    public async Task CancelamentoNaLeitura_Propaga()
    {
        using var c = new Cenario();
        using var cts = new CancellationTokenSource();
        var id = c.Amb.Semear();
        var contexto = new RecoveryTenantContext("token-de-teste", true, ct =>
        {
            cts.Cancel();
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        });

        Func<Task> act = () => c.CicloAsync(contexto, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        c.Focus.Chamadas.Should().BeEmpty();
        var n = c.Amb.Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.Ativa);
        n.ProximaTentativaEm.Should().BeNull();
        n.TentativasConsulta.Should().Be(0);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Isolamento e retomada
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Uma linha divergente e uma compativel no MESMO ciclo: so a compativel chama a Focus")]
    public async Task LinhaDivergenteELinhaCompativel_NoMesmoCiclo()
    {
        using var c = new Cenario();   // producao
        var divergente = c.Amb.Semear(n => n.CriadaEmProducao = false);
        var compativel = c.Amb.Semear(n => n.CriadaEmProducao = true);
        var refCompativel = c.Amb.Ler(compativel).Referencia;
        c.Focus.EnfileirarGet(Resp.Autorizada());

        var r = await c.CicloAsync();

        c.Focus.Chamadas.Should().ContainSingle().Which.Referencia.Should().Be(refCompativel);
        r.Autorizadas.Should().Be(1);
        r.AmbienteDivergente.Should().Be(1);
        c.Amb.Visivel(compativel).Should().BeFalse("a compativel foi autorizada e removida");
        c.Amb.Ler(divergente).Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
    }

    [Fact(DisplayName = "RETOMADA: so depois de o ambiente voltar a bater e a agenda vencer a pendencia e consultada de novo")]
    public async Task Retomada_SoDepoisDeConfirmarOMesmoAmbiente()
    {
        using var c = new Cenario();   // configuracao e chamada em producao; a pendencia nasceu em homologacao
        var id = c.Amb.Semear(n => n.CriadaEmProducao = false);

        (await c.CicloAsync()).AmbienteDivergente.Should().Be(1);
        c.Focus.Chamadas.Should().BeEmpty();

        // O ambiente volta a bater, mas a agenda ainda nao venceu: nada acontece.
        c.Ambiente = new AmbienteLido(false);
        c.ChamadaEmProducao = false;
        var semAgenda = await c.CicloAsync();
        semAgenda.Elegiveis.Should().Be(0);
        c.Focus.Chamadas.Should().BeEmpty();

        // Agenda vencida e ambiente compativel: retoma.
        c.Relogio.Agora = Agora0.AddMinutes(31);
        c.Focus.EnfileirarGet(Resp.Autorizada());
        var retomada = await c.CicloAsync();

        retomada.Elegiveis.Should().Be(1);
        retomada.Autorizadas.Should().Be(1);
        c.Focus.Chamadas.Should().ContainSingle().Which.IsProducao.Should().BeFalse();
        c.Amb.Visivel(id).Should().BeFalse();
    }

    [Fact(DisplayName = "Agenda vencida mas o ambiente CONTINUA divergente: a pendencia nao e consultada e e reagendada")]
    public async Task AgendaVencida_AmbienteAindaDivergente_ReagendaSemChamar()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear(n => n.CriadaEmProducao = false);
        await c.CicloAsync();

        c.Relogio.Agora = Agora0.AddMinutes(31);
        var r = await c.CicloAsync();

        c.Focus.Chamadas.Should().BeEmpty();
        r.Elegiveis.Should().Be(1);
        r.AmbienteDivergente.Should().Be(1);
        c.Amb.Ler(id).ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 16, 1, 0));
    }
}
