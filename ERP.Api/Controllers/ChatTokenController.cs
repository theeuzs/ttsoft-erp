// ── ERP.Api/Controllers/ChatTokenController.cs ───────────────────────────────
// S1.5 — VULN #5: Token de sessão descartável para ERPChatHub.
//
// Fluxo seguro:
//   1. WPF (autenticado) chama POST /api/auth/chat-token com Bearer JWT
//   2. API valida o JWT e emite um ChatToken de 5 minutos com os claims do usuário
//   3. WPF conecta no ERPChatHub passando ?chatToken=... na query string
//   4. Hub valida o ChatToken e extrai tenant/user do JWT — nunca da query string
//
// Por que não usar o Bearer JWT diretamente no hub?
//   Browsers e WPF não enviam Authorization header em WebSocket.
//   A solução padrão é passar um token curto na query string.
//   Usar o JWT de sessão (válido por horas) na query string é inseguro — fica em logs.
//   O ChatToken de 5 min minimiza a janela de exposição.
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Api.Services;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class ChatTokenController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IRequestTenant _tenant;

    public ChatTokenController(IConfiguration config, IRequestTenant tenant)
    {
        _config = config;
        _tenant = tenant;
    }

    /// <summary>
    /// Emite um ChatToken de curta duração (5 min) para conexão ao ERPChatHub.
    /// Requer o Bearer JWT de sessão no header Authorization.
    /// </summary>
    [HttpPost("chat-token")]
    [Authorize]
    public IActionResult GetChatToken()
    {
        var userName = User.FindFirst(ClaimTypes.Name)?.Value
                    ?? User.FindFirst(JwtRegisteredClaimNames.Name)?.Value
                    ?? "Terminal";
        var tenantId = _tenant.TenantId.ToString();
        var userId   = _tenant.UserId.ToString();

        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        // 5 minutos — janela mínima para o WPF iniciar a conexão WebSocket
        var expires = DateTime.UtcNow.AddMinutes(5);

        var claims = new[]
        {
            new Claim("tenant_id",                  tenantId),
            new Claim(JwtRegisteredClaimNames.Name, userName),
            new Claim(JwtRegisteredClaimNames.Sub,  userId),
            new Claim("chat_token",                 "true"),   // marca como token de chat
            new Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        return Ok(new
        {
            chatToken = new JwtSecurityTokenHandler().WriteToken(token),
            expiresIn = 300 // segundos
        });
    }
}
