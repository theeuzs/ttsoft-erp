// ── ERP.Api/Middleware/BruteForceAlertMiddleware.cs ──────────────────────────
// S2.5 — Alerta automático de brute force no login.
//
// Detecta rajadas de 401 no endpoint /api/auth/login e loga como Warning
// com nível de severidade que o Serilog/App Insights pode alertar.
//
// Threshold: 5 falhas em 60 segundos pelo mesmo IP → Warning no log.
// Com App Insights ou Seq, configure um alerta neste Warning para receber
// notificação em Slack/Teams/e-mail automaticamente.
//
// Não bloqueia (o rate limiting do AspNetCoreRateLimit já cuida disso) —
// responsabilidade aqui é apenas observabilidade e auditoria.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Concurrent;

namespace ERP.Api.Middleware;

public class BruteForceAlertMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BruteForceAlertMiddleware> _logger;

    // Cache em memória: IP → (contagem de falhas, timestamp da primeira falha)
    // ConcurrentDictionary — thread-safe sem lock explícito
    private static readonly ConcurrentDictionary<string, (int Count, DateTime FirstAt)>
        _failureCache = new();

    private const int    ThresholdCount   = 5;
    private const int    WindowSeconds    = 60;
    private const string LoginPath        = "/api/auth/login";

    public BruteForceAlertMiddleware(RequestDelegate next,
        ILogger<BruteForceAlertMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Só analisa POSTs ao endpoint de login que retornaram 401
        if (!context.Request.Path.StartsWithSegments(LoginPath, StringComparison.OrdinalIgnoreCase)
            || context.Request.Method != HttpMethods.Post
            || context.Response.StatusCode != 401)
            return;

        var ip  = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var now = DateTime.UtcNow;

        _failureCache.AddOrUpdate(ip,
            _ => (1, now),
            (_, existing) =>
            {
                // Reseta a janela se já passou o período
                if ((now - existing.FirstAt).TotalSeconds > WindowSeconds)
                    return (1, now);
                return (existing.Count + 1, existing.FirstAt);
            });

        if (_failureCache.TryGetValue(ip, out var state) && state.Count >= ThresholdCount)
        {
            // Log estruturado — App Insights e Seq indexam cada propriedade
            _logger.LogWarning(
                "🚨 BRUTE FORCE DETECTADO: {Count} falhas de login em {WindowSeconds}s " +
                "a partir de {IpAddress}. Verificar bloqueio.",
                state.Count, WindowSeconds, ip);

            // Limpa o contador após alertar para não logar a cada tentativa
            _failureCache.TryRemove(ip, out _);
        }
    }
}
