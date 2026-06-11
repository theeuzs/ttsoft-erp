// ── ERP.Api/Middleware/TenantRateLimitMiddleware.cs ───────────────────────────
// F1.2 — Rate limiting por tenant_id (complementa o rate limiting por IP).
//
// O AspNetCoreRateLimit existente limita por IP — bom para ataques externos,
// insuficiente para multi-tenant: um tenant com 10 usuários de IPs diferentes
// teria 10x o limite de um tenant com 1 usuário.
//
// Este middleware adiciona uma segunda camada: limita por tenant_id (do JWT),
// garantindo que nenhum tenant monopolize a API em detrimento dos outros.
//
// Limites por janela de 1 minuto:
//   POST /api/auth/login  → 5 req/min/tenant  (brute force de senha por conta)
//   Qualquer outro        → 200 req/min/tenant (operação normal de uma loja)
//
// Posição no pipeline: APÓS TenantMiddleware (que popula IRequestTenant).
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Application.Interfaces;
using System.Collections.Concurrent;

namespace ERP.Api.Middleware;

public class TenantRateLimitMiddleware
{
    private readonly RequestDelegate                         _next;
    private readonly ILogger<TenantRateLimitMiddleware>     _logger;

    // Contadores em memória: chave = "tipo:tenantId:yyyyMMddHHmm"
    // ConcurrentDictionary garante thread-safety sem lock explícito.
    // Chaves são por minuto — crescem no máximo 2 × n_tenants por minuto.
    private static readonly ConcurrentDictionary<string, int> _counters = new();

    // Limpeza periódica para não vazar memória em runs longos
    private static DateTime _lastCleanup = DateTime.UtcNow;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private const int LoginLimitPerMinute   = 5;
    private const int GeneralLimitPerMinute = 200;

    public TenantRateLimitMiddleware(
        RequestDelegate next,
        ILogger<TenantRateLimitMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRequestTenant tenant)
    {
        var isLogin = context.Request.Method == HttpMethods.Post
                   && context.Request.Path.StartsWithSegments(
                          "/api/Auth/login", StringComparison.OrdinalIgnoreCase);

        // ── Resolve o TenantId para uso no rate limit ─────────────────────────
        // Para requests autenticados: vem do JWT via IRequestTenant.
        // Para login (pré-JWT): lê X-Tenant-CNPJ e deriva o TenantId via SHA-256
        // — mesma lógica do TenantHelper.FromCnpj. Sem isso, o rate limit era
        // contornado simplesmente não enviando JWT (bypass trivial via brute force).
        var rateLimitId = tenant.TenantId;

        if (rateLimitId == Guid.Empty && isLogin)
        {
            var cnpj = context.Request.Headers["X-Tenant-CNPJ"].FirstOrDefault();
            if (!string.IsNullOrEmpty(cnpj))
                rateLimitId = ERP.Api.Models.TenantHelper.FromCnpj(cnpj);
        }

        // Sem tenant identificável — deixa passar (será rejeitado mais adiante)
        if (rateLimitId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        var limit  = isLogin ? LoginLimitPerMinute : GeneralLimitPerMinute;
        var tipo   = isLogin ? "login" : "gen";
        var janela = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var key    = $"rl:{tipo}:{rateLimitId}:{janela}";

        // Incremento atômico — AddOrUpdate garante que não há lost update
        var count = _counters.AddOrUpdate(key, 1, (_, c) => c + 1);

        // Cabeçalhos informativos (padrão RateLimit-draft-7)
        context.Response.Headers["X-RateLimit-Limit"]     = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - count).ToString();
        context.Response.Headers["X-RateLimit-Window"]    = "60";

        if (count > limit)
        {
            _logger.LogWarning(
                "Tenant {TenantId} bloqueado por rate limit — {Path} " +
                "({Count}/{Limit} req/min). Usuário: {UserName}",
                rateLimitId, context.Request.Path, count, limit, tenant.UserName);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsync(
                $"Limite de {limit} requisições/minuto por loja atingido. " +
                $"Tente novamente em até 60 segundos.");
            return;
        }

        // Limpeza periódica de chaves expiradas (a cada 5 min)
        if (DateTime.UtcNow - _lastCleanup > CleanupInterval)
            CleanupOldKeys();

        await _next(context);
    }

    private static void CleanupOldKeys()
    {
        _lastCleanup = DateTime.UtcNow;
        var cutoff = DateTime.UtcNow.AddMinutes(-2).ToString("yyyyMMddHHmm");

        foreach (var key in _counters.Keys)
        {
            // Chave tem formato "rl:tipo:tenantId:yyyyMMddHHmm" — compara o sufixo de data
            var parts = key.Split(':');
            if (parts.Length >= 4 && string.Compare(parts[^1], cutoff) < 0)
                _counters.TryRemove(key, out _);
        }
    }
}