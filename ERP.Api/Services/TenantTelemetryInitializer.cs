// ── ERP.Api/Services/TenantTelemetryInitializer.cs ───────────────────────────
// F1.3 — Enriquece cada evento do Application Insights com TenantId.
//
// Sem isso, requests/exceptions/dependencies chegam no App Insights sem
// identificação de loja — impossível filtrar "erros só da Loja X" ou
// "latência média da Loja Y" no portal Azure.
//
// Com isso, cada telemetria recebe:
//   CustomDimensions["TenantId"]  → Guid da loja (filtrável em KQL/Workbooks)
//   CustomDimensions["UserName"]  → Nome do operador
//   cloud_RoleInstance            → Nome da máquina (já padrão do SDK)
//
// Exemplo de query KQL no App Insights para ver erros por loja:
//   requests
//   | where customDimensions["TenantId"] == "00000000-..."
//   | where success == false
//   | summarize count() by bin(timestamp, 1h)
// ─────────────────────────────────────────────────────────────────────────────
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System.Security.Claims;

namespace ERP.Api.Services;

public class TenantTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _http;

    public TenantTelemetryInitializer(IHttpContextAccessor http) => _http = http;

    public void Initialize(ITelemetry telemetry)
    {
        var ctx = _http.HttpContext;
        if (ctx == null) return;

        var tenantId = ctx.User.FindFirst("tenant_id")?.Value      ?? "anonymous";
        var userId   = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        var userName = ctx.User.FindFirst(ClaimTypes.Name)?.Value  ?? "anonymous";
        var corrId   = ctx.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? "";

        // Adiciona em todos os tipos de telemetria (requests, exceptions, dependencies, traces)
        telemetry.Context.GlobalProperties["TenantId"]      = tenantId;
        telemetry.Context.GlobalProperties["UserId"]        = userId;
        telemetry.Context.GlobalProperties["UserName"]      = userName;
        telemetry.Context.GlobalProperties["CorrelationId"] = corrId;

        // Em requests, adiciona também como Properties para facilitar filtros no portal
        if (telemetry is RequestTelemetry req)
        {
            req.Properties["TenantId"]      = tenantId;
            req.Properties["CorrelationId"] = corrId;
        }

        // Em exceptions, marca TenantId para alertas por loja
        if (telemetry is ExceptionTelemetry ex)
        {
            ex.Properties["TenantId"] = tenantId;
            ex.Properties["UserName"] = userName;
        }
    }
}
