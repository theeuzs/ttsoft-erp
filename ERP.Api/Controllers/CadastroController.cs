using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>
/// Onboarding self-service — endpoint público (sem autenticação).
/// Rate limiting aplicado via TenantRateLimitMiddleware (3 req/hora por IP).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class CadastroController : ControllerBase
{
    private readonly ICadastroService _cadastroService;

    public CadastroController(ICadastroService cadastroService)
    {
        _cadastroService = cadastroService;
    }

    /// <summary>
    /// Cria um novo tenant no sistema.
    /// Deriva TenantId via SHA-256(CNPJ) — compatível com o WPF.
    /// Cria roles padrão, usuário admin e envia e-mail de boas-vindas.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CadastroResponseDto), 200)]
    [ProducesResponseType(typeof(string), 400)]
    public async Task<IActionResult> Cadastrar([FromBody] CadastroRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Cnpj))
            return BadRequest("CNPJ é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.RazaoSocial))
            return BadRequest("Razão Social é obrigatória.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("E-mail é obrigatório.");

        if (string.IsNullOrWhiteSpace(dto.Senha) || dto.Senha.Length < 6)
            return BadRequest("Senha deve ter no mínimo 6 caracteres.");

        try
        {
            var resultado = await _cadastroService.CadastrarTenantAsync(dto);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
