using ERP.Application.Fiscal.Recovery;
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace ERP.Api.BackgroundServices;

/// <summary>
/// Worker da recuperacao fiscal de NFC-e (Etapa 4A-6b). REGISTRADO, mas INERTE por construcao: enquanto
/// FiscalRecovery__TenantsHabilitados estiver vazia (o padrao), cada ciclo so registra o batimento e retorna, SEM abrir escopo,
/// SEM consultar o banco e SEM chamar a Focus.
///
/// Com tenants habilitados, cada ciclo: (1) UMA consulta restrita aos tenants habilitados que tem NFC-e elegivel; (2) por tenant, um
/// escopo NOVO em que o IRequestTenant.TenantId e definido ANTES de resolver qualquer servico que dependa do AppDbContext (o contexto
/// copia o tenant uma unica vez, na construcao: e o "CRITICO" do worker antigo), le a configuracao fiscal DAQUELE tenant (token e
/// ambiente) e chama o orquestrador. A falha de um tenant nao derruba os outros; o cancelamento encerra limpo.
///
/// NAO e um substituto do worker antigo para NF-e (que continua la) e nao tem mutex entre instancias (Etapa 5): com duas instancias da
/// API ao mesmo tempo (por exemplo, durante um deploy), a protecao e o GET antes de qualquer POST e a idempotencia da ref.
/// </summary>
public class NfeRecoveryHostedService : BackgroundService
{
    public static readonly TimeSpan IntervaloPadrao = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan AtrasoInicialPadrao = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan IntervaloEntreAlertas = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NfeRecoveryHostedService> _logger;
    private readonly IFiscalRecoverySwitch _interruptor;
    private readonly IFiscalRecoveryHeartbeat _batimento;
    private readonly TimeSpan _atrasoInicial;
    private readonly TimeSpan _intervalo;
    private readonly Func<DateTimeOffset> _relogio;
    private readonly Dictionary<Guid, DateTimeOffset> _ultimoAlertaPorTenant = new();   // um ciclo por vez: sem concorrencia

