using System.Globalization;
using System.Linq.Expressions;
using ERP.Application.Fiscal.Focus;
using ERP.Application.Fiscal.Recovery;
using ERP.Application.Interfaces;
using ERP.Domain.Common;
using FluentAssertions;
using Moq;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// 4A-5d: o orquestrador, com a store REAL sobre SQLite (NoTracking, como a producao), a Focus
/// roteirizada e IFiscalService / ISaleService como Moq (verificados pelos argumentos exatos; a
/// persistencia real deles ja tem testes proprios). O estado final e sempre lido por contexto novo.
/// Nenhum teste chama a Focus.
/// </summary>
public sealed class NfePendenteRecoveryOrchestratorTests
{
    private static readonly DateTimeOffset Agora0 = new(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);

    // Calculada pelo MESMO caminho que a producao usa (fuso do helper), para nao depender do banco de fusos da maquina.
    private static readonly string DataNova =
        TimeZoneInfo.ConvertTime(Agora0, FusoBrasilHelper.FusoBrasil)
            .ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    private static readonly RecoveryPolicyOptions D8Ligada =
        new() { PermitirRegeneracaoDataEmissao = true, JanelaMaximaRegeneracaoHoras = 6 };

    private const string BodyAutorizadoSemChave = "{\"status\":\"autorizado\",\"status_sefaz\":\"100\",\"numero\":\"3322\",\"caminho_danfe\":\"/x.html\"}";
    private const string BodyAutorizadoSemDanfe = "{\"status\":\"autorizado\",\"status_sefaz\":\"100\",\"chave_nfe\":\"NFe41260912820608000141650010000033221640357603\"}";

    // ── Cenario ──────────────────────────────────────────────────────────

    private static Expression<Func<IFiscalService, Task>> QualquerPersistir() =>
        f => f.PersistirEmissaoAutorizadaAsync(
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

            Fiscal.Setup(QualquerPersistir()).Callback(() => Eventos.Add("Fiscal.Persistir")).Returns(Task.CompletedTask);
            Vendas.Setup(QualquerRejeitar()).Callback(() => Eventos.Add("Vendas.Rejeitar")).Returns(Task.CompletedTask);
        }

        public RecoveryAmbiente Amb { get; } = new();
        public List<string> Eventos { get; } = new();
        public RelogioDeTeste Relogio { get; } = new(Agora0);
        public FocusRoteirizado Focus { get; }
        public Mock<IFiscalService> Fiscal { get; } = new();
        public Mock<ISaleService> Vendas { get; } = new();
        public RecoveryPolicyOptions Opcoes { get; set; } = new();
        public RecoveryTenantContext Contexto { get; set; } = new("token-de-teste", true);
        public Dictionary<string, Exception> Falhas { get; } = new();
        public Dictionary<string, Func<Task>> Antes { get; } = new();

        /// <summary>Um ciclo com um orquestrador NOVO e uma store NOVA (o estado so pode vir do banco).</summary>
        public async Task<RecoveryCycleResult> CicloAsync(CancellationToken ct = default)
        {
            using var alvo = Amb.NovaStore();
            var store = new StoreRegistradora(alvo.Store, Eventos, Falhas, Antes);
            var orquestrador = new NfePendenteRecoveryOrchestrator(store, Focus, Fiscal.Object, Vendas.Object, Opcoes, Relogio.Ler);

            return await orquestrador.ProcessarTenantAsync(Contexto, ct);
        }

        public IEnumerable<string> Metodos => Focus.Chamadas.Select(x => x.Metodo);

