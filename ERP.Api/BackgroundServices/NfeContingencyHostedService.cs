using ERP.Application.DTOs.FocusNfe;
using ERP.Application.Interfaces;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ERP.Api.BackgroundServices;

/// <summary>
/// S25 (18/08) — item pendente desde a auditoria de 13/08: o retry de notas
/// fiscais em contingência (NfeContingencyWorker) só rodava dentro do WPF,
/// como Fire-and-forget disparado no login (App.xaml.cs) — ou seja, uma
/// nota que falhou só era reprocessada enquanto alguém tivesse o PDV de
/// alguma loja aberto. Vendas de marketplace (via OrderProcessingService,
/// sem WPF envolvido nenhum) ficariam presas em contingência pra sempre se
/// a primeira tentativa falhasse fora do horário de expediente.
///
/// Esse serviço roda dentro da própria API, continuamente, processando
/// TODOS os tenants — não só o que estiver logado no momento. Detalhe
/// crítico de multi-tenancy: AppDbContext (construtor da API) só copia
/// IRequestTenant.TenantId pro filtro de tenant UMA VEZ, na hora que é
/// construído — por isso o IRequestTenant precisa ser setado ANTES de
/// resolver qualquer serviço que dependa do AppDbContext, em cada scope.
/// </summary>
public class NfeContingencyHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NfeContingencyHostedService> _logger;
    private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromMinutes(2);

    public NfeContingencyHostedService(IServiceScopeFactory scopeFactory, ILogger<NfeContingencyHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NfeContingencyHostedService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarTodosOsTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // S25 FIX: era `catch { }` no worker original — agora loga de
                // verdade. Um erro aqui (ex: banco fora do ar) não deve derrubar
                // o loop inteiro, só o ciclo atual — tenta de novo no próximo.
                _logger.LogError(ex, "NfeContingencyHostedService: falha no ciclo de contingência.");
            }

            try { await Task.Delay(IntervaloEntreCiclos, stoppingToken); }
            catch (OperationCanceledException) { /* aplicação está parando */ }
        }

        _logger.LogInformation("NfeContingencyHostedService encerrado.");
    }

    /// <summary>Consulta SEM filtro de tenant — é o único jeito de descobrir
    /// quais tenants têm nota pendente antes de escolher o contexto de cada
    /// um. IgnoreQueryFilters aqui é intencional e seguro: só lê TenantId,
    /// nunca dado de negócio de um tenant misturado com outro.</summary>
    internal async Task<List<Guid>> ObterTenantsComNotasPendentesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await ctx.NfePendentes
            .IgnoreQueryFilters()
            .Select(n => n.TenantId)
            .Distinct()
            .ToListAsync(ct);
    }

    internal async Task ProcessarTodosOsTenantsAsync(CancellationToken ct)
    {
        var tenantIds = await ObterTenantsComNotasPendentesAsync(ct);
        if (tenantIds.Count == 0) return;

        foreach (var tenantId in tenantIds)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                await ProcessarTenantAsync(tenantId, ct);
            }
            catch (Exception ex)
            {
                // Um tenant com problema (ex: token Focus inválido) não pode
                // travar o processamento dos outros.
                _logger.LogError(ex, "NfeContingencyHostedService: falha processando o tenant {TenantId}.", tenantId);
            }
        }
    }

    internal async Task ProcessarTenantAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        // CRÍTICO: seta o tenant ANTES de resolver qualquer serviço que
        // dependa do AppDbContext — ver o comentário da classe.
        var requestTenant = scope.ServiceProvider.GetRequiredService<IRequestTenant>();
        requestTenant.TenantId = tenantId;

        var contingencyService = scope.ServiceProvider.GetRequiredService<INfeContingencyService>();
        var pendentes = await contingencyService.ObterNotasPendentesAsync();
        if (!pendentes.Any()) return;

        if (!await contingencyService.VerificarConexaoSefazAsync())
        {
            _logger.LogWarning("NfeContingencyHostedService: sem conexão com a SEFAZ, tenant {TenantId} fica pra próxima.", tenantId);
            return;
        }

        var configProvider = scope.ServiceProvider.GetRequiredService<IFiscalConfigurationProvider>();
        var config = await configProvider.ObterConfiguracaoAsync();

        if (string.IsNullOrWhiteSpace(config.TokenFocusNfe))
        {
            _logger.LogWarning("NfeContingencyHostedService: tenant {TenantId} sem token Focus configurado — {Count} nota(s) pendente(s) não puderam ser tentadas.", tenantId, pendentes.Count());
            return;
        }

        var nfceService = scope.ServiceProvider.GetRequiredService<INfceEmissionService>();
        var nfeService  = scope.ServiceProvider.GetRequiredService<INfeEmissionService>();
        var saleService = scope.ServiceProvider.GetRequiredService<ISaleService>();
        string ambienteSefaz = config.UsarAmbienteProducao ? "Produção" : "Homologação";

        foreach (var nota in pendentes)
        {
            if (ct.IsCancellationRequested) return;

            try
            {
                var request = Newtonsoft.Json.JsonConvert.DeserializeObject<FocusNfceRequest>(nota.PayloadJson);
                bool sucesso = false; string mensagem = ""; string urlDanfe = "";

                if (nota.TipoNota == "NFCE")
                {
                    var result = await nfceService.EmitirNfceAsync(nota.Referencia, request!, config.TokenFocusNfe, config.UsarAmbienteProducao);
                    sucesso = result.Sucesso; mensagem = result.Mensagem; urlDanfe = result.UrlDanfe;
                }
                else
                {
                    var result = await nfeService.EmitirNfeA4Async(nota.Referencia, request!, config.TokenFocusNfe, config.UsarAmbienteProducao);
                    sucesso = result.Sucesso; mensagem = result.Mensagem; urlDanfe = result.UrlDanfe;
                }

                if (sucesso && !string.IsNullOrWhiteSpace(urlDanfe))
                {
                    await saleService.AtualizarDadosNfceAsync(nota.VendaId, urlDanfe, "Autorizada", ambienteSefaz, nota.Referencia);
                    await contingencyService.RemoverNotaPendenteAsync(nota.Id);
                    _logger.LogInformation("NfeContingencyHostedService: nota {Referencia} (tenant {TenantId}) autorizada em contingência.", nota.Referencia, tenantId);
                }
                else if (mensagem.Contains("UnprocessableEntity") || mensagem.Contains("erro_validacao_schema") || !mensagem.Contains("Erro de Comunicação"))
                {
                    // SEFAZ rejeitou por erro de validação (ex: NCM errado) — não é
                    // problema de conectividade, insistir não vai resolver.
                    await saleService.AtualizarDadosNfceAsync(nota.VendaId, "", "Rejeitada: " + mensagem, ambienteSefaz, nota.Referencia);
                    await contingencyService.RemoverNotaPendenteAsync(nota.Id);
                    _logger.LogWarning("NfeContingencyHostedService: nota {Referencia} (tenant {TenantId}) rejeitada: {Mensagem}", nota.Referencia, tenantId, mensagem);
                }
                else
                {
                    // Falha de comunicação de verdade — conta mais uma tentativa e deixa na fila.
                    await contingencyService.RegistrarFalhaTentativaAsync(nota.Id, mensagem);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("UnprocessableEntity") || ex.Message.Contains("erro_validacao_schema"))
                {
                    await saleService.AtualizarDadosNfceAsync(nota.VendaId, "", "Rejeitada: " + ex.Message, ambienteSefaz, nota.Referencia);
                    await contingencyService.RegistrarFalhaTentativaAsync(nota.Id, $"ERRO SEFAZ OFFLINE: {ex.Message}");
                }
                else
                {
                    await contingencyService.RegistrarFalhaTentativaAsync(nota.Id, ex.Message);
                }

                _logger.LogError(ex, "NfeContingencyHostedService: exceção processando nota {Referencia} (tenant {TenantId}).", nota.Referencia, tenantId);
            }
        }
    }
}
