// ERP.Api/BackgroundServices/NfeStatusReconciliationHostedService.cs
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
/// Achado real (25/09) — uma devolução foi autorizada pela SEFAZ (Status
/// 100) mas o ERP nunca soube, porque a resposta chegou depois da janela de
/// consulta de 3s embutida em NfeEmissionService. O mesmo problema já existia
/// (e já tinha sido resolvido) do lado da Nota Avulsa desde 19-20/08 — esse
/// serviço replica o mesmo padrão pra venda normal e devolução, que nunca
/// tinham sido conectadas a ele.
///
/// Responsabilidade única: CONSULTAR o resultado de documentos que já foram
/// submetidos à Focus e ficaram "Processando" — nunca reenviar. Reenviar um
/// documento em processando_autorizacao arriscaria duplicidade fiscal; é
/// exatamente esse risco que existe pra erro de COMUNICAÇÃO (documento nunca
/// chegou), tratado à parte por NfeContingencyHostedService — os dois
/// problemas são conceitualmente diferentes e não devem ser misturados
/// (ver análise de impacto, 25/09):
///
///   CONTINGÊNCIA (NfeContingencyHostedService/NfePendente)
///     → não conseguiu comunicar/enviar → precisa REENVIAR depois
///   PROCESSANDO (este serviço)
///     → conseguiu enviar, SEFAZ ainda não decidiu → só pode CONSULTAR
///
/// Mesmo cuidado multi-tenant do NfeContingencyHostedService: AppDbContext
/// (API) só copia IRequestTenant.TenantId pro filtro UMA VEZ, no construtor —
/// por isso o IRequestTenant precisa ser setado ANTES de resolver qualquer
/// serviço que dependa do AppDbContext, em cada escopo.
/// </summary>
public class NfeStatusReconciliationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NfeStatusReconciliationHostedService> _logger;
    private static readonly TimeSpan IntervaloEntreCiclos = TimeSpan.FromMinutes(1);

    public NfeStatusReconciliationHostedService(IServiceScopeFactory scopeFactory, ILogger<NfeStatusReconciliationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NfeStatusReconciliationHostedService iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessarTodosOsTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Um erro aqui (ex: banco fora do ar) não deve derrubar o
                // loop inteiro, só o ciclo atual — tenta de novo no próximo.
                _logger.LogError(ex, "NfeStatusReconciliationHostedService: falha no ciclo de reconciliação.");
            }

            try { await Task.Delay(IntervaloEntreCiclos, stoppingToken); }
            catch (OperationCanceledException) { /* aplicação está parando */ }
        }

        _logger.LogInformation("NfeStatusReconciliationHostedService encerrado.");
    }

    /// <summary>Consulta SEM filtro de tenant — só pra descobrir quais
    /// tenants têm algo pendente antes de escolher o contexto de cada um.
    /// IgnoreQueryFilters aqui é intencional e seguro: só lê TenantId, nunca
    /// dado de negócio de um tenant misturado com outro.</summary>
    internal async Task<List<Guid>> ObterTenantsComPendenciasAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenantsVenda = await ctx.Sales.IgnoreQueryFilters()
            .Where(s => s.NfceStatusFocus == "Processando")
            .Select(s => s.TenantId)
            .Distinct()
            .ToListAsync(ct);

        var tenantsDevolucao = await ctx.NotasFiscais.IgnoreQueryFilters()
            .Where(n => n.Status == "Processando" && n.Finalidade == "4")
            .Select(n => n.TenantId)
            .Distinct()
            .ToListAsync(ct);

        return tenantsVenda.Union(tenantsDevolucao).ToList();
    }

    internal async Task ProcessarTodosOsTenantsAsync(CancellationToken ct)
    {
        var tenantIds = await ObterTenantsComPendenciasAsync(ct);
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
                _logger.LogError(ex, "NfeStatusReconciliationHostedService: falha processando o tenant {TenantId}.", tenantId);
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

        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fiscalService = scope.ServiceProvider.GetRequiredService<IFiscalService>();

        var vendaIds = await ctx.Sales
            .Where(s => s.NfceStatusFocus == "Processando")
            .Select(s => s.Id)
            .ToListAsync(ct);

        var notaFiscalIds = await ctx.NotasFiscais
            .Where(n => n.Status == "Processando" && n.Finalidade == "4")
            .Select(n => n.Id)
            .ToListAsync(ct);

        if (vendaIds.Count == 0 && notaFiscalIds.Count == 0) return;

        foreach (var vendaId in vendaIds)
        {
            if (ct.IsCancellationRequested) return;
            try { await fiscalService.ReconciliarVendaProcessandoAsync(vendaId); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NfeStatusReconciliationHostedService: falha reconciliando venda {VendaId} (tenant {TenantId}).", vendaId, tenantId);
            }
        }

        foreach (var notaFiscalId in notaFiscalIds)
        {
            if (ct.IsCancellationRequested) return;
            try { await fiscalService.ReconciliarDevolucaoProcessandoAsync(notaFiscalId); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NfeStatusReconciliationHostedService: falha reconciliando devolução {NotaFiscalId} (tenant {TenantId}).", notaFiscalId, tenantId);
            }
        }
    }
}
