using ERP.Application.Fiscal.Focus;
using ERP.Application.Fiscal.Recovery;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using ERP.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace ERP.Tests.Fiscal;

/// <summary>Apoio dos testes do orquestrador (4A-5d). Nada aqui chama a Focus.</summary>
internal static class RecoveryFixtures
{
    public const string DataAntiga = "2026-10-08T09:01:12-03:00";

    // Payload no formato do JsonConvert.SerializeObject(FocusNfceRequest): PascalCase, compacto, nulos explicitos.
    public const string PayloadNfce = """
        {"CnpjEmitente":"12820608000141","NaturezaOperacao":"VENDA AO CONSUMIDOR","DataEmissao":"2026-10-08T09:01:12-03:00","TipoDocumento":"1","PresencaComprador":"1","ConsumidorFinal":"1","FinalidadeEmissao":"1","ModalidadeFrete":"9","ValorFrete":null,"CnpjTransportador":null,"CpfTransportador":null,"NomeTransportador":null,"InscricaoEstadualTransportador":null,"EnderecoTransportador":null,"MunicipioTransportador":null,"UfTransportador":null,"VeiculoPlaca":null,"VeiculoUf":null,"Volumes":null,"InformacoesAdicionaisContribuinte":"Venda de 08/10/2026 – vendedor João | \"balcão\" | \u00e7\u00e3o","Nome":"CONSUMIDOR FINAL","CpfCnpj":null,"LogradouroDestinatario":null,"NumeroDestinatario":null,"BairroDestinatario":null,"MunicipioDestinatario":null,"UfDestinatario":null,"CepDestinatario":null,"IeDestinatario":null,"IndicadorIeDestinatario":null,"Itens":[{"NumeroItem":"1","CodigoProduto":"1001","Descricao":"PARAFUSO SEXTAVADO 1/4\" X 2\" AÇO INOX","Cfop":"5102","UnidadeComercial":"UN","QuantidadeComercial":"10.0000","ValorUnitarioComercial":"1.5000","ValorBruto":"15.00","CodigoNcm":"73181500","IcmsOrigem":"0","IcmsSituacaoTributaria":"102","IcmsModalidadeBaseCalculoSt":null,"IcmsMargemValorAdicionadoSt":null,"IcmsBaseCalculoSt":null,"IcmsAliquotaSt":null,"IcmsValorSt":null,"PisSituacaoTributaria":"07","CofinsSituacaoTributaria":"07","ChaveAcessoDfeReferenciado":null,"NumeroItemDfeReferenciado":null}],"Pagamentos":[{"FormaPagamento":"01","ValorPagamento":"15.00"}],"NotasReferenciadas":null}
        """;
}

/// <summary>Respostas prontas da Focus, vindas das fixtures reais.</summary>
internal static class Resp
{
    public static FocusResponse Http(int status, string corpo) => FocusResponseParser.FromHttp(status, corpo);
    public static FocusResponse Autorizada() => Http(200, FocusFixtures.Autorizada);
    public static FocusResponse Processando() => Http(200, FocusFixtures.Processando);
    public static FocusResponse NaoEncontrado() => Http(404, FocusFixtures.NaoEncontrado);
    public static FocusResponse Rejeitada704() => Http(200, FocusFixtures.Rejeitada704);
    public static FocusResponse RejeitadaOutroCodigo() => Http(200, FocusFixtures.RejeicaoOutroCodigoSefaz);
    public static FocusResponse JaProcessado() => Http(422, FocusFixtures.AlreadyProcessed);
    public static FocusResponse PermissaoNegada() => Http(401, FocusFixtures.PermissaoNegada);
    public static FocusResponse Timeout() => FocusResponseParser.FromTransportError("Timeout: simulado");
}

internal sealed record FotoPendencia(
    string Estado, DateTime? ProximaTentativaEm, string? UltimaDecisao, DateTime? UltimaConsultaEm,
    DateTime? UltimoPostEm, int TentativasPost, int TentativasConsulta, int FalhasTransitorias,
    int FalhasDesconhecidas, string PayloadJson, int Tentativas, string? UltimaMensagemErro,
    string TipoNota, string Referencia, DateTime DataFalha, Guid VendaId)
{
    public static FotoPendencia De(NfePendente n) => new(
        n.Estado, n.ProximaTentativaEm, n.UltimaDecisao, n.UltimaConsultaEm, n.UltimoPostEm,
        n.TentativasPost, n.TentativasConsulta, n.FalhasTransitoriasSeguidas, n.FalhasDesconhecidasSeguidas,
        n.PayloadJson, n.Tentativas, n.UltimaMensagemErro, n.TipoNota, n.Referencia, n.DataFalha, n.VendaId);
}

