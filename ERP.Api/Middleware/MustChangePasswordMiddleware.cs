using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;

namespace ERP.Api.Middleware;

/// <summary>
/// Intercepta requisições de usuários com o claim "must_change_password" = "true"
/// e bloqueia tudo exceto o próprio endpoint de troca de senha.
///
/// Fluxo:
///   1. Usuário faz login com senha padrão (admin123)
///   2. JWT retorna com claim "must_change_password: true"
///   3. Qualquer request que não seja /api/auth/change-password recebe 403
///   4. Usuário troca a senha → novo login → JWT sem o claim → acesso liberado
/// </summary>
public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    // Caminhos liberados mesmo com must_change_password
    private static readonly string[] _caminhoPermitidos =
    [
        "/api/auth/login",
        "/api/auth/change-password",
    ];

    public MustChangePasswordMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var mustChange = context.User
                .FindFirst("must_change_password")?.Value == "true";

            if (mustChange)
            {
                var path = context.Request.Path.Value ?? "";
                var permitido = _caminhoPermitidos.Any(p =>
                    // S8 FIX: StartsWith puro aceita "/api/auth/login-history" como whitelist.
                    // Equals cobre o path exato; StartsWith(p + "/") cobre sub-rotas reais.
                    path.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));

                if (!permitido)
                {
                    context.Response.StatusCode  = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        mustChangePassword = true,
                        mensagem = "Você deve trocar sua senha antes de continuar. Use POST /api/auth/change-password."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}