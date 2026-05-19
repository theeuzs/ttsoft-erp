// ── ERP.Api/Middleware/MetricsMiddleware.cs ───────────────────────────────────
// S2.7 — Coleta latência e status de cada request para o MetricsCollector.
// ─────────────────────────────────────────────────────────────────────────────
using System.Diagnostics;

namespace ERP.Api.Middleware;

public class MetricsMiddleware
{
    private readonly RequestDelegate              _next;
    private readonly ERP.Api.Services.MetricsCollector _collector;

    public MetricsMiddleware(RequestDelegate next, ERP.Api.Services.MetricsCollector collector)
    {
        _next      = next;
        _collector = collector;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            // Ignora endpoints de infraestrutura para não distorcer as métricas
            var path = context.Request.Path.Value ?? "/";
            if (!path.StartsWith("/health") && !path.StartsWith("/swagger"))
                _collector.Record(path, context.Response.StatusCode, sw.Elapsed.TotalMilliseconds);
        }
    }
}
