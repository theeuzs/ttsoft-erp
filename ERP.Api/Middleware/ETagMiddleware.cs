// ── ERP.Api/Middleware/ETagMiddleware.cs ──────────────────────────────────────
// Gera e valida ETags para respostas GET, permitindo que clientes usem
// conditional requests (If-None-Match) e recebam 304 Not Modified quando
// o conteúdo não mudou — reduz tráfego e latência nas listagens grandes.
//
// Escopo: apenas GET com status 200 e Content-Type application/json.
// Excluído: SignalR hubs, health checks, Swagger, endpoints com dados sensíveis.
//
// Fluxo:
//   1. Request chega → middleware bufferiza o response body
//   2. Response é gerado normalmente pelo controller
//   3. Middleware calcula SHA256 do body → formata como ETag fraco: W/"abc123"
//   4. Se o request tinha If-None-Match igual ao ETag calculado → 304, body vazio
//   5. Caso contrário → envia response com header ETag incluso
//
// ETags fracos (W/"...") são usados porque o corpo pode variar em formatação
// JSON sem mudança semântica — mais tolerante e correto para APIs.
// ─────────────────────────────────────────────────────────────────────────────
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Api.Middleware;

public class ETagMiddleware
{
    private readonly RequestDelegate _next;

    // Prefixos excluídos do ETag — dados em tempo real, autenticação, uploads
    private static readonly string[] _prefixosExcluidos =
    [
        "/hubs/",
        "/health",
        "/swagger",
        "/api/auth",
        "/api/caixa",
        "/api/sales",       // vendas mudam a cada transação — cache inútil
        "/api/auditoria",   // logs sempre novos
    ];

    public ETagMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        // Aplica apenas em GET
        if (ctx.Request.Method != HttpMethods.Get)
        {
            await _next(ctx);
            return;
        }

        // Pula prefixos excluídos
        var path = ctx.Request.Path.Value ?? string.Empty;
        if (_prefixosExcluidos.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(ctx);
            return;
        }

        // Bufferiza o response para calcular o hash depois
        var originalBody = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;

        await _next(ctx);

        // Só processa respostas 200 JSON
        if (ctx.Response.StatusCode != 200 ||
            ctx.Response.ContentType?.Contains("application/json") != true)
        {
            buffer.Seek(0, SeekOrigin.Begin);
            await buffer.CopyToAsync(originalBody);
            ctx.Response.Body = originalBody;
            return;
        }

        // Calcula ETag fraco a partir do SHA256 do body
        buffer.Seek(0, SeekOrigin.Begin);
        var body    = await new StreamReader(buffer).ReadToEndAsync();
        var hash    = SHA256.HashData(Encoding.UTF8.GetBytes(body));
        var etag    = $"W/\"{Convert.ToHexString(hash)[..16]}\""; // 16 chars = suficiente

        // Valida If-None-Match — retorna 304 se o cliente já tem a versão atual
        var ifNoneMatch = ctx.Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == etag)
        {
            ctx.Response.StatusCode        = StatusCodes.Status304NotModified;
            ctx.Response.ContentLength     = 0;
            ctx.Response.Headers.ETag      = etag;
            ctx.Response.Body              = originalBody;
            return;
        }

        // Adiciona headers de cache
        ctx.Response.Headers.ETag          = etag;

        // Cache-Control por tipo de endpoint:
        // - /catalogo (público): 5 min no browser, 10 min no CDN
        // - demais GETs autenticados: no-store (dados do tenant não devem ficar em CDN)
        //   mas o ETag ainda permite revalidação eficiente
        if (path.Contains("/catalogo", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.Headers.CacheControl = "public, max-age=300, s-maxage=600";
        }
        else if (path.Contains("/calculadora/templates", StringComparison.OrdinalIgnoreCase))
        {
            // Templates são estáticos — cache longo no browser
            ctx.Response.Headers.CacheControl = "public, max-age=3600, immutable";
        }
        else
        {
            // Endpoints autenticados: permite revalidação com ETag, sem cache compartilhado
            ctx.Response.Headers.CacheControl = "private, no-cache";
        }

        // Escreve o body original no stream real
        ctx.Response.Body = originalBody;
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentLength = bytes.Length;
        await originalBody.WriteAsync(bytes);
    }
}
