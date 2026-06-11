using ERP.Api.Models;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
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

        // TenantId resolvido localmente — NÃO usa o método estático AppDbContext.SetGlobalTenantId().
        // O método estático era compartilhado entre threads e causava race condition / vazamento
        // de dados entre tenants em alta concorrência (violação de LGPD).
        // Para requisições autenticadas, o TenantMiddleware resolve IRequestTenant via claim JWT.
        var tenantId = TenantHelper.FromCnpj(cnpj);

        var result = await _authService.LoginAsync(dto, tenantId);

        if (!result.Sucedeu || result.Usuario is null)
            return Unauthorized(new { erro = result.Mensagem });

        var token = GerarToken(result.Usuario, tenantId);

        return Ok(new TokenResponseDto
        {
            AccessToken = token,
            ExpiresIn   = int.Parse(_config["Jwt:ExpirationHours"]!) * 3600,
            Usuario     = result.Usuario.Name,
            Cargo       = result.Usuario.RoleName,
            Permissoes  = result.Usuario.Permissions
        });
    }

    private string GerarToken(UserDto user, Guid tenantId)
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
    public string       AccessToken { get; set; } = string.Empty;
    public int          ExpiresIn   { get; set; }
    public string       Usuario     { get; set; } = string.Empty;
    public string       Cargo       { get; set; } = string.Empty;
    public List<string> Permissoes  { get; set; } = new();
}