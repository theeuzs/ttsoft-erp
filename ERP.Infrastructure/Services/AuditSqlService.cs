// ── ERP.Infrastructure/Services/AuditSqlService.cs ───────────────────────────
// S2.6 — Audit trail para operações ExecuteSqlRawAsync.
//
// O interceptor EF Core (GerarLogsAuditoria no AppDbContext) já audita
// operações via SaveChanges. Mas ExecuteSqlRawAsync bypassa o interceptor.
//
// Este serviço deve ser chamado ANTES de cada ExecuteSqlRawAsync crítico
// (UPDATE de status financeiro, DELETE, etc.) para garantir trilha completa.
//
// Uso:
//   _auditSql.Log("UPDATE ContasPagar", id, _tenant.TenantId, _tenant.UserName,
//                 new { Status = "Pago", Id = id });
//   await _ctx.Database.ExecuteSqlRawAsync(...);
// ─────────────────────────────────────────────────────────────────────────────
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Loga operações SQL diretas (ExecuteSqlRawAsync) que bypassam o interceptor EF Core.
/// Injete via DI e chame antes de cada ExecuteSqlRawAsync crítico.
/// </summary>
public class AuditSqlService
{
    private readonly ILogger<AuditSqlService> _logger;

    public AuditSqlService(ILogger<AuditSqlService> logger)
        => _logger = logger;

    /// <summary>
    /// Registra uma operação SQL direto antes de executá-la.
    /// O log é estruturado — indexado por App Insights/Seq/Grafana.
    /// </summary>
    public void Log(
        string operacao,
        Guid   entityId,
        Guid   tenantId,
        string operadorNome,
        object? dados = null)
    {
        _logger.LogInformation(
            "SQL_AUDIT | {Operacao} | EntityId={EntityId} | TenantId={TenantId} | " +
            "Operador={Operador} | Dados={Dados}",
            operacao,
            entityId,
            tenantId,
            operadorNome,
            dados != null ? JsonSerializer.Serialize(dados) : "{}");
    }
}