/// <summary>
/// SQLite real, contexto NoTracking (como a producao, Program.cs linha 54). O tenant do filtro e um
/// AsyncLocal estatico: cada contexto construido o atualiza, entao a store do tenant testado e criada
/// por ULTIMO e as leituras de verificacao usam contextos novos.
/// </summary>
internal sealed class RecoveryAmbiente : IDisposable
{
    private readonly SqliteConnection _conexao;

    public Guid Tenant { get; } = Guid.NewGuid();

    public RecoveryAmbiente()
    {
        _conexao = new SqliteConnection($"DataSource=recoveryorq_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        _conexao.Open();

        using var ctx = NovoContexto(Tenant);
        ctx.Database.EnsureCreated();
    }

    public void Dispose() => _conexao.Dispose();

    public AppDbContext NovoContexto(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_conexao)
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;

        return new AppDbContext(options, new ERP.Tests.FakeRequestTenant { TenantId = tenantId });
    }

    public Guid Semear(Action<NfePendente>? ajustar = null, string tipoNota = "NFCE", DateTime? dataFalha = null)
    {
        using var ctx = NovoContexto(Tenant);

        var nota = new NfePendente
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            VendaId = Guid.NewGuid(),
            TipoNota = tipoNota,
            PayloadJson = RecoveryFixtures.PayloadNfce,
            Referencia = Guid.NewGuid().ToString(),
            DataFalha = dataFalha ?? new DateTime(2026, 10, 8, 9, 0, 0),
            Tentativas = 2,
            UltimaMensagemErro = "erro legado"
        };
        ajustar?.Invoke(nota);

        ctx.NfePendentes.Add(nota);
        ctx.SaveChanges();
        return nota.Id;
    }

    public NfePendente Ler(Guid id)
    {
        using var ctx = NovoContexto(Tenant);
        return ctx.NfePendentes.IgnoreQueryFilters().AsNoTracking().Single(n => n.Id == id);
    }

    public bool ExisteNoBanco(Guid id)
    {
        using var ctx = NovoContexto(Tenant);
        return ctx.NfePendentes.IgnoreQueryFilters().AsNoTracking().Any(n => n.Id == id);
    }

    /// <summary>Visivel para o tenant, com o filtro global aplicado (vale tanto para remocao fisica quanto logica).</summary>
    public bool Visivel(Guid id)
    {
        using var ctx = NovoContexto(Tenant);
        return ctx.NfePendentes.AsNoTracking().Any(n => n.Id == id);
    }

    /// <summary>Altera a linha por fora da store (simula outra instancia ou um operador).</summary>
    public void ModificarPorFora(Guid id, Action<NfePendente> alterar)
    {
        using var ctx = NovoContexto(Tenant);
        var nota = ctx.NfePendentes.IgnoreQueryFilters().AsTracking().Single(n => n.Id == id);
        alterar(nota);
        ctx.SaveChanges();
    }

    public AlvoStore NovaStore()
    {
        var ctx = NovoContexto(Tenant);
        var uow = new ERP.Infrastructure.UnitOfWork.UnitOfWork(
            ctx,
            Mock.Of<IProductRepository>(),
            Mock.Of<ICustomerRepository>(),
            Mock.Of<ISaleRepository>(),
            Mock.Of<ICategoryRepository>(),
            Mock.Of<IUserRepository>(),
            new ERP.Tests.FakeRequestTenant { TenantId = Tenant });

        return new AlvoStore(uow);
    }
}

internal sealed class AlvoStore : IDisposable
{
    private readonly ERP.Infrastructure.UnitOfWork.UnitOfWork _uow;

    public AlvoStore(ERP.Infrastructure.UnitOfWork.UnitOfWork uow)
    {
        _uow = uow;
        Store = new FiscalRecoveryStore(uow);
    }

    public FiscalRecoveryStore Store { get; }

    public void Dispose() => _uow.Dispose();
}

internal sealed class RelogioDeTeste
{
    public RelogioDeTeste(DateTimeOffset inicio) { Agora = inicio; }

    public DateTimeOffset Agora { get; set; }

    public Func<DateTimeOffset> Ler => () => Agora;

    public void DefinirUtc(DateTime utc) => Agora = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
}

