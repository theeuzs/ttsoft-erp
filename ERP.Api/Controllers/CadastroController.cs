using ERP.Application.DTOs;
using ERP.Application.Exceptions;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

namespace ERP.Api.Controllers;

/// <summary>
/// Onboarding self-service — endpoint público (sem autenticação).
/// Rate limiting: 3 req/hora/IP via partitioner (S11).
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

        // S11 FIX: mensagem genérica idêntica para "CNPJ novo criado" e
        // "CNPJ já existe" — fecha o oráculo de enumeração via status code.
        const string mensagemGenerica =
            "Se o CNPJ informado for válido e ainda não estiver cadastrado, " +
            "você receberá em poucos minutos um e-mail com instruções de acesso.";

        try
        {
            var resultado = await _cadastroService.CadastrarTenantAsync(dto);
            return Ok(resultado);
        }
        catch (TenantJaExisteException)
        {
            // S11 FIX: engole — loga internamente, mas devolve a MESMA resposta
            // HTTP (200 + mensagem genérica) que o caso de sucesso real.
            // Sem isso, status 400 vs 200 era um oráculo para enumerar quais
            // CNPJs já são clientes TTSoft.
            Log.Information("Onboarding: tentativa de cadastro duplicado CNPJ={Cnpj}", dto.Cnpj);
            return Ok(new CadastroResponseDto
            {
                MensagemSucesso = mensagemGenerica,
                LoginUrl        = "https://app.ttsofts.com.br"
            });
        }
        catch (InvalidOperationException ex)
        {
            // Erros REAIS de validação (CNPJ malformado/dígitos inválidos) continuam 400.
            return BadRequest(ex.Message);
        }
    }
}