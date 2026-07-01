using ERP.Api.Models;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService   _authService;
    private readonly IConfiguration _config;
    private readonly IUnitOfWork    _uow;

    public AuthController(IAuthService authService, IConfiguration config, IUnitOfWork uow)
    {
        _authService = authService;
        _config      = config;
        _uow         = uow;
    }

    /// <summary>
    /// Autentica um usuário e retorna um JWT Bearer token.
    /// Inclua o CNPJ da empresa no header X-Tenant-CNPJ.
    /// Se MustChangePassword = true, redirecione para POST /api/auth/change-password.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login(
        [FromBody]   LoginDto dto,
        [FromHeader(Name = "X-Tenant-CNPJ")] string? cnpj)
    {
        if (string.IsNullOrWhiteSpace(cnpj))
            return BadRequest(new { erro = "Header X-Tenant-CNPJ é obrigatório." });

        var tenantId = TenantHelper.FromCnpj(cnpj);
        var result   = await _authService.LoginAsync(dto, tenantId);

        if (!result.Sucedeu || result.Usuario is null)
            return Unauthorized(new { erro = result.Mensagem });

        var token = GerarToken(result.Usuario, tenantId, result.MustChangePassword);

        return Ok(new TokenResponseDto
        {
            AccessToken        = token,
            ExpiresIn          = int.Parse(_config["Jwt:ExpirationHours"]!) * 3600,
            Usuario            = result.Usuario.Name,
            Cargo              = result.Usuario.RoleName,
            Permissoes         = result.Usuario.Permissions,
            // 1.7.4: portal/WPF deve verificar e redirecionar para troca de senha
            MustChangePassword = result.MustChangePassword
        });
    }

    /// <summary>
    /// Troca a senha do usuário autenticado (1.7.4 — MustChangePassword enforcement).
    /// Obrigatório quando JWT contém claim "must_change_password: true".
    /// Após a troca, faça login novamente para obter um token sem a restrição.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                       ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { erro = "Token inválido." });

        try
        {
            await _authService.ChangePasswordAsync(userId, dto.CurrentPassword, dto.NewPassword);
            return Ok(new { mensagem = "Senha alterada com sucesso. Faça login novamente para obter um novo token." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { erro = ex.Message });
        }
    }

    // ── Helper interno ────────────────────────────────────────────────────────

    // ── S11: Recuperação de senha ──────────────────────────────────────────────

    /// <summary>
    /// Solicita envio de link de recuperação de senha por e-mail.
    /// Resposta sempre genérica (anti-enumeração): não revela se e-mail existe.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("forgot-password-strict")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        const string respostaGenerica =
            "Se o e-mail informado estiver cadastrado, você receberá um link de recuperação em breve.";

        if (string.IsNullOrWhiteSpace(dto.Cnpj) || string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("CNPJ e e-mail são obrigatórios.");

        var tenantId = TenantHelper.FromCnpj(dto.Cnpj);
        if (tenantId == Guid.Empty)
            return Ok(new { mensagem = respostaGenerica });

        // Busca usuário pelo email — silencia erros para não vazar informação
        var user = await _uow.Users.GetByEmailAndTenantAsync(dto.Email.Trim().ToLower(), tenantId);
        if (user is null)
        {
            // Anti-enumeração: aguarda tempo similar ao de geração do token
            await Task.Delay(200);
            return Ok(new { mensagem = respostaGenerica });
        }

        // Gera JWT de reset (1 hora) com purpose dedicado
        var jwtKey = _config["Jwt:Key"]!;
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenJwt = new JwtSecurityToken(
            issuer:   _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims:   new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("tid",     tenantId.ToString()),
                new Claim("purpose", "password-reset"),
                // S12 FIX: iat explícito — sem ele, jwtToken.IssuedAt retorna
                // DateTime.MinValue, fazendo o check one-time-use sempre falhar
                new Claim(JwtRegisteredClaimNames.Iat,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64),
            },
            notBefore: DateTime.UtcNow,
            expires:   DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        var tokenStr = new JwtSecurityTokenHandler().WriteToken(tokenJwt);
        var link     = $"https://app.ttsofts.com.br/nova-senha?token={Uri.EscapeDataString(tokenStr)}";

        _ = EnviarEmailResetAsync(user.Email!, link);

        return Ok(new { mensagem = respostaGenerica });
    }

    /// <summary>
    /// Define nova senha usando o token recebido por e-mail.
    /// Token é de uso único — invalidado ao salvar UpdatedAt posterior ao iat do token.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("reset-password-strict")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Token) || string.IsNullOrWhiteSpace(dto.NovaSenha))
            return BadRequest("Token e nova senha são obrigatórios.");

        // S12 FIX: usa PasswordPolicy centralizado (antes: 8 chars, inconsistente com cadastro 12)
        var (senhaOk, senhaErro) = ERP.Application.Helpers.PasswordPolicy.Validar(dto.NovaSenha);
        if (!senhaOk) return BadRequest(senhaErro);

        // Valida o JWT de reset
        var jwtKey   = _config["Jwt:Key"]!;
        // S12 FIX: Clear() preserva os nomes originais das claims JWT (sub, tid, etc.).
        // Sem isso, JwtSecurityTokenHandler mapeia "sub" → ClaimTypes.NameIdentifier
        // (namespace SAML longo), e FindFirstValue("sub") retorna null → ArgumentNullException → 500.
        var handler  = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        JwtSecurityToken? jwtToken;
        ClaimsPrincipal principal;

        try
        {
            principal = handler.ValidateToken(dto.Token,
                new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer              = _config["Jwt:Issuer"],
                    ValidAudience            = _config["Jwt:Audience"],
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                    ClockSkew                = TimeSpan.Zero,
                }, out var validatedToken);

            jwtToken = validatedToken as JwtSecurityToken;
        }
        catch
        {
            return BadRequest("Token inválido ou expirado.");
        }

        // Verifica purpose
        var purpose = principal.FindFirstValue("purpose");
        if (purpose != "password-reset")
            return BadRequest("Token inválido.");

        var userId   = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var tenantId = Guid.Parse(principal.FindFirstValue("tid")!);
        var iat      = jwtToken!.IssuedAt;

        // Carrega usuário e verifica one-time-use
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user is null || user.TenantId != tenantId || !user.IsActive)
            return BadRequest("Token inválido.");

        // Se UpdatedAt > iat, a senha já foi alterada após este token — token já foi usado
        if (user.UpdatedAt.HasValue && user.UpdatedAt.Value > iat.AddSeconds(5))
            return BadRequest("Este link já foi utilizado. Solicite um novo link de recuperação.");

        // Atualiza senha — S12 FIX: passa tenantId explícito (do claim "tid" do JWT de reset)
        // Antes: UpdatePasswordAsync usava _context.GetTenantId() → Guid.Empty (anônimo) → 500
        var hash = BCrypt.Net.BCrypt.HashPassword(dto.NovaSenha, 12);
        await _uow.Users.UpdatePasswordAsync(userId, tenantId, hash, false);

        Log.Information("Recuperação de senha: senha redefinida para userId={UserId} tenantId={TenantId}",
            userId, tenantId);

        return Ok(new { mensagem = "Senha alterada com sucesso. Você já pode fazer login." });
    }

    private async Task EnviarEmailResetAsync(string emailDestino, string link)
    {
        try
        {
            var host  = _config["Email:SmtpHost"]  ?? "smtppro.zoho.com";
            var port  = int.Parse(_config["Email:SmtpPort"] ?? "587");
            var user  = _config["Email:Usuario"]   ?? "";
            var senha = _config["Email:Senha"]      ?? "";

            if (string.IsNullOrWhiteSpace(user)) return;

            var corpo = $@"
<html><body style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px'>
  <div style='background:#1a56db;padding:20px;border-radius:8px 8px 0 0;text-align:center'>
    <h2 style='color:white;margin:0'>TTSoft ERP — Recuperação de Senha</h2>
  </div>
  <div style='background:#f8fafc;padding:24px;border:1px solid #e2e8f0;border-radius:0 0 8px 8px'>
    <p>Recebemos uma solicitação para redefinir a senha da sua conta.</p>
    <p>Clique no botão abaixo para definir uma nova senha. O link é válido por <strong>1 hora</strong>.</p>
    <div style='text-align:center;margin:24px 0'>
      <a href='{link}' style='background:#1a56db;color:white;padding:12px 32px;
         border-radius:6px;text-decoration:none;font-weight:bold'>
        Redefinir senha →
      </a>
    </div>
    <p style='color:#64748b;font-size:12px'>
      Se você não solicitou a redefinição, ignore este e-mail. Sua senha permanece a mesma.
    </p>
  </div>
</body></html>";

            using var client = new SmtpClient(host, port) { EnableSsl = true,
                Credentials = new NetworkCredential(user, senha) };
            using var msg = new MailMessage(user, emailDestino,
                "🔑 Recuperação de senha — TTSoft ERP", corpo)
                { IsBodyHtml = true };
            await client.SendMailAsync(msg);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Falha ao enviar e-mail de recuperação para {Email}", emailDestino);
        }
    }

    private string GerarToken(UserDto user, Guid tenantId, bool mustChangePassword = false)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(double.Parse(_config["Jwt:ExpirationHours"]!));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,  user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.Name),
            new(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString()),
            new("tenant_id",                  tenantId.ToString()),
            new("role_name",                  user.RoleName),
            // S9: limite de desconto da role no token — lido pelo TenantMiddleware → IRequestTenant.
            // Evita lookup no DB por venda; Admin=100, Gerente=30, Supervisor=15, Vendedor=5.
            new("max_discount", user.MaxDiscountPercentage.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
        };

        foreach (var perm in user.Permissions)
            claims.Add(new Claim("permission", perm));

        // 1.7.4: claim que o MustChangePasswordMiddleware usa para bloquear todas as rotas
        // exceto /api/auth/change-password enquanto o usuário não trocar a senha padrão.
        if (mustChangePassword)
            claims.Add(new Claim("must_change_password", "true"));

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class TokenResponseDto
{
    public string       AccessToken        { get; set; } = string.Empty;
    public int          ExpiresIn          { get; set; }
    public string       Usuario            { get; set; } = string.Empty;
    public string       Cargo              { get; set; } = string.Empty;
    public List<string> Permissoes         { get; set; } = new();
    /// <summary>Se true, redirecionar para POST /api/auth/change-password antes de qualquer outra ação.</summary>
    public bool         MustChangePassword { get; set; } = false;
}