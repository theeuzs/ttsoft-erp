// ── ERP.Api/Middleware/TenantMiddleware.cs ────────────────────────────────────
// S2.1 — Serilog Enrichment: TenantId e UserId injetados no LogContext.
// Cada log gerado após este middleware carrega automaticamente TenantId e UserId
// como propriedades estruturadas — filtrável em Seq/Grafana/App Insights em 1 clique.
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Application.Interfaces;
using Serilog.Context;
using System.Security.Claims;

namespace ERP.Api.Middleware;

/// <summary>
/// Resolve o TenantId por requisição via IRequestTenant (Scoped).
/// S2.1: enriquece o LogContext do Serilog com TenantId, UserId e UserName
/// para que todos os logs da requisição carreguem essas propriedades automaticamente.
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IRequestTenant requestTenant)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            if (Guid.TryParse(tenantClaim, out var tenantId))
                requestTenant.TenantId = tenantId;

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? context.User.FindFirst("sub")?.Value;
            if (Guid.TryParse(userIdClaim, out var userId))
                requestTenant.UserId = userId;

            requestTenant.UserName = context.User.FindFirst(ClaimTypes.Name)?.Value
                                  ?? context.User.FindFirst("name")?.Value
                                  ?? "API";
        }

        // S2.1: empurra TenantId, UserId e UserName para o LogContext do Serilog.
        // LogContext.PushProperty retorna um IDisposable — o using garante que as
        // propriedades são removidas ao fim da requisição, sem vazar entre threads.
        using var tenantProp = LogContext.PushProperty("TenantId",  requestTenant.TenantId);
        using var userProp   = LogContext.PushProperty("UserId",    requestTenant.UserId);
        using var nameProp   = LogContext.PushProperty("UserName",  requestTenant.UserName);
        using var pathProp   = LogContext.PushProperty("RequestPath", context.Request.Path.Value);

        await _next(context);
    }
}