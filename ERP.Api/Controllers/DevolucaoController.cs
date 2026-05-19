using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevolucaoController : ControllerBase
{
    private readonly IDevolucaoService _service;
    public DevolucaoController(IDevolucaoService service) => _service = service;

    /// <summary>
    /// Processa devolução parcial ou total de itens de uma venda.
    /// Estorna estoque e gera crédito no Haver do cliente.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DevolucaoResultDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Devolver([FromBody] CreateDevolucaoDto dto)
    {
        try
        {
            var result = await _service.DevolverItensAsync(dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Retorna quantidade já devolvida de um produto em uma venda.</summary>
    [HttpGet("quantidade/{saleId:guid}/{productId:guid}")]
    public async Task<IActionResult> GetQuantidadeDevolvida(Guid saleId, Guid productId)
    {
        var qtd = await _service.GetQuantidadeJaDevolvida(saleId, productId);
        return Ok(new { saleId, productId, quantidadeDevolvida = qtd });
    }
}
