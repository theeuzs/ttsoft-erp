// ── ERP.Api/Middleware/CorrelationIdMiddleware.cs ────────────────────────────
// S2.3 — Correlation ID: rastreio ponta a ponta de qualquer requisição.
//
// Fluxo:
//   1. Se o cliente enviar X-Correlation-Id no request → usa o valor dele
//   2. Se não → gera um novo GUID
//   3. Injeta o ID no LogContext do Serilog → aparece em TODOS os logs da req
//   4. Retorna o ID no response header X-Correlation-Id
//   5. O exception handler já usa ctx.TraceIdentifier — mas o X-Correlation-Id
//      é mais legível e rastreável por ferramentas externas (Postman, logs do cliente)
//
// No Portal Blazor: interceptar o header X-Correlation-Id do response e exibir
// ao operador quando o status for 500 ("Código de rastreamento: abc-123").
// ─────────────────────────────────────────────────────────────────────────────
using Serilog.Context;

namespace ERP.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Usa o ID enviado pelo cliente ou gera um novo
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                         ?? Guid.NewGuid().ToString("N")[..12]; // 12 chars — legível, único

        // Adiciona ao response para que o cliente/suporte possa referenciar
        context.Response.Headers[HeaderName] = correlationId;

        // Injeta no LogContext — aparece em todos os logs desta requisição
        using var prop = LogContext.PushProperty("CorrelationId", correlationId);

        // Sobrescreve o TraceIdentifier do ASP.NET para consistência
        context.TraceIdentifier = correlationId;

        await _next(context);
    }
}
