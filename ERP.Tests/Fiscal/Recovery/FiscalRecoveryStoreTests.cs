using ERP.Application.Fiscal.Recovery;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ERP.Tests.Fiscal;

/// <summary>
/// 4A-5c: a fila de recuperacao por estado e agenda, em SQLite REAL.
///
/// Dois cuidados de fidelidade com a producao:
///  1) O contexto usa QueryTrackingBehavior.NoTracking, como o Program.cs da API (linha 54). Assim,
///     uma gravacao que esquecesse o Update(entidade) NAO persistiria e estes testes falhariam.
///  2) O estado final e sempre lido por um contexto NOVO, nunca pelo que gravou.
///
/// O tenant do filtro e um AsyncLocal estatico, atualizado a cada contexto construido: por isso as
/// pendencias de cada tenant sao semeadas por contextos proprios, e a store do tenant testado e
/// criada POR ULTIMO.
/// </summary>
public sealed class FiscalRecoveryStoreTests : IDisposable
{
    private static readonly DateTime Agora = new(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc);

    private const string PayloadPadrao = "{\"DataEmissao\":\"2026-10-08T09:01:12-03:00\",\"Itens\":[]}";

    private readonly SqliteConnection _conexao;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _outroTenant = Guid.NewGuid();

    public FiscalRecoveryStoreTests()
    {
        _conexao = new SqliteConnection($"DataSource=recoverystore_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        _conexao.Open();

        using var ctx = NovoContexto(_tenant);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    // ── Infraestrutura de teste ──────────────────────────────────────────

    private AppDbContext NovoContexto(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conexao)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)   // igual a producao
            .Options;

        return new AppDbContext(options, new ERP.Tests.FakeRequestTenant { TenantId = tenantId });
    }

    private sealed class Alvo : IDisposable
    {
        private readonly ERP.Infrastructure.UnitOfWork.UnitOfWork _uow;

        public Alvo(ERP.Infrastructure.UnitOfWork.UnitOfWork uow)
        {
            _uow = uow;
            Store = new FiscalRecoveryStore(uow);
        }

        public FiscalRecoveryStore Store { get; }

        public void Dispose() => _uow.Dispose();
    }

    /// <summary>Cria a store do tenant. Chamar POR ULTIMO: o construtor do contexto define o tenant ambiente.</summary>
    private Alvo Novo(Guid tenantId)
    {
        var ctx = NovoContexto(tenantId);
        var uow = new ERP.Infrastructure.UnitOfWork.UnitOfWork(
            ctx,
            Mock.Of<IProductRepository>(),
            Mock.Of<ICustomerRepository>(),
            Mock.Of<ISaleRepository>(),
            Mock.Of<ICategoryRepository>(),
            Mock.Of<IUserRepository>(),
            new ERP.Tests.FakeRequestTenant { TenantId = tenantId });

        return new Alvo(uow);
    }

    private Guid Semear(Guid tenantId, Action<NfePendente>? ajustar = null, string tipoNota = "NFCE")
    {
        using var ctx = NovoContexto(tenantId);

        var nota = new NfePendente
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VendaId = Guid.NewGuid(),
            TipoNota = tipoNota,
            PayloadJson = PayloadPadrao,
            Referencia = Guid.NewGuid().ToString(),
            DataFalha = new DateTime(2026, 10, 8, 9, 0, 0),
            Tentativas = 2,
            UltimaMensagemErro = "erro legado"
        };
        ajustar?.Invoke(nota);