        public void Dispose() => Amb.Dispose();
    }

    private static void VerificarPersistiu(Cenario c, ERP.Domain.Entities.NfePendente nota, int vezes = 1, bool producao = true)
    {
        var r = Resp.Autorizada();
        var host = producao ? "https://api.focusnfe.com.br" : "https://homologacao.focusnfe.com.br";
        var ambiente = producao ? "Produção" : "Homologação";

        c.Fiscal.Verify(f => f.PersistirEmissaoAutorizadaAsync(
            nota.VendaId, "NFCE", host + r.CaminhoDanfe, ambiente, nota.Referencia,
            host + r.CaminhoXmlNotaFiscal, r.ChaveNfe!, r.Numero!), Times.Exactly(vezes));
    }

    private static void NadaFoiPersistido(Cenario c) => c.Fiscal.Verify(QualquerPersistir(), Times.Never());

    private static void NenhumaVendaMarcada(Cenario c) => c.Vendas.Verify(QualquerRejeitar(), Times.Never());

    // ═════════════════════════════════════════════════════════════════════
    //  Caminhos principais
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "GET autorizado: persiste com os argumentos exatos, depois remove; zero POST; ordem das chamadas")]
    public async Task GetAutorizado_PersisteERemove()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        var nota = c.Amb.Ler(id);
        c.Focus.EnfileirarGet(Resp.Autorizada());

        var r = await c.CicloAsync();

        r.Autorizadas.Should().Be(1);
        VerificarPersistiu(c, nota);
        c.Amb.Visivel(id).Should().BeFalse();
        c.Metodos.Should().Equal("GET");
        c.Eventos.Should().Equal("Store.Obter", "Focus.GET", "Fiscal.Persistir", "Store.Remover");
        c.Focus.Chamadas.Single().Referencia.Should().Be(nota.Referencia);
        c.Focus.Chamadas.Single().Token.Should().Be("token-de-teste");
    }

    [Fact(DisplayName = "Homologacao: URLs com o host e o nome de ambiente de homologacao")]
    public async Task Homologacao_HostEAmbiente()
    {
        using var c = new Cenario { Contexto = new RecoveryTenantContext("token-de-teste", false) };
        var id = c.Amb.Semear();
        var nota = c.Amb.Ler(id);
        c.Focus.EnfileirarGet(Resp.Autorizada());

        await c.CicloAsync();

        VerificarPersistiu(c, nota, producao: false);
        c.Focus.Chamadas.Single().IsProducao.Should().BeFalse();
    }

    [Fact(DisplayName = "GET processando: agenda +2 min, conta a consulta, zero POST, nada persistido nem marcado")]
    public async Task GetProcessando_Agenda()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        var antes = c.Amb.Ler(id);
        c.Focus.EnfileirarGet(Resp.Processando());

        var r = await c.CicloAsync();

        r.Agendadas.Should().Be(1);
        var n = c.Amb.Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.Ativa);
        n.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 2, 0));
        n.UltimaConsultaEm.Should().Be(new DateTime(2026, 10, 8, 15, 0, 0));
        n.TentativasConsulta.Should().Be(1);
        n.TentativasPost.Should().Be(0);
        n.UltimaDecisao.Should().StartWith("Processando");
        n.PayloadJson.Should().Be(antes.PayloadJson);
        c.Metodos.Should().Equal("GET");
        NadaFoiPersistido(c);
        NenhumaVendaMarcada(c);
    }

    [Fact(DisplayName = "GET 404 com a D8 desligada (padrao): IntervencaoManual, zero POST, payload intacto")]
    public async Task Get404_D8Desligada()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());

        var r = await c.CicloAsync();

        r.IntervencaoManual.Should().Be(1);
        var n = c.Amb.Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.IntervencaoManual);
        n.UltimaDecisao.Should().Contain("Desligada");
        n.PayloadJson.Should().Be(RecoveryFixtures.PayloadNfce);
        c.Metodos.Should().Equal("GET");
    }

    [Fact(DisplayName = "GET 404 com a D8 ligada: payload gravado ANTES do POST, mesma ref, so DataEmissao muda, ordem exata")]
    public async Task Get404_D8Ligada_RegravaAntesDoPost()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        var nota = c.Amb.Ler(id);
        string? payloadNoBancoNoInstanteDoPost = null;
        c.Focus.AoPostar = _ =>
        {
            payloadNoBancoNoInstanteDoPost = c.Amb.Ler(id).PayloadJson;
            return Task.CompletedTask;
        };
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Autorizada());

        var r = await c.CicloAsync();

        var payloadEsperado = PayloadDataEmissao.Substituir(RecoveryFixtures.PayloadNfce, DataNova);
        payloadNoBancoNoInstanteDoPost.Should().Be(payloadEsperado, "o payload novo tem que estar no banco quando o POST sai");

        var post = c.Focus.Chamadas.Single(x => x.Metodo == "POST");
        post.Referencia.Should().Be(nota.Referencia, "o POST usa a MESMA ref do GET");
        post.Corpo.Should().Be(NfceCorpoDeEnvio.Montar(payloadEsperado));

        c.Eventos.Should().Equal(
            "Store.Obter", "Focus.GET", "Store.Aplicar", "Store.Regravar", "Focus.POST", "Fiscal.Persistir", "Store.Remover");
        r.Autorizadas.Should().Be(1);
        VerificarPersistiu(c, nota);
        c.Amb.Visivel(id).Should().BeFalse();
    }

    [Fact(DisplayName = "POST com timeout: a data ORIGINAL e a NOVA ficam em UltimaDecisao; payload novo gravado; contadores e agenda")]
    public async Task PostTimeout_PreservaDatas_EContadores()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Timeout());

        var r = await c.CicloAsync();

        r.Agendadas.Should().Be(1);
        var n = c.Amb.Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.Ativa);
        n.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 2, 0));
        n.FalhasTransitoriasSeguidas.Should().Be(1);
        n.FalhasDesconhecidasSeguidas.Should().Be(0);
        n.TentativasConsulta.Should().Be(1);
        n.TentativasPost.Should().Be(1);
        n.UltimaConsultaEm.Should().Be(new DateTime(2026, 10, 8, 15, 0, 0));
        n.UltimoPostEm.Should().Be(new DateTime(2026, 10, 8, 15, 0, 0));
        n.UltimaDecisao.Should().Contain("original=" + RecoveryFixtures.DataAntiga).And.Contain("nova=" + DataNova);
        PayloadDataEmissao.LerTexto(n.PayloadJson).Should().Be(DataNova);
        n.Tentativas.Should().Be(2, "a coluna legada fica intacta");
        n.UltimaMensagemErro.Should().Be("erro legado");
    }

    [Fact(DisplayName = "Depois do timeout no POST, o CICLO SEGUINTE (orquestrador novo) comeca por GET e nao faz POST antes dele")]
    public async Task Ciclo2_ComecaPorGet()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        var nota = c.Amb.Ler(id);
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Timeout());
        await c.CicloAsync();
        c.Metodos.Should().Equal("GET", "POST");

        c.Relogio.DefinirUtc(c.Amb.Ler(id).ProximaTentativaEm!.Value);
        c.Eventos.Clear();
        c.Focus.EnfileirarGet(Resp.Autorizada());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET", "POST", "GET");
        c.Eventos.Should().Equal("Store.Obter", "Focus.GET", "Fiscal.Persistir", "Store.Remover");
        r.Autorizadas.Should().Be(1);
        VerificarPersistiu(c, nota);
        c.Amb.Visivel(id).Should().BeFalse();
    }

    [Fact(DisplayName = "BACKOFF entre execucoes: 7 ciclos com orquestradores novos gravam 2, 2, 4, 8, 15, 30, 30 min; um resultado conhecido zera")]
    public async Task Backoff_EntreExecucoes()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        var esperas = new[] { 2, 2, 4, 8, 15, 30, 30 };

        for (var i = 0; i < esperas.Length; i++)
        {
            c.Focus.EnfileirarGet(Resp.Timeout());

            await c.CicloAsync();

            var n = c.Amb.Ler(id);
            n.FalhasTransitoriasSeguidas.Should().Be(i + 1, $"ciclo {i + 1}");
            (n.ProximaTentativaEm!.Value - c.Relogio.Agora.UtcDateTime).Should().Be(TimeSpan.FromMinutes(esperas[i]), $"ciclo {i + 1}");
            n.TentativasConsulta.Should().Be(i + 1);

            if (i == 0)
            {
                // Um minuto ANTES de vencer: nenhuma chamada e nenhuma coluna alterada.
                c.Relogio.DefinirUtc(n.ProximaTentativaEm.Value.AddMinutes(-1));
                var foto = FotoPendencia.De(c.Amb.Ler(id));
                var chamadas = c.Focus.Chamadas.Count;

                var cedo = await c.CicloAsync();

                cedo.Elegiveis.Should().Be(0);
                c.Focus.Chamadas.Count.Should().Be(chamadas);
                FotoPendencia.De(c.Amb.Ler(id)).Should().Be(foto);
            }

            c.Relogio.DefinirUtc(n.ProximaTentativaEm.Value);
        }

        c.Focus.EnfileirarGet(Resp.Processando());
        await c.CicloAsync();

        var final = c.Amb.Ler(id);
        final.FalhasTransitoriasSeguidas.Should().Be(0, "um resultado nao transitorio zera o contador");
        (final.ProximaTentativaEm!.Value - c.Relogio.Agora.UtcDateTime).Should().Be(TimeSpan.FromMinutes(2));
        c.Metodos.Should().OnlyContain(m => m == "GET");
    }

    [Fact(DisplayName = "POST pending_operation: aguarda +2 min, a nota da regeneracao acompanha a decisao, POST e consulta contados")]
    public async Task PostPendingOperation_AguardaComNota()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Http(422, FocusFixtures.PendingOperation));

        await c.CicloAsync();

        var n = c.Amb.Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.Ativa);
        n.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 2, 0));
        n.TentativasConsulta.Should().Be(1);
        n.TentativasPost.Should().Be(1);
        n.UltimaDecisao.Should().StartWith("OperacaoPendente").And.Contain("original=" + RecoveryFixtures.DataAntiga);
        NadaFoiPersistido(c);
        NenhumaVendaMarcada(c);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  JaProcessado e as contradicoes da Focus
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "POST already_processed: um GET no mesmo ciclo, que autoriza: persiste, remove, e houve um unico POST")]
    public async Task JaProcessado_GetSeguinteAutoriza()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        var nota = c.Amb.Ler(id);
        c.Focus.EnfileirarGet(Resp.NaoEncontrado(), Resp.Autorizada());
        c.Focus.EnfileirarPost(Resp.JaProcessado());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET", "POST", "GET");
        c.Eventos.Should().Equal(
            "Store.Obter", "Focus.GET", "Store.Aplicar", "Store.Regravar", "Focus.POST", "Focus.GET", "Fiscal.Persistir", "Store.Remover");
        r.Autorizadas.Should().Be(1);
        VerificarPersistiu(c, nota);
        c.Amb.Visivel(id).Should().BeFalse();
    }

    [Fact(DisplayName = "POST already_processed seguido de GET 404: contradicao -> IntervencaoManual, sem segundo POST e sem persistir")]
    public async Task JaProcessado_Get404_Contradicao()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado(), Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.JaProcessado());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET", "POST", "GET");
        r.IntervencaoManual.Should().Be(1);
        var n = c.Amb.Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.IntervencaoManual);
        n.UltimaDecisao.Should().Contain("Contradicao");
        NadaFoiPersistido(c);
        NenhumaVendaMarcada(c);
    }

    [Theory(DisplayName = "POST already_processed seguido de GET com REJEICAO (704 ou outra): contradicao -> IntervencaoManual, venda NAO marcada")]
    [InlineData("704")]
    [InlineData("outra")]
    public async Task JaProcessado_GetRejeicao_Contradicao(string tipo)
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado(), tipo == "704" ? Resp.Rejeitada704() : Resp.RejeitadaOutroCodigo());
        c.Focus.EnfileirarPost(Resp.JaProcessado());

        await c.CicloAsync();

        c.Metodos.Should().Equal("GET", "POST", "GET");
        c.Amb.Ler(id).Estado.Should().Be(NfePendenteEstados.IntervencaoManual);
        c.Amb.Ler(id).UltimaDecisao.Should().Contain("Contradicao");
        NenhumaVendaMarcada(c);
        NadaFoiPersistido(c);
    }

    [Fact(DisplayName = "POST devolve 704 depois de regenerar: IntervencaoManual, sem nova regeneracao e sem novo POST")]
    public async Task Post704_SemLaco()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Rejeitada704());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET", "POST");
        r.IntervencaoManual.Should().Be(1);
        c.Amb.Ler(id).Estado.Should().Be(NfePendenteEstados.IntervencaoManual);
        c.Amb.Ler(id).UltimaDecisao.Should().Contain("704");
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Rejeicao, configuracao, anomalias
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "GET com rejeicao fiscal (nao 704): marca a venda Rejeitada com os argumentos exatos e REMOVE a pendencia")]
    public async Task GetRejeicao_MarcaVendaERemove()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        var nota = c.Amb.Ler(id);
        c.Focus.EnfileirarGet(Resp.RejeitadaOutroCodigo());

        var r = await c.CicloAsync();

        r.Rejeitadas.Should().Be(1);
        c.Vendas.Verify(v => v.AtualizarDadosNfceAsync(
            nota.VendaId, "", "Rejeitada: Rejeicao qualquer", "Produção", nota.Referencia, null, null), Times.Once());
        c.Amb.Visivel(id).Should().BeFalse("a pendencia rejeitada nao pode ficar na fila (o worker antigo a deixava)");
        c.Eventos.Should().Equal("Store.Obter", "Focus.GET", "Vendas.Rejeitar", "Store.Remover");
        NadaFoiPersistido(c);
        c.Metodos.Should().Equal("GET");
    }

    [Fact(DisplayName = "POST 422 de validacao: marca a venda Rejeitada (codigo: mensagem) e remove a pendencia")]
    public async Task Post422Validacao_Rejeita()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        var nota = c.Amb.Ler(id);
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Focus.EnfileirarPost(Resp.Http(422, "{\"codigo\":\"erro_validacao_schema\",\"mensagem\":\"campo invalido\"}"));

        await c.CicloAsync();

        c.Vendas.Verify(v => v.AtualizarDadosNfceAsync(
            nota.VendaId, "", "Rejeitada: erro_validacao_schema: campo invalido", "Produção", nota.Referencia, null, null), Times.Once());
        c.Amb.Visivel(id).Should().BeFalse();
        NadaFoiPersistido(c);
    }

    [Fact(DisplayName = "GET 401: AguardandoCorrecao com +30 min; no ciclo seguinte (+30 min) a pendencia volta a Ativa quando a Focus responde")]
    public async Task Get401_AguardaCorrecao_DepoisRetoma()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.PermissaoNegada());

        var r1 = await c.CicloAsync();

        r1.AguardandoCorrecao.Should().Be(1);
        var n1 = c.Amb.Ler(id);
        n1.Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        n1.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 30, 0));

        c.Relogio.DefinirUtc(n1.ProximaTentativaEm!.Value);
        c.Focus.EnfileirarGet(Resp.Processando());
        await c.CicloAsync();

        var n2 = c.Amb.Ler(id);
        n2.Estado.Should().Be(NfePendenteEstados.Ativa);
        n2.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 32, 0));
    }

    [Fact(DisplayName = "Token em branco: AguardandoCorrecao, ZERO chamadas a Focus e a consulta nao e contada")]
    public async Task TokenEmBranco_SemHttp()
    {
        using var c = new Cenario { Contexto = new RecoveryTenantContext("   ", true) };
        var id = c.Amb.Semear();

        var r = await c.CicloAsync();

        c.Focus.Chamadas.Should().BeEmpty();
        r.AguardandoCorrecao.Should().Be(1);
        var n = c.Amb.Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.AguardandoCorrecao);
        n.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 30, 0));
        n.TentativasConsulta.Should().Be(0);
        n.UltimaConsultaEm.Should().BeNull();
        n.UltimaDecisao.Should().Contain("ErroDeConfiguracao");
    }

    [Theory(DisplayName = "Autorizado sem chave normalizada ou sem caminho do DANFE: IntervencaoManual e NADA e persistido")]
    [InlineData(BodyAutorizadoSemChave)]
    [InlineData(BodyAutorizadoSemDanfe)]
    public async Task AutorizadoComDadosIncompletos_NaoPersiste(string corpo)
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.Http(200, corpo));

        var r = await c.CicloAsync();

        r.IntervencaoManual.Should().Be(1);
        c.Amb.Ler(id).Estado.Should().Be(NfePendenteEstados.IntervencaoManual);
        c.Amb.Visivel(id).Should().BeTrue("a pendencia nao pode ser removida sem persistir");
        NadaFoiPersistido(c);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  O que NAO pode ser tocado
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "IntervencaoManual e pendencia com agenda no futuro: ZERO chamadas a Focus e nenhuma coluna alterada")]
    public async Task IntervencaoManualEAgendaFutura_NaoTocadas()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var manual = c.Amb.Semear(n => n.Estado = NfePendenteEstados.IntervencaoManual);
        var futura = c.Amb.Semear(n => n.ProximaTentativaEm = new DateTime(2026, 10, 8, 15, 10, 0));
        var fotoManual = FotoPendencia.De(c.Amb.Ler(manual));
        var fotoFutura = FotoPendencia.De(c.Amb.Ler(futura));

        var r = await c.CicloAsync();

        r.Elegiveis.Should().Be(0);
        c.Focus.Chamadas.Should().BeEmpty();
        c.Eventos.Should().Equal("Store.Obter");
        FotoPendencia.De(c.Amb.Ler(manual)).Should().Be(fotoManual);
        FotoPendencia.De(c.Amb.Ler(futura)).Should().Be(fotoFutura);
    }

    [Fact(DisplayName = "Pendencia de NF-e nao e tocada pelo orquestrador (NFC-e primeiro)")]
    public async Task Nfe_Ignorada()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear(tipoNota: "NFE");
        var foto = FotoPendencia.De(c.Amb.Ler(id));

        var r = await c.CicloAsync();

        r.Elegiveis.Should().Be(0);
        c.Focus.Chamadas.Should().BeEmpty();
        FotoPendencia.De(c.Amb.Ler(id)).Should().Be(foto);
    }

    [Fact(DisplayName = "TERMINAL durante o ciclo: se a linha vira IntervencaoManual no banco antes de gravar o payload, ZERO POST")]
    public async Task Terminal_DuranteOCiclo_ZeroPost()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());
        c.Antes["Regravar"] = () =>
        {
            c.Amb.ModificarPorFora(id, n => n.Estado = NfePendenteEstados.IntervencaoManual);
            return Task.CompletedTask;
        };

        var r = await c.CicloAsync();

        r.Ignoradas.Should().Be(1);
        c.Metodos.Should().Equal("GET");
        var n = c.Amb.Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.IntervencaoManual);
        n.PayloadJson.Should().Be(RecoveryFixtures.PayloadNfce, "o payload nao pode ter sido regravado");
        NadaFoiPersistido(c);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Falhas: processamento x persistencia x cancelamento
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "Excecao de PROCESSAMENTO numa pendencia vira Desconhecido (conta) e o ciclo segue para a proxima")]
    public async Task ExcecaoDeProcessamento_ViraDesconhecido_ECicloSegue()
    {
        using var c = new Cenario();
        var primeira = c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 8, 0, 0));
        var segunda = c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 9, 0, 0));
        var notaSegunda = c.Amb.Ler(segunda);
        c.Focus.EnfileirarGet(new InvalidOperationException("boom"), Resp.Autorizada());

        var r = await c.CicloAsync();

        r.ErrosDeProcessamento.Should().Be(1);
        r.FalhasDePersistencia.Should().Be(0);
        r.Autorizadas.Should().Be(1);

        var n = c.Amb.Ler(primeira);
        n.Estado.Should().Be(NfePendenteEstados.Ativa);
        n.FalhasDesconhecidasSeguidas.Should().Be(1);
        n.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 2, 0));
        n.TentativasConsulta.Should().Be(1);
        n.UltimaDecisao.Should().Contain("InvalidOperationException").And.Contain("boom");

        VerificarPersistiu(c, notaSegunda);
        c.Amb.Visivel(segunda).Should().BeFalse();
    }

    [Fact(DisplayName = "Excecao de processamento 3 vezes seguidas (limite K): IntervencaoManual, sem nenhum POST")]
    public async Task ExcecaoRepetida_TresCiclos_IntervencaoManual()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();

        for (var i = 1; i <= 3; i++)
        {
            c.Focus.EnfileirarGet(new InvalidOperationException("boom"));
            await c.CicloAsync();

            var n = c.Amb.Ler(id);
            n.FalhasDesconhecidasSeguidas.Should().Be(i);
            if (i < 3)
            {
                n.Estado.Should().Be(NfePendenteEstados.Ativa);
                c.Relogio.DefinirUtc(n.ProximaTentativaEm!.Value);
            }
        }

        c.Amb.Ler(id).Estado.Should().Be(NfePendenteEstados.IntervencaoManual);
        c.Metodos.Should().OnlyContain(m => m == "GET");
    }

    [Fact(DisplayName = "Payload que nao vira corpo de envio: Desconhecido ANTES de gravar qualquer coisa, zero POST, payload intacto")]
    public async Task PayloadInconvertivel_Desconhecido_SemGravarPayload()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var payloadRuim = "{\"DataEmissao\":\"" + RecoveryFixtures.DataAntiga + "\",\"Itens\":\"nao-e-uma-lista\"}";
        var id = c.Amb.Semear(n => n.PayloadJson = payloadRuim);
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());

        var r = await c.CicloAsync();

        r.ErrosDeProcessamento.Should().Be(1);
        c.Metodos.Should().Equal("GET");
        c.Eventos.Should().Equal("Store.Obter", "Focus.GET", "Store.Aplicar");
        var n = c.Amb.Ler(id);
        n.PayloadJson.Should().Be(payloadRuim);
        n.FalhasDesconhecidasSeguidas.Should().Be(1);
    }

    [Fact(DisplayName = "FALHA DE PERSISTENCIA ao gravar o payload: ZERO POST, nao vira Desconhecido, e a proxima pendencia e processada")]
    public async Task FalhaAoGravarPayload_ZeroPost()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var primeira = c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 8, 0, 0));
        var segunda = c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 9, 0, 0));
        c.Falhas["Regravar"] = new InvalidOperationException("banco fora do ar");
        c.Focus.EnfileirarGet(Resp.NaoEncontrado(), Resp.Autorizada());

        var r = await c.CicloAsync();

        c.Metodos.Should().Equal("GET", "GET");
        r.FalhasDePersistencia.Should().Be(1);
        r.ErrosDeProcessamento.Should().Be(0);
        r.Autorizadas.Should().Be(1);

        var n = c.Amb.Ler(primeira);
        n.PayloadJson.Should().Be(RecoveryFixtures.PayloadNfce);
        n.FalhasDesconhecidasSeguidas.Should().Be(0, "falha de persistencia nao e resposta desconhecida da Focus");
        n.Estado.Should().Be(NfePendenteEstados.Ativa);
        c.Amb.Visivel(segunda).Should().BeFalse();
    }

    [Fact(DisplayName = "FALHA DE PERSISTENCIA ao gravar a intencao (data original/nova): nem regrava o payload nem faz POST")]
    public async Task FalhaAoGravarAIntencao_ZeroRegravarEZeroPost()
    {
        using var c = new Cenario { Opcoes = D8Ligada };
        var id = c.Amb.Semear();
        c.Falhas["Aplicar"] = new InvalidOperationException("banco fora do ar");
        c.Focus.EnfileirarGet(Resp.NaoEncontrado());

        var r = await c.CicloAsync();

        r.FalhasDePersistencia.Should().Be(1);
        c.Eventos.Should().Equal("Store.Obter", "Focus.GET", "Store.Aplicar");
        c.Metodos.Should().Equal("GET");
        c.Amb.Ler(id).PayloadJson.Should().Be(RecoveryFixtures.PayloadNfce);
    }

    [Fact(DisplayName = "FALHA ao persistir a AUTORIZACAO: a pendencia NAO e removida, nao vira Desconhecido, reagenda com backoff e retoma depois")]
    public async Task FalhaAoPersistirAutorizacao_NaoRemove_Reagenda_Retoma()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        var nota = c.Amb.Ler(id);
        c.Fiscal.SetupSequence(QualquerPersistir())
            .Throws(new InvalidOperationException("sem banco"))
            .Returns(Task.CompletedTask);
        c.Focus.EnfileirarGet(Resp.Autorizada(), Resp.Autorizada());

        var r1 = await c.CicloAsync();

        r1.FalhasDePersistencia.Should().Be(1);
        r1.Autorizadas.Should().Be(0);
        r1.ErrosDeProcessamento.Should().Be(0);
        c.Eventos.Should().NotContain("Store.Remover");
        c.Amb.Visivel(id).Should().BeTrue();
        var n1 = c.Amb.Ler(id);
        n1.Estado.Should().Be(NfePendenteEstados.Ativa);
        n1.FalhasTransitoriasSeguidas.Should().Be(1);
        n1.FalhasDesconhecidasSeguidas.Should().Be(0, "nao e resposta desconhecida da Focus");
        n1.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 2, 0));
        n1.UltimaDecisao.Should().Contain("persistencia local falhou");

        c.Relogio.DefinirUtc(n1.ProximaTentativaEm!.Value);
        var r2 = await c.CicloAsync();

        r2.Autorizadas.Should().Be(1);
        VerificarPersistiu(c, nota, vezes: 2);
        c.Amb.Visivel(id).Should().BeFalse();
    }

    [Fact(DisplayName = "FALHA ao gravar uma decisao comum (GET processando): nada muda no banco e NAO vira Desconhecido")]
    public async Task FalhaAoAplicarDecisao_NaoMascara()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        var antes = FotoPendencia.De(c.Amb.Ler(id));
        c.Falhas["Aplicar"] = new InvalidOperationException("banco fora do ar");
        c.Focus.EnfileirarGet(Resp.Processando());

        var r = await c.CicloAsync();

        r.FalhasDePersistencia.Should().Be(1);
        r.ErrosDeProcessamento.Should().Be(0);
        FotoPendencia.De(c.Amb.Ler(id)).Should().Be(antes);
    }

    [Fact(DisplayName = "FALHA ao remover depois de persistir: a pendencia fica e a rodada seguinte reconcilia e remove (persiste de novo)")]
    public async Task FalhaAoRemover_Reconcilia()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        var nota = c.Amb.Ler(id);
        c.Falhas["Remover"] = new InvalidOperationException("banco fora do ar");
        c.Focus.EnfileirarGet(Resp.Autorizada(), Resp.Autorizada());

        var r1 = await c.CicloAsync();

        r1.FalhasDePersistencia.Should().Be(1);
        c.Amb.Visivel(id).Should().BeTrue();

        c.Falhas.Clear();
        var r2 = await c.CicloAsync();

        r2.Autorizadas.Should().Be(1);
        VerificarPersistiu(c, nota, vezes: 2);
        c.Amb.Visivel(id).Should().BeFalse();
    }

    [Fact(DisplayName = "FALHA ao marcar a venda Rejeitada: a pendencia NAO e removida")]
    public async Task FalhaAoMarcarVendaRejeitada_NaoRemove()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        c.Vendas.Setup(QualquerRejeitar()).Throws(new InvalidOperationException("venda indisponivel"));
        c.Focus.EnfileirarGet(Resp.RejeitadaOutroCodigo());

        var r = await c.CicloAsync();

        r.FalhasDePersistencia.Should().Be(1);
        r.Rejeitadas.Should().Be(0);
        c.Eventos.Should().NotContain("Store.Remover");
        c.Amb.Visivel(id).Should().BeTrue();
    }

    [Fact(DisplayName = "Falha ao SELECIONAR a fila e infraestrutura: a excecao sobe e nenhuma chamada a Focus acontece")]
    public async Task FalhaNaSelecao_Propaga()
    {
        using var c = new Cenario();
        c.Amb.Semear();
        c.Falhas["Obter"] = new InvalidOperationException("sem banco");

        Func<Task> act = async () => await c.CicloAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("sem banco");
        c.Focus.Chamadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Cancelamento ja solicitado: propaga OperationCanceledException, zero Focus, nada alterado")]
    public async Task Cancelamento_JaSolicitado()
    {
        using var c = new Cenario();
        var id = c.Amb.Semear();
        var foto = FotoPendencia.De(c.Amb.Ler(id));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await c.CicloAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        c.Focus.Chamadas.Should().BeEmpty();
        FotoPendencia.De(c.Amb.Ler(id)).Should().Be(foto);
    }

    [Fact(DisplayName = "Cancelamento DURANTE o GET: propaga, NAO e contado como Desconhecido, nada e gravado e as demais ficam intactas")]
    public async Task Cancelamento_DuranteOGet()
    {
        using var c = new Cenario();
        var primeira = c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 8, 0, 0));
        var segunda = c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 9, 0, 0));
        var fotoPrimeira = FotoPendencia.De(c.Amb.Ler(primeira));
        var fotoSegunda = FotoPendencia.De(c.Amb.Ler(segunda));
        c.Focus.EnfileirarGet((Func<CancellationToken, Task<FocusResponse>>)(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return null!;
        }));
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        Func<Task> act = async () => await c.CicloAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        c.Eventos.Should().Equal("Store.Obter", "Focus.GET");
        FotoPendencia.De(c.Amb.Ler(primeira)).Should().Be(fotoPrimeira);
        FotoPendencia.De(c.Amb.Ler(segunda)).Should().Be(fotoSegunda);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  Resultado do ciclo e construtor
    // ═════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "As contagens do ciclo batem: autorizada, rejeitada, agendada e manual na mesma rodada")]
    public async Task ContagensDoCiclo()
    {
        using var c = new Cenario();
        c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 6, 0, 0));
        c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 7, 0, 0));
        c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 8, 0, 0));
        c.Amb.Semear(dataFalha: new DateTime(2026, 10, 8, 9, 0, 0));
        c.Focus.EnfileirarGet(Resp.Autorizada(), Resp.RejeitadaOutroCodigo(), Resp.Processando(), Resp.NaoEncontrado());

        var r = await c.CicloAsync();

        r.Elegiveis.Should().Be(4);
        r.Autorizadas.Should().Be(1);
        r.Rejeitadas.Should().Be(1);
        r.Agendadas.Should().Be(1);
        r.IntervencaoManual.Should().Be(1);   // 404 com a D8 desligada
        r.ErrosDeProcessamento.Should().Be(0);
        r.FalhasDePersistencia.Should().Be(0);
    }

    [Fact(DisplayName = "Construtor e metodo recusam argumentos nulos")]
    public async Task ArgumentosNulos()
    {
        using var c = new Cenario();
        using var alvo = c.Amb.NovaStore();
        var store = new StoreRegistradora(alvo.Store, c.Eventos, c.Falhas, c.Antes);

        var ctor = () => new NfePendenteRecoveryOrchestrator(null!, c.Focus, c.Fiscal.Object, c.Vendas.Object, new RecoveryPolicyOptions());
        ctor.Should().Throw<ArgumentNullException>();

        var orquestrador = new NfePendenteRecoveryOrchestrator(store, c.Focus, c.Fiscal.Object, c.Vendas.Object, new RecoveryPolicyOptions());
        Func<Task> act = async () => await orquestrador.ProcessarTenantAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
