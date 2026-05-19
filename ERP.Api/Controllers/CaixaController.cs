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
    private readonly ICaixaService _service;

    public CaixaController(ICaixaService service) => _service = service;

    private Guid UsuarioId => Guid.Parse(
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value
        ?? Guid.Empty.ToString());

    /// <summary>Retorna o caixa aberto do usuário autenticado.</summary>
    [HttpGet("aberto")]
    public async Task<IActionResult> GetAberto()
    {
        var caixa = await _service.ObterCaixaAbertoAsync(UsuarioId);
        return caixa is null ? NotFound(new { mensagem = "Nenhum caixa aberto." }) : Ok(caixa);
    }

    /// <summary>Abre um novo caixa.</summary>
    [HttpPost("abrir")]
    public async Task<IActionResult> Abrir([FromBody] AbrirCaixaDto dto)
    {
        try
        {
            await _service.AbrirCaixaAsync(dto);
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
    [HttpPost("sangria")]
    public async Task<IActionResult> Sangria([FromBody] MovimentoCaixaRequest dto)
    {
        await _service.RegistrarMovimentoAsync(
            UsuarioId, dto.Valor, dto.Descricao,
            PaymentMethod.Dinheiro, TipoMovimentoCaixa.Sangria);
        return Ok(new { mensagem = "Sangria registrada." });
    }

    /// <summary>Registra suprimento no caixa.</summary>
    [HttpPost("suprimento")]
    public async Task<IActionResult> Suprimento([FromBody] MovimentoCaixaRequest dto)
    {
        await _service.RegistrarMovimentoAsync(
            UsuarioId, dto.Valor, dto.Descricao,
            PaymentMethod.Dinheiro, TipoMovimentoCaixa.Suprimento);
        return Ok(new { mensagem = "Suprimento registrado." });
    }

    /// <summary>Registra movimento genérico no caixa.</summary>
    [HttpPost("movimento")]
    public async Task<IActionResult> Movimento([FromBody] MovimentoCaixaRequest dto)
    {
        if (!Enum.TryParse<TipoMovimentoCaixa>(dto.Tipo, out var tipo))
            return BadRequest(new { erro = $"Tipo inválido: {dto.Tipo}" });

        if (!Enum.TryParse<PaymentMethod>(dto.FormaPagamento ?? "Dinheiro", out var forma))
            forma = PaymentMethod.Dinheiro;

        await _service.RegistrarMovimentoAsync(UsuarioId, dto.Valor, dto.Descricao, forma, tipo);
        return Ok(new { mensagem = "Movimento registrado." });
    }
}

public record MovimentoCaixaRequest(
    decimal Valor,
    string  Descricao,
    string? Tipo          = "Sangria",
    string? FormaPagamento = "Dinheiro");
