using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ERP.Api.Controllers;

/// <summary>
/// Onboarding self-service — endpoint público (sem autenticação).
/// Rate limiting: 3 req/hora/IP via sliding window (S10).
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

    [EnableRateLimiting("cadastro-strict")]
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

        // S10 FIX: senha mínima de 12 chars (OWASP 2026) + pelo menos 1 número
        if (string.IsNullOrWhiteSpace(dto.Senha) || dto.Senha.Length < 12)
            return BadRequest("Senha deve ter no mínimo 12 caracteres.");

        if (!dto.Senha.Any(char.IsDigit))
            return BadRequest("Senha deve conter pelo menos um número.");

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