/// <summary>
/// Decorator da store REAL que registra a ordem dos eventos e permite injetar falha (excecao ANTES
/// da chamada real) ou um gancho (executado antes da chamada real, ex.: mudar a linha por fora).
/// </summary>
internal sealed class StoreRegistradora : IFiscalRecoveryStore
{
    private readonly IFiscalRecoveryStore _real;
    private readonly List<string> _eventos;
    private readonly Dictionary<string, Exception> _falhas;
    private readonly Dictionary<string, Func<Task>> _antes;

    public StoreRegistradora(
        IFiscalRecoveryStore real, List<string> eventos,
        Dictionary<string, Exception> falhas, Dictionary<string, Func<Task>> antes)
    {
        _real = real;
        _eventos = eventos;
        _falhas = falhas;
        _antes = antes;
    }

    private async Task EntrarAsync(string metodo)
    {
        _eventos.Add("Store." + metodo);

        if (_antes.TryGetValue(metodo, out var gancho))
            await gancho();

        if (_falhas.TryGetValue(metodo, out var falha))
            throw falha;
    }

    public async Task<IReadOnlyList<NfePendente>> ObterElegiveisAsync(string tipoNota, DateTime agoraUtc)
    {
        await EntrarAsync("Obter");
        return await _real.ObterElegiveisAsync(tipoNota, agoraUtc);
    }

    public async Task<bool> RegravarPayloadAsync(Guid id, string payloadJson)
    {
        await EntrarAsync("Regravar");
        return await _real.RegravarPayloadAsync(id, payloadJson);
    }

    public async Task<bool> AplicarDecisaoAsync(Guid id, RecoveryDecision decisao, DateTime agoraUtc, bool consultou, bool postou)
    {
        await EntrarAsync("Aplicar");
        return await _real.AplicarDecisaoAsync(id, decisao, agoraUtc, consultou, postou);
    }

    public async Task<bool> RemoverAsync(Guid id)
    {
        await EntrarAsync("Remover");
        return await _real.RemoverAsync(id);
    }
}

internal sealed record ChamadaFocus(string Metodo, string Referencia, string Token, bool IsProducao, string? Corpo);

/// <summary>Cliente da Focus roteirizado: cada chamada consome o proximo item da fila (resposta, excecao ou funcao).</summary>
internal sealed class FocusRoteirizado : IFocusReferenceClient
{
    private readonly List<string> _eventos;
    private readonly Queue<object> _gets = new();
    private readonly Queue<object> _posts = new();

    public FocusRoteirizado(List<string> eventos) { _eventos = eventos; }

    public List<ChamadaFocus> Chamadas { get; } = new();

    /// <summary>Executado dentro do POST, antes de responder (ex.: ler o banco nesse instante).</summary>
    public Func<string, Task>? AoPostar { get; set; }

    public FocusRoteirizado EnfileirarGet(params object[] itens)
    {
        foreach (var item in itens) _gets.Enqueue(item);
        return this;
    }

    public FocusRoteirizado EnfileirarPost(params object[] itens)
    {
        foreach (var item in itens) _posts.Enqueue(item);
        return this;
    }

    public async Task<FocusResponse> ConsultarAsync(
        FocusDocumentType tipo, string referencia, string token, bool isProducao, CancellationToken ct = default)
    {
        if (tipo != FocusDocumentType.Nfce)
            throw new InvalidOperationException("O orquestrador da 4A-5d so consulta NFC-e.");

        _eventos.Add("Focus.GET");
        Chamadas.Add(new ChamadaFocus("GET", referencia, token, isProducao, null));
        return await ProximoAsync(_gets, "GET", ct);
    }

    public async Task<FocusResponse> EnviarNfceAsync(
        string referencia, string jsonBody, string token, bool isProducao, CancellationToken ct = default)
    {
        _eventos.Add("Focus.POST");
        Chamadas.Add(new ChamadaFocus("POST", referencia, token, isProducao, jsonBody));

        if (AoPostar is not null)
            await AoPostar(jsonBody);

        return await ProximoAsync(_posts, "POST", ct);
    }

    private static async Task<FocusResponse> ProximoAsync(Queue<object> fila, string metodo, CancellationToken ct)
    {
        if (fila.Count == 0)
            throw new InvalidOperationException($"Roteiro de teste esgotado: chamada {metodo} inesperada.");

        var item = fila.Dequeue();

        switch (item)
        {
            case FocusResponse resposta:
                return resposta;
            case Exception excecao:
                throw excecao;
            case Func<CancellationToken, Task<FocusResponse>> funcao:
                return await funcao(ct);
            default:
                throw new InvalidOperationException("Item de roteiro invalido.");
        }
    }
}
