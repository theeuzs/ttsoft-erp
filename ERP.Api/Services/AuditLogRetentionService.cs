// ── ERP.Api/Services/AuditLogRetentionService.cs ────────────────────────────
using ERP.Domain.Entities;
using ERP.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace ERP.Api.Services;

/// <summary>
/// Plano de 30 dias, item 4/4 — sem isso, AuditLogs cresce pra sempre: toda
/// operação de Add/Modify/Delete em QUALQUER entidade de QUALQUER tenant vira
/// uma linha aqui (ver AppDbContext.GerarLogsAuditoria). Numa loja com
/// movimento real, isso é bastante linha por dia — sem limpeza, vira o maior
/// consumidor de espaço/índice do banco cedo ou tarde.
///
/// Mesmo padrão do CleanupCadastrosExpiradosService: BackgroundService,
/// delay inicial pra não concorrer com migrations no boot, fail-safe (erro
/// aqui não pode derrubar a API).
///
/// Usa AuditLog.CreatedAt (UTC, confiável) pra decidir o que é "antigo" — não
/// AuditLog.Timestamp, que até 21/08 usava DateTime.Now (mesmo padrão de bug
/// do S21, corrigido separadamente, mas CreatedAt já era UTC desde sempre).
///
/// ExecuteDeleteAsync — deleta direto no banco, sem carregar as linhas na
/// memória da API primeiro (podem ser dezenas de milhares) — mesmo padrão já
/// estabelecido no projeto (ExecuteUpdateAsync em outros repositórios).
/// </summary>
public class AuditLogRetentionService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(24);

    public AuditLogRetentionService(IServiceProvider sp) => _sp = sp;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // 10 minutos — depois do CleanupCadastrosExpiradosService (5 min),
        // pra não competir por conexão de banco logo no boot.
        await Task.Delay(TimeSpan.FromMinutes(10), ct);

        while (!ct.IsCancellationRequested)
        {
            await LimparAntigosAsync(ct);
            await Task.Delay(Intervalo, ct);
        }
    }

    private async Task LimparAntigosAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // Configurável via appsettings ("AuditLog:RetencaoDias") — sem
            // decidir por conta própria qual é o período "certo" pra reter
            // (isso é decisão de negócio/compliance do Matheus, não técnica).
            // 180 dias como default só pra não deixar sem limite nenhum
            // enquanto ele não define o valor real.
            var dias    = config.GetValue<int?>("AuditLog:RetencaoDias") ?? 180;
            var limite  = DateTime.UtcNow.AddDays(-dias);

            var removidos = await db.AuditLogs
                .IgnoreQueryFilters() // limpa de TODOS os tenants, não só um
                .Where(a => a.CreatedAt < limite)
                .ExecuteDeleteAsync(ct);

            if (removidos > 0)
                Log.Information(
                    "AuditLogRetentionService: {N} registros de auditoria com mais de {Dias} dias removidos",
                    removidos, dias);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AuditLogRetentionService: erro ao limpar logs antigos");
        }
    }
}