    public NfeRecoveryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<NfeRecoveryHostedService> logger,
        IFiscalRecoverySwitch interruptor,
        IFiscalRecoveryHeartbeat batimento,
        TimeSpan? atrasoInicial = null,
        TimeSpan? intervalo = null,
        Func<DateTimeOffset>? relogio = null)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interruptor = interruptor ?? throw new ArgumentNullException(nameof(interruptor));
        _batimento = batimento ?? throw new ArgumentNullException(nameof(batimento));
        _atrasoInicial = atrasoInicial ?? AtrasoInicialPadrao;
        _intervalo = intervalo ?? IntervaloPadrao;
        _relogio = relogio ?? (() => DateTimeOffset.UtcNow);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "NfeRecoveryHostedService iniciado (ciclo de {Intervalo} s, tenants habilitados: {Quantidade}).",
            _intervalo.TotalSeconds, _interruptor.TenantsHabilitados.Count);

        try { await Task.Delay(_atrasoInicial, stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecutarCicloAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Uma falha de ciclo (ex.: banco fora do ar) nao derruba o loop: tenta de novo no proximo.
                _logger.LogError(ex, "NfeRecoveryHostedService: falha no ciclo da recuperacao fiscal.");
            }

            try { await Task.Delay(_intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("NfeRecoveryHostedService encerrado.");
    }

    /// <summary>Um ciclo. Com a lista de tenants vazia: so o batimento, sem escopo, sem banco, sem Focus.</summary>
    internal async Task ExecutarCicloAsync(CancellationToken ct)
    {
        _batimento.RegistrarCiclo();

        var habilitados = _interruptor.TenantsHabilitados;
        if (habilitados.Count == 0)
            return;

        var tenants = await DescobrirTenantsComTrabalhoAsync(habilitados.ToArray(), _relogio().UtcDateTime, ct);

        foreach (var tenantId in tenants)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await ProcessarTenantAsync(tenantId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NfeRecoveryHostedService: falha processando o tenant {TenantId}; os demais seguem.", tenantId);
            }
        }
    }

    /// <summary>
    /// Consulta SEM filtro de tenant, mas RESTRITA aos tenants habilitados: so le TenantId, nunca dado de negocio. Retorna apenas
    /// quem tem NFC-e elegivel (Ativa ou AguardandoCorrecao, com a agenda vencida).
    /// </summary>
    private async Task<List<Guid>> DescobrirTenantsComTrabalhoAsync(Guid[] habilitados, DateTime agoraUtc, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.NfePendentes
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(n => habilitados.Contains(n.TenantId)
                     && n.TipoNota == "NFCE"
                     && (n.Estado == NfePendenteEstados.Ativa || n.Estado == NfePendenteEstados.AguardandoCorrecao)
                     && (n.ProximaTentativaEm == null || n.ProximaTentativaEm <= agoraUtc))
            .Select(n => n.TenantId)
            .Distinct()
            .ToListAsync(ct);
    }

    internal virtual async Task ProcessarTenantAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        // CRITICO: o tenant ANTES de resolver qualquer servico que dependa do AppDbContext (o contexto o copia uma vez, ao ser construido).
        var requestTenant = scope.ServiceProvider.GetRequiredService<IRequestTenant>();
        requestTenant.TenantId = tenantId;

        var config = await scope.ServiceProvider.GetRequiredService<IFiscalConfigurationProvider>().ObterConfiguracaoAsync();
        var contexto = new RecoveryTenantContext(config.TokenFocusNfe ?? string.Empty, config.UsarAmbienteProducao);

        var orquestrador = scope.ServiceProvider.GetRequiredService<NfePendenteRecoveryOrchestrator>();
        var resultado = await orquestrador.ProcessarTenantAsync(contexto, ct);

        RegistrarResumo(tenantId, config.UsarAmbienteProducao, resultado);
    }

    private void RegistrarResumo(Guid tenantId, bool emProducao, RecoveryCycleResult r)
    {
        var atividade = r.Autorizadas + r.Rejeitadas + r.Agendadas + r.AguardandoCorrecao + r.IntervencaoManual
                      + r.Ignoradas + r.ErrosDeProcessamento + r.FalhasDePersistencia;

        if (atividade == 0)
            return;

        _logger.LogInformation(
            "Recuperacao fiscal: tenant {TenantId} ({Ambiente}): {Elegiveis} elegivel(is), {Autorizadas} autorizada(s), {Rejeitadas} rejeitada(s), " +
            "{Agendadas} agendada(s), {AguardandoCorrecao} aguardando correcao, {IntervencaoManual} em intervencao manual, {Ignoradas} ignorada(s), " +
            "{Erros} erro(s) de processamento, {FalhasPersistencia} falha(s) de persistencia.",
            tenantId, emProducao ? "PRODUCAO" : "homologacao", r.Elegiveis, r.Autorizadas, r.Rejeitadas, r.Agendadas,
            r.AguardandoCorrecao, r.IntervencaoManual, r.Ignoradas, r.ErrosDeProcessamento, r.FalhasDePersistencia);

        if (r.IntervencaoManual + r.ErrosDeProcessamento + r.FalhasDePersistencia > 0 && DeveAlertar(tenantId))
        {
            _logger.LogWarning(
                "Recuperacao fiscal: ATENCAO no tenant {TenantId}: {IntervencaoManual} pendencia(s) em intervencao manual, " +
                "{Erros} erro(s) de processamento e {FalhasPersistencia} falha(s) de persistencia neste ciclo. Revise a fila de pendencias fiscais.",
                tenantId, r.IntervencaoManual, r.ErrosDeProcessamento, r.FalhasDePersistencia);
        }
    }

    /// <summary>Alerta deduplicado: no maximo um a cada 30 minutos por tenant.</summary>
    private bool DeveAlertar(Guid tenantId)
    {
        var agora = _relogio();

        if (_ultimoAlertaPorTenant.TryGetValue(tenantId, out var ultimo) && agora - ultimo < IntervaloEntreAlertas)
            return false;

        _ultimoAlertaPorTenant[tenantId] = agora;
        return true;
    }
}