        ctx.NfePendentes.Add(nota);
        ctx.SaveChanges();
        return nota.Id;
    }

    /// <summary>Le a linha crua (sem filtro de tenant) por um contexto novo.</summary>
    private NfePendente Ler(Guid id)
    {
        using var ctx = NovoContexto(_tenant);
        return ctx.NfePendentes.IgnoreQueryFilters().AsNoTracking().Single(n => n.Id == id);
    }

    private bool ExisteNoBanco(Guid id)
    {
        using var ctx = NovoContexto(_tenant);
        return ctx.NfePendentes.IgnoreQueryFilters().AsNoTracking().Any(n => n.Id == id);
    }

    /// <summary>Visivel para o tenant, com o filtro global aplicado.</summary>
    private bool Visivel(Guid tenantId, Guid id)
    {
        using var ctx = NovoContexto(tenantId);
        return ctx.NfePendentes.AsNoTracking().Any(n => n.Id == id);
    }

    private sealed record Foto(
        string Estado, DateTime? ProximaTentativaEm, string? UltimaDecisao, DateTime? UltimaConsultaEm,
        DateTime? UltimoPostEm, int TentativasPost, int TentativasConsulta, int FalhasTransitorias,
        int FalhasDesconhecidas, string PayloadJson, int Tentativas, string? UltimaMensagemErro,
        string TipoNota, string Referencia, DateTime DataFalha, Guid VendaId);

    private static Foto Tirar(NfePendente n) => new(
        n.Estado, n.ProximaTentativaEm, n.UltimaDecisao, n.UltimaConsultaEm, n.UltimoPostEm,
        n.TentativasPost, n.TentativasConsulta, n.FalhasTransitoriasSeguidas, n.FalhasDesconhecidasSeguidas,
        n.PayloadJson, n.Tentativas, n.UltimaMensagemErro, n.TipoNota, n.Referencia, n.DataFalha, n.VendaId);

    private static RecoveryDecision Decisao(
        string? novoEstado = NfePendenteEstados.Ativa,
        DateTimeOffset? proxima = null,
        int transitorias = 0,
        int desconhecidas = 0,
        string motivo = "Transitorio: falha transitoria #1") =>
        new(RecoveryAction.ManterComBackoff, novoEstado, proxima, transitorias, desconhecidas, motivo);

    // ── A fidelidade do proprio teste ────────────────────────────────────

    [Fact(DisplayName = "O contexto de teste e NoTracking, como a producao (senao um Update esquecido passaria despercebido)")]
    public void Harness_EhNoTracking()
    {
        using var ctx = NovoContexto(_tenant);

        ctx.ChangeTracker.QueryTrackingBehavior.Should().Be(QueryTrackingBehavior.NoTracking);
    }

    [Fact(DisplayName = "Tamanho maximo de UltimaDecisao e 500")]
    public void Constante_500()
    {
        FiscalRecoveryStore.TamanhoMaximoUltimaDecisao.Should().Be(500);
    }

    // ── ObterElegiveisAsync ──────────────────────────────────────────────

    [Fact(DisplayName = "Elegiveis: Ativa com ProximaTentativaEm nula entra")]
    public async Task Elegiveis_Nula_Entra()
    {
        var id = Semear(_tenant);
        using var a = Novo(_tenant);

        var lista = await a.Store.ObterElegiveisAsync("NFCE", Agora);

        lista.Select(n => n.Id).Should().Equal(id);
    }

    [Theory(DisplayName = "Elegiveis: ProximaTentativaEm igual ou anterior a agora entra")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-120)]
    public async Task Elegiveis_Vencida_Entra(int segundosRelativos)
    {
        var id = Semear(_tenant, n => n.ProximaTentativaEm = Agora.AddSeconds(segundosRelativos));
        using var a = Novo(_tenant);

        var lista = await a.Store.ObterElegiveisAsync("NFCE", Agora);

        lista.Select(n => n.Id).Should().Equal(id);
    }

    [Theory(DisplayName = "Elegiveis: ProximaTentativaEm no futuro NAO entra")]
    [InlineData(1)]
    [InlineData(120)]
    [InlineData(1800)]
    public async Task Elegiveis_NoFuturo_NaoEntra(int segundosRelativos)
    {
        Semear(_tenant, n => n.ProximaTentativaEm = Agora.AddSeconds(segundosRelativos));
        using var a = Novo(_tenant);

        var lista = await a.Store.ObterElegiveisAsync("NFCE", Agora);

        lista.Should().BeEmpty();
    }

    [Fact(DisplayName = "Elegiveis: AguardandoCorrecao vencida entra")]
    public async Task Elegiveis_AguardandoCorrecao_Entra()
    {
        var id = Semear(_tenant, n =>
        {
            n.Estado = NfePendenteEstados.AguardandoCorrecao;
            n.ProximaTentativaEm = Agora.AddMinutes(-1);
        });
        using var a = Novo(_tenant);

        var lista = await a.Store.ObterElegiveisAsync("NFCE", Agora);

        lista.Select(n => n.Id).Should().Equal(id);
    }

    [Fact(DisplayName = "Elegiveis: IntervencaoManual NUNCA entra (nem com agenda nula nem vencida)")]
    public async Task Elegiveis_IntervencaoManual_NuncaEntra()
    {
        Semear(_tenant, n => { n.Estado = NfePendenteEstados.IntervencaoManual; n.ProximaTentativaEm = null; });
        Semear(_tenant, n => { n.Estado = NfePendenteEstados.IntervencaoManual; n.ProximaTentativaEm = Agora.AddDays(-3); });
        using var a = Novo(_tenant);

        var lista = await a.Store.ObterElegiveisAsync("NFCE", Agora);

        lista.Should().BeEmpty();
    }

    [Theory(DisplayName = "Elegiveis: estado desconhecido ou corrompido NAO entra")]
    [InlineData("Processando")]
    [InlineData("ativa")]
    [InlineData("")]
    [InlineData("Concluida")]
    public async Task Elegiveis_EstadoDesconhecido_NaoEntra(string estado)
    {
        Semear(_tenant, n => n.Estado = estado);
        using var a = Novo(_tenant);

        var lista = await a.Store.ObterElegiveisAsync("NFCE", Agora);

        lista.Should().BeEmpty();
    }

    [Fact(DisplayName = "Elegiveis: filtra pelo TipoNota pedido (NFCE nao traz NFE e vice-versa)")]
    public async Task Elegiveis_FiltraPorTipo()
    {
        var nfce = Semear(_tenant, tipoNota: "NFCE");
        var nfe = Semear(_tenant, tipoNota: "NFE");
        using var a = Novo(_tenant);

        var soNfce = await a.Store.ObterElegiveisAsync("NFCE", Agora);
        var soNfe = await a.Store.ObterElegiveisAsync("NFE", Agora);

        soNfce.Select(n => n.Id).Should().Equal(nfce);
        soNfe.Select(n => n.Id).Should().Equal(nfe);
    }

    [Fact(DisplayName = "Elegiveis: tipo de nota com outra caixa nao casa (comparacao exata, como o worker atual)")]
    public async Task Elegiveis_TipoComparadoExatamente()
    {
        Semear(_tenant, tipoNota: "NFCE");
        using var a = Novo(_tenant);

        (await a.Store.ObterElegiveisAsync("nfce", Agora)).Should().BeEmpty();
    }

    [Fact(DisplayName = "Elegiveis: ordenadas por DataFalha, da mais antiga para a mais nova")]
    public async Task Elegiveis_Ordenadas()
    {
        var meio = Semear(_tenant, n => n.DataFalha = new DateTime(2026, 10, 8, 10, 0, 0));
        var nova = Semear(_tenant, n => n.DataFalha = new DateTime(2026, 10, 8, 12, 0, 0));
        var antiga = Semear(_tenant, n => n.DataFalha = new DateTime(2026, 10, 8, 8, 0, 0));
        using var a = Novo(_tenant);

        var lista = await a.Store.ObterElegiveisAsync("NFCE", Agora);

        lista.Select(n => n.Id).Should().Equal(antiga, meio, nova);
    }

    [Fact(DisplayName = "Elegiveis: a pendencia de OUTRO tenant nao aparece")]
    public async Task Elegiveis_OutroTenant_NaoAparece()
    {
        var meu = Semear(_tenant);
        Semear(_outroTenant);
        using var a = Novo(_tenant);

        var lista = await a.Store.ObterElegiveisAsync("NFCE", Agora);

        lista.Select(n => n.Id).Should().Equal(meu);
    }

    [Fact(DisplayName = "Elegiveis: devolve o payload e a referencia gravados")]
    public async Task Elegiveis_TrazOsDados()
    {
        var id = Semear(_tenant, n => n.PayloadJson = "{\"x\":1}");
        using var a = Novo(_tenant);

        var nota = (await a.Store.ObterElegiveisAsync("NFCE", Agora)).Single();

        nota.Id.Should().Be(id);
        nota.PayloadJson.Should().Be("{\"x\":1}");
        nota.Referencia.Should().NotBeNullOrWhiteSpace();
    }

    [Theory(DisplayName = "Elegiveis: agora sem Kind=Utc lanca ArgumentException")]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public async Task Elegiveis_AgoraNaoUtc_Lanca(DateTimeKind kind)
    {
        using var a = Novo(_tenant);

        Func<Task> act = async () => await a.Store.ObterElegiveisAsync("NFCE", new DateTime(2026, 10, 8, 15, 0, 0, kind));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory(DisplayName = "Elegiveis: tipo de nota vazio lanca ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Elegiveis_TipoVazio_Lanca(string? tipo)
    {
        using var a = Novo(_tenant);

        Func<Task> act = async () => await a.Store.ObterElegiveisAsync(tipo!, Agora);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── AplicarDecisaoAsync ──────────────────────────────────────────────

    [Fact(DisplayName = "Aplicar: grava EXATAMENTE as colunas da decisao (estado, agenda em UTC, contadores, motivo, consulta)")]
    public async Task Aplicar_GravaAsColunas()
    {
        var id = Semear(_tenant);
        var proxima = new DateTimeOffset(Agora).AddMinutes(2);
        using var a = Novo(_tenant);

        var ok = await a.Store.AplicarDecisaoAsync(
            id, Decisao(NfePendenteEstados.Ativa, proxima, transitorias: 1, desconhecidas: 0, motivo: "Transitorio: falha #1"),
            Agora, consultou: true, postou: false);

        ok.Should().BeTrue();
        var n = Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.Ativa);
        n.ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 2, 0));
        n.FalhasTransitoriasSeguidas.Should().Be(1);
        n.FalhasDesconhecidasSeguidas.Should().Be(0);
        n.UltimaDecisao.Should().Be("Transitorio: falha #1");
        n.UltimaConsultaEm.Should().Be(Agora);
        n.TentativasConsulta.Should().Be(1);
        n.UltimoPostEm.Should().BeNull();
        n.TentativasPost.Should().Be(0);
    }

    [Fact(DisplayName = "Aplicar: consultou E postou grava os dois pares de data e contador")]
    public async Task Aplicar_ConsultouEPostou()
    {
        var id = Semear(_tenant);
        using var a = Novo(_tenant);

        await a.Store.AplicarDecisaoAsync(id, Decisao(), Agora, consultou: true, postou: true);

        var n = Ler(id);
        n.UltimaConsultaEm.Should().Be(Agora);
        n.UltimoPostEm.Should().Be(Agora);
        n.TentativasConsulta.Should().Be(1);
        n.TentativasPost.Should().Be(1);
    }

    [Fact(DisplayName = "Aplicar: os contadores de tentativa SOMAM sobre o valor ja gravado")]
    public async Task Aplicar_ContadoresSomam()
    {
        var id = Semear(_tenant, n => { n.TentativasConsulta = 5; n.TentativasPost = 3; });
        using var a = Novo(_tenant);

        await a.Store.AplicarDecisaoAsync(id, Decisao(), Agora, consultou: true, postou: true);

        var n = Ler(id);
        n.TentativasConsulta.Should().Be(6);
        n.TentativasPost.Should().Be(4);
    }

    [Fact(DisplayName = "Aplicar: sem consultou/postou, datas e contadores de tentativa ficam como estavam")]
    public async Task Aplicar_SemFlags_NaoMexe()
    {
        var antes = new DateTime(2026, 10, 8, 14, 0, 0);
        var id = Semear(_tenant, n =>
        {
            n.UltimaConsultaEm = antes; n.UltimoPostEm = antes; n.TentativasConsulta = 4; n.TentativasPost = 2;
        });
        using var a = Novo(_tenant);

        await a.Store.AplicarDecisaoAsync(id, Decisao(), Agora, consultou: false, postou: false);

        var n = Ler(id);
        n.UltimaConsultaEm.Should().Be(antes);
        n.UltimoPostEm.Should().Be(antes);
        n.TentativasConsulta.Should().Be(4);
        n.TentativasPost.Should().Be(2);
    }

    [Fact(DisplayName = "Aplicar: decisao sem agenda (ex.: ir para IntervencaoManual) grava ProximaTentativaEm nula")]
    public async Task Aplicar_AgendaNula()
    {
        var id = Semear(_tenant, n => n.ProximaTentativaEm = Agora.AddMinutes(-5));
        using var a = Novo(_tenant);

        await a.Store.AplicarDecisaoAsync(id, Decisao(NfePendenteEstados.IntervencaoManual, proxima: null), Agora, true, false);

        var n = Ler(id);
        n.Estado.Should().Be(NfePendenteEstados.IntervencaoManual);
        n.ProximaTentativaEm.Should().BeNull();
    }

    [Fact(DisplayName = "Aplicar: a agenda com offset do Brasil e gravada como UTC (12:00 -03:00 vira 15:00 UTC)")]
    public async Task Aplicar_ConverteParaUtc()
    {
        var id = Semear(_tenant);
        var comOffset = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.FromHours(-3));
        using var a = Novo(_tenant);

        await a.Store.AplicarDecisaoAsync(id, Decisao(proxima: comOffset), Agora, false, false);

        Ler(id).ProximaTentativaEm.Should().Be(new DateTime(2026, 10, 8, 15, 0, 0));
    }

    [Theory(DisplayName = "Aplicar: UltimaDecisao e truncada em 500 caracteres")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(499)]
    [InlineData(500)]
    [InlineData(501)]
    [InlineData(2000)]
    public async Task Aplicar_Trunca500(int tamanho)
    {
        var id = Semear(_tenant);
        using var a = Novo(_tenant);

        await a.Store.AplicarDecisaoAsync(id, Decisao(motivo: new string('x', tamanho)), Agora, false, false);

        Ler(id).UltimaDecisao!.Length.Should().Be(Math.Min(tamanho, 500));
    }

    [Fact(DisplayName = "Aplicar: o corte em 500 nao parte um emoji ao meio (fica com 499)")]
    public async Task Aplicar_Trunca_NaoPartePar()
    {
        var id = Semear(_tenant);
        var motivo = new string('a', 499) + "😀" + "zzz";   // o emoji ocupa os indices 499 e 500
        using var a = Novo(_tenant);

        await a.Store.AplicarDecisaoAsync(id, Decisao(motivo: motivo), Agora, false, false);

        var gravado = Ler(id).UltimaDecisao!;
        gravado.Length.Should().Be(499);
        gravado.Should().Be(new string('a', 499));
    }

    [Fact(DisplayName = "Aplicar: um emoji que termina exatamente na posicao 500 e preservado")]
    public async Task Aplicar_Trunca_EmojiNoLimiteExato()
    {
        var id = Semear(_tenant);
        var motivo = new string('a', 498) + "😀";             // 500 chars exatos
        using var a = Novo(_tenant);

        await a.Store.AplicarDecisaoAsync(id, Decisao(motivo: motivo), Agora, false, false);

        Ler(id).UltimaDecisao.Should().Be(motivo);
    }

    [Fact(DisplayName = "Aplicar: colunas legadas e dados da nota ficam EXATAMENTE como estavam")]
    public async Task Aplicar_LegadoIntacto()
    {
        var id = Semear(_tenant, n => n.PayloadJson = "{\"DataEmissao\":\"2026-10-08T09:01:12-03:00\",\"k\":\"v\"}");
        var antes = Ler(id);
        using var a = Novo(_tenant);

        await a.Store.AplicarDecisaoAsync(id, Decisao(proxima: new DateTimeOffset(Agora).AddMinutes(2), transitorias: 3, motivo: "x"), Agora, true, true);

        var depois = Ler(id);
        depois.Tentativas.Should().Be(antes.Tentativas).And.Be(2);
        depois.UltimaMensagemErro.Should().Be(antes.UltimaMensagemErro).And.Be("erro legado");
        depois.PayloadJson.Should().Be(antes.PayloadJson);
        depois.TipoNota.Should().Be(antes.TipoNota);
        depois.Referencia.Should().Be(antes.Referencia);
        depois.DataFalha.Should().Be(antes.DataFalha);
        depois.VendaId.Should().Be(antes.VendaId);
        depois.TenantId.Should().Be(antes.TenantId);
    }

    [Fact(DisplayName = "Aplicar: AguardandoCorrecao pode voltar para Ativa (a credencial foi corrigida)")]
    public async Task Aplicar_AguardandoCorrecao_VoltaParaAtiva()
    {
        var id = Semear(_tenant, n => n.Estado = NfePendenteEstados.AguardandoCorrecao);
        using var a = Novo(_tenant);

        var ok = await a.Store.AplicarDecisaoAsync(id, Decisao(NfePendenteEstados.Ativa), Agora, true, false);

        ok.Should().BeTrue();
        Ler(id).Estado.Should().Be(NfePendenteEstados.Ativa);
    }

    [Fact(DisplayName = "Aplicar: decisao de remocao (NovoEstado nulo) lanca ArgumentException e nada muda")]
    public async Task Aplicar_DecisaoDeRemocao_Lanca()
    {
        var id = Semear(_tenant);
        var antes = Tirar(Ler(id));
        using var a = Novo(_tenant);

        Func<Task> act = async () => await a.Store.AplicarDecisaoAsync(id, Decisao(novoEstado: null), Agora, true, false);

        await act.Should().ThrowAsync<ArgumentException>();
        Tirar(Ler(id)).Should().Be(antes);
    }

    [Theory(DisplayName = "Aplicar: estado invalido na decisao lanca ArgumentException e nada muda")]
    [InlineData("")]
    [InlineData("ativa")]
    [InlineData("Processando")]
    public async Task Aplicar_EstadoInvalido_Lanca(string estado)
    {
        var id = Semear(_tenant);
        var antes = Tirar(Ler(id));
        using var a = Novo(_tenant);

        Func<Task> act = async () => await a.Store.AplicarDecisaoAsync(id, Decisao(estado), Agora, true, false);

        await act.Should().ThrowAsync<ArgumentException>();
        Tirar(Ler(id)).Should().Be(antes);
    }

    [Theory(DisplayName = "Aplicar: contador negativo lanca ArgumentException e nada muda")]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public async Task Aplicar_ContadorNegativo_Lanca(int transitorias, int desconhecidas)
    {
        var id = Semear(_tenant);
        var antes = Tirar(Ler(id));
        using var a = Novo(_tenant);

        Func<Task> act = async () => await a.Store.AplicarDecisaoAsync(id, Decisao(transitorias: transitorias, desconhecidas: desconhecidas), Agora, true, false);

        await act.Should().ThrowAsync<ArgumentException>();
        Tirar(Ler(id)).Should().Be(antes);
    }

    [Theory(DisplayName = "Aplicar: agora sem Kind=Utc lanca ArgumentException e nada muda")]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    public async Task Aplicar_AgoraNaoUtc_Lanca(DateTimeKind kind)
    {
        var id = Semear(_tenant);
        var antes = Tirar(Ler(id));
        using var a = Novo(_tenant);

        Func<Task> act = async () => await a.Store.AplicarDecisaoAsync(id, Decisao(), new DateTime(2026, 10, 8, 15, 0, 0, kind), true, false);

        await act.Should().ThrowAsync<ArgumentException>();
        Tirar(Ler(id)).Should().Be(antes);
    }

    [Fact(DisplayName = "Aplicar: decisao nula lanca ArgumentNullException")]
    public async Task Aplicar_DecisaoNula_Lanca()
    {
        var id = Semear(_tenant);
        using var a = Novo(_tenant);

        Func<Task> act = async () => await a.Store.AplicarDecisaoAsync(id, null!, Agora, true, false);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact(DisplayName = "Aplicar: id inexistente devolve false")]
    public async Task Aplicar_IdInexistente()
    {
        using var a = Novo(_tenant);

        (await a.Store.AplicarDecisaoAsync(Guid.NewGuid(), Decisao(), Agora, true, false)).Should().BeFalse();
    }

    [Fact(DisplayName = "Aplicar: pendencia de OUTRO tenant devolve false e a linha do outro tenant nao muda")]
    public async Task Aplicar_OutroTenant()
    {
        var alheio = Semear(_outroTenant);
        var antes = Tirar(Ler(alheio));
        using var a = Novo(_tenant);

        var ok = await a.Store.AplicarDecisaoAsync(alheio, Decisao(NfePendenteEstados.IntervencaoManual), Agora, true, true);

        ok.Should().BeFalse();
        Tirar(Ler(alheio)).Should().Be(antes);
    }

    // ── IntervencaoManual terminal, verificado no banco ──────────────────

    [Fact(DisplayName = "TERMINAL: IntervencaoManual gravada -> Aplicar devolve false e NENHUMA coluna muda")]
    public async Task Terminal_Aplicar()
    {
        var id = Semear(_tenant, n =>
        {
            n.Estado = NfePendenteEstados.IntervencaoManual;
            n.ProximaTentativaEm = null;
            n.UltimaDecisao = "ErroDeRequisicao: manual";
            n.UltimaConsultaEm = new DateTime(2026, 10, 8, 13, 0, 0);
            n.UltimoPostEm = new DateTime(2026, 10, 8, 13, 1, 0);
            n.TentativasConsulta = 7; n.TentativasPost = 2;
            n.FalhasTransitoriasSeguidas = 3; n.FalhasDesconhecidasSeguidas = 1;
        });
        var antes = Tirar(Ler(id));
        using var a = Novo(_tenant);

        var ok = await a.Store.AplicarDecisaoAsync(id, Decisao(NfePendenteEstados.Ativa, new DateTimeOffset(Agora), 9, 9, "tentou reabrir"), Agora, true, true);

        ok.Should().BeFalse();
        Tirar(Ler(id)).Should().Be(antes);
    }

    [Fact(DisplayName = "TERMINAL: IntervencaoManual gravada -> RegravarPayload devolve false e o payload nao muda")]
    public async Task Terminal_Regravar()
    {
        var id = Semear(_tenant, n => n.Estado = NfePendenteEstados.IntervencaoManual);
        var antes = Tirar(Ler(id));
        using var a = Novo(_tenant);

        var ok = await a.Store.RegravarPayloadAsync(id, "{\"DataEmissao\":\"2026-10-08T09:07:45-03:00\"}");

        ok.Should().BeFalse();
        Tirar(Ler(id)).Should().Be(antes);
    }

    [Fact(DisplayName = "TERMINAL: IntervencaoManual gravada -> Remover devolve false e a linha continua no banco")]
    public async Task Terminal_Remover()
    {
        var id = Semear(_tenant, n => n.Estado = NfePendenteEstados.IntervencaoManual);
        var antes = Tirar(Ler(id));
        using var a = Novo(_tenant);

        var ok = await a.Store.RemoverAsync(id);

        ok.Should().BeFalse();
        ExisteNoBanco(id).Should().BeTrue();
        Tirar(Ler(id)).Should().Be(antes);
    }

    [Fact(DisplayName = "TERMINAL, passo a passo: Ativa -> IntervencaoManual (true); dai em diante tudo devolve false")]
    public async Task Terminal_DepoisDeEntrar()
    {
        var id = Semear(_tenant);
        using var a = Novo(_tenant);

        (await a.Store.AplicarDecisaoAsync(id, Decisao(NfePendenteEstados.IntervencaoManual), Agora, true, false)).Should().BeTrue();
        var travada = Tirar(Ler(id));

        (await a.Store.AplicarDecisaoAsync(id, Decisao(NfePendenteEstados.Ativa), Agora, true, true)).Should().BeFalse();
        (await a.Store.RegravarPayloadAsync(id, "{\"novo\":1}")).Should().BeFalse();
        (await a.Store.RemoverAsync(id)).Should().BeFalse();
        (await a.Store.ObterElegiveisAsync("NFCE", Agora.AddDays(10))).Should().BeEmpty();

        Tirar(Ler(id)).Should().Be(travada);
        ExisteNoBanco(id).Should().BeTrue();
    }

    [Fact(DisplayName = "Estado gravado desconhecido (corrompido): Aplicar, Regravar e Remover devolvem false e nada muda")]
    public async Task EstadoDesconhecido_NaoMexe()
    {
        var id = Semear(_tenant, n => n.Estado = "Processando");
        var antes = Tirar(Ler(id));
        using var a = Novo(_tenant);

        (await a.Store.AplicarDecisaoAsync(id, Decisao(), Agora, true, true)).Should().BeFalse();
        (await a.Store.RegravarPayloadAsync(id, "{\"novo\":1}")).Should().BeFalse();
        (await a.Store.RemoverAsync(id)).Should().BeFalse();

        Tirar(Ler(id)).Should().Be(antes);
        ExisteNoBanco(id).Should().BeTrue();
    }

    // ── RegravarPayloadAsync ─────────────────────────────────────────────

    [Fact(DisplayName = "Regravar: altera SO o payload, e ja esta no banco quando a chamada devolve")]
    public async Task Regravar_SoOPayload()
    {
        var id = Semear(_tenant, n => { n.TentativasConsulta = 3; n.UltimaDecisao = "antes"; });
        var antes = Tirar(Ler(id));
        var novoPayload = "{\"DataEmissao\":\"2026-10-08T09:07:45-03:00\",\"Itens\":[]}";
        using var a = Novo(_tenant);

        var ok = await a.Store.RegravarPayloadAsync(id, novoPayload);

        ok.Should().BeTrue();
        Tirar(Ler(id)).Should().Be(antes with { PayloadJson = novoPayload });
    }

    [Theory(DisplayName = "Regravar: payload vazio lanca ArgumentException e nada muda")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Regravar_PayloadVazio_Lanca(string? payload)
    {
        var id = Semear(_tenant);
        var antes = Tirar(Ler(id));
        using var a = Novo(_tenant);

        Func<Task> act = async () => await a.Store.RegravarPayloadAsync(id, payload!);

        await act.Should().ThrowAsync<ArgumentException>();
        Tirar(Ler(id)).Should().Be(antes);
    }

    [Fact(DisplayName = "Regravar: id inexistente devolve false")]
    public async Task Regravar_IdInexistente()
    {
        using var a = Novo(_tenant);

        (await a.Store.RegravarPayloadAsync(Guid.NewGuid(), "{\"x\":1}")).Should().BeFalse();
    }

    [Fact(DisplayName = "Regravar: pendencia de OUTRO tenant devolve false e o payload dele nao muda")]
    public async Task Regravar_OutroTenant()
    {
        var alheio = Semear(_outroTenant);
        var antes = Tirar(Ler(alheio));
        using var a = Novo(_tenant);

        (await a.Store.RegravarPayloadAsync(alheio, "{\"x\":1}")).Should().BeFalse();

        Tirar(Ler(alheio)).Should().Be(antes);
    }

    // ── RemoverAsync ─────────────────────────────────────────────────────

    [Fact(DisplayName = "Remover: some para o tenant e as outras pendencias continuam")]
    public async Task Remover_ApagaSoAAlvo()
    {
        var alvo = Semear(_tenant);
        var outra = Semear(_tenant);
        using var a = Novo(_tenant);

        var ok = await a.Store.RemoverAsync(alvo);

        ok.Should().BeTrue();
        Visivel(_tenant, alvo).Should().BeFalse();
        Visivel(_tenant, outra).Should().BeTrue();
    }

    [Fact(DisplayName = "Remover: id inexistente devolve false")]
    public async Task Remover_IdInexistente()
    {
        using var a = Novo(_tenant);

        (await a.Store.RemoverAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact(DisplayName = "Remover: pendencia de OUTRO tenant devolve false e continua la")]
    public async Task Remover_OutroTenant()
    {
        var alheio = Semear(_outroTenant);
        var antes = Tirar(Ler(alheio));
        using var a = Novo(_tenant);

        (await a.Store.RemoverAsync(alheio)).Should().BeFalse();

        ExisteNoBanco(alheio).Should().BeTrue();
        Tirar(Ler(alheio)).Should().Be(antes);
    }

    // ── Construtor ───────────────────────────────────────────────────────

    [Fact(DisplayName = "Construtor com IUnitOfWork nulo lanca ArgumentNullException")]
    public void Construtor_Nulo()
    {
        var act = () => new FiscalRecoveryStore(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
