// ── ERP.Api/Controllers/CalculadoraController.cs ─────────────────────────────
// I.2: controller reduzido de 605 → ~40 linhas.
// Toda a lógica de templates e cálculos foi movida para ICalculadoraService.
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // Templates e Calcular são públicos; calcular-com-estoque e gerar-orcamento têm [Authorize] próprio
public class CalculadoraController : ControllerBase
{
    private readonly ICalculadoraService _calc;

    public CalculadoraController(ICalculadoraService calc) => _calc = calc;

    /// <summary>Lista todos os templates disponíveis com parâmetros de entrada.</summary>
    [HttpGet("templates")]
    public IActionResult GetTemplates()
        => Ok(_calc.GetTemplates());

    /// <summary>
    /// Calcula os materiais necessários — endpoint público.
    /// Usado pela CalculadoraPublica.razor e por clientes externos.
    /// </summary>
    [HttpPost("calcular")]
    public IActionResult Calcular([FromBody] CalcularRequest req)
    {
        try
        {
            var resultado = _calc.Calcular(req.Template, req.Parametros);
            return Ok(resultado);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { erro = ex.Message });
        }
    }

    /// <summary>Cruza os materiais calculados com o estoque real do tenant.</summary>
    [HttpPost("calcular-com-estoque")]
    [Authorize]
    public async Task<IActionResult> CalcularComEstoque([FromBody] CalcularRequest req)
    {
        try
        {
            var resultado = await _calc.CalcularComEstoqueAsync(req.Template, req.Parametros);
            return Ok(resultado);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { erro = ex.Message }); }
    }

    /// <summary>Gera um orçamento completo cruzando materiais com produtos do estoque.</summary>
    [HttpPost("gerar-orcamento")]
    [Authorize]
    public async Task<IActionResult> GerarOrcamento([FromBody] GerarOrcamentoRequest req)
    {
        try
        {
            var resultado = await _calc.GerarOrcamentoAsync(
                req.Template, req.Parametros, req.ClienteNome, req.ClienteId);
            return Ok(resultado);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { erro = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { erro = ex.Message }); }
    }
}

public class CalcularRequest
{
    public string                      Template   { get; set; } = string.Empty;
    public Dictionary<string, decimal> Parametros { get; set; } = new();
}

public class GerarOrcamentoRequest
{
    public string                      Template    { get; set; } = string.Empty;
    public Dictionary<string, decimal> Parametros  { get; set; } = new();
    public string?                     ClienteNome { get; set; }
    public Guid?                       ClienteId   { get; set; }
}