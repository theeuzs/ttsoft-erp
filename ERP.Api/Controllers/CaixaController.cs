using ERP.Api.Security;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CaixaController : ControllerBase
{
    private readonly ICaixaService  _service;
    private readonly IRequestTenant _tenant;

    public CaixaController(ICaixaService service, IRequestTenant tenant)
    {
        _service = service;
        _tenant  = tenant;
    }

    private Guid UsuarioId => Guid.Parse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? Guid.Empty.ToString());

    /// <summary>Retorna o caixa aberto do usuário autenticado.</summary>
    [HasPermission(Permissions.CashViewSummary)]
    [HttpGet("aberto")]
    public async Task<IActionResult> GetAberto()
    {
        var caixa = await _service.ObterCaixaAbertoAsync(UsuarioId);
        return caixa is null ? NotFound(new { mensagem = "Nenhum caixa aberto." }) : Ok(caixa);
    }

    /// <summary>Abre um novo caixa para o usuário autenticado.</summary>
    [HttpPost("abrir")]
    public async Task<IActionResult> Abrir([FromBody] AbrirCaixaRequestDto dto)
    {
        // S8 FIX: UsuarioId e OperadorNome do JWT — não confiar no body.
        // Antes: dto.UsuarioId do body permitia abrir caixa em nome de outro usuário (DoS + audit trail falso).
        var operadorNome = User.FindFirst(ClaimTypes.Name)?.Value
                        ?? User.FindFirst("name")?.Value
                        ?? "Operador";
        try
        {
            await _service.AbrirCaixaAsync(new AbrirCaixaDto
            {
                UsuarioId     = UsuarioId,    // ← JWT
                OperadorNome  = operadorNome, // ← JWT
                ValorAbertura = dto.ValorAbertura
            });
            return Ok(new { mensagem = "Caixa aberto com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Fecha o caixa do usuário autenticado.</summary>
    [HttpPost("fechar")]
    public async Task<IActionResult> Fechar()
    {
        try
        {
            await _service.FecharCaixaAsync(UsuarioId);
            return Ok(new { mensagem = "Caixa fechado com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Registra sangria no caixa.</summary>
    [HasPermission(Permissions.CashSangria)]
    [HttpPost("sangria")]
    public async Task<IActionResult> Sangria([FromBody] MovimentoCaixaRequest dto)
    {
        try
        {
            // S13: passa MaxSangriaValue do cargo (IRequestTenant) para SangriaPolicy
            await _service.RegistrarMovimentoAsync(
                UsuarioId, dto.Valor, dto.Descricao,
                PaymentMethod.Dinheiro, TipoMovimentoCaixa.Sangria,
                maxSangriaValue: _tenant.MaxSangriaValue);
            return Ok(new { mensagem = "Sangria registrada." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Registra suprimento no caixa.</summary>
    [HasPermission(Permissions.CashSangria)]
    [HttpPost("suprimento")]
    public async Task<IActionResult> Suprimento([FromBody] MovimentoCaixaRequest dto)
    {
        try
        {
            await _service.RegistrarMovimentoAsync(
                UsuarioId, dto.Valor, dto.Descricao,
                PaymentMethod.Dinheiro, TipoMovimentoCaixa.Suprimento);
            return Ok(new { mensagem = "Suprimento registrado." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Registra movimento genérico no caixa.</summary>
    [HttpPost("movimento")]
    public async Task<IActionResult> Movimento([FromBody] MovimentoCaixaRequest dto)
    {
        if (!Enum.TryParse<TipoMovimentoCaixa>(dto.Tipo, out var tipo))
            return BadRequest(new { erro = $"Tipo inválido: {dto.Tipo}" });

        if (!Enum.TryParse<PaymentMethod>(dto.FormaPagamento ?? "Dinheiro", out var forma))
            forma = PaymentMethod.Dinheiro;

        // S8 FIX: tipos restritos exigem a mesma permissão que os endpoints dedicados.
        // Antes: POST /movimento?Tipo=Sangria bypass total de [HasPermission(CashSangria)] em /sangria.
        var tipoRestrito = tipo is TipoMovimentoCaixa.Sangria or TipoMovimentoCaixa.Suprimento;
        if (tipoRestrito && !User.HasClaim("permission", Permissions.CashSangria))
            return Forbid();

        try
        {
            await _service.RegistrarMovimentoAsync(UsuarioId, dto.Valor, dto.Descricao, forma, tipo);
            return Ok(new { mensagem = "Movimento registrado." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }
}

public record MovimentoCaixaRequest(
    decimal Valor,
    string  Descricao,
    string? Tipo          = "Sangria",
    string? FormaPagamento = "Dinheiro");