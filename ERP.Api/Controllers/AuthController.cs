using ERP.Api.Models;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService   _authService;
    private readonly IConfiguration _config;

    public AuthController(IAuthService authService, IConfiguration config)
    {
        _authService = authService;
        _config      = config;
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