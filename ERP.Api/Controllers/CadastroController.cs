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
    private readonly ERP.Domain.Interfaces.IUnitOfWork _uow;

    public CadastroController(ICadastroService cadastroService, ERP.Domain.Interfaces.IUnitOfWork uow)
    {
        _cadastroService = cadastroService;
        _uow             = uow;
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

        // S12 FIX: usa PasswordPolicy centralizado (antes: Length < 12 inline)
        var (senhaOk, senhaErro) = ERP.Application.Helpers.PasswordPolicy.Validar(dto.Senha);
        if (!senhaOk) return BadRequest(senhaErro);

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

    /// <summary>
    /// S12: Confirma cadastro quando o e-mail informado difere do e-mail RFB.
    /// Token foi enviado para o e-mail oficial da Receita Federal.
    /// Ativa o admin (IsActive=false → true) e limpa o token.
    /// </summary>
    [HttpGet("confirmar")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmarCadastro([FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Token de confirmação inválido.");

        var user = await _uow.Users.GetByConfirmacaoTokenAsync(token);

        if (user is null)
            return BadRequest("Token inválido ou já utilizado.");

        if (user.IsActive && user.ConfirmacaoToken is null)
            return Ok(new { mensagem = "Conta já está ativa. Faça login normalmente." });

        user.IsActive         = true;
        user.ConfirmacaoToken = null;
        user.UpdatedAt        = DateTime.UtcNow;
        await _uow.CommitAsync();

        Serilog.Log.Information("Onboarding: conta confirmada via token RFB para userId={UserId}", user.Id);

        return Ok(new { mensagem = "Conta confirmada com sucesso! Acesse o portal para fazer login." });
    }
}