using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/sugestao-compras")]
[Authorize]
public class SugestaoComprasController : ControllerBase
{
    private readonly ISugestaoComprasService _service;
    private readonly IRequestTenant          _tenant;

    public SugestaoComprasController(ISugestaoComprasService service, IRequestTenant tenant)
    {
        _service = service;
        _tenant  = tenant;
    }

    /// <summary>
    /// Lista sugestões de compra ordenadas por urgência.
    /// Inclui: dias para ruptura, giro diário, quantidade sugerida e custo total.
    /// Urgência: Critico (≤3d), Alerta (≤7d), Normal (≤45d).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSugestoes()
        => Ok(await _service.GetSugestoesAsync());

    /// <summary>
    /// Gera pedidos de compra automaticamente com os itens selecionados.
    /// Agrupa por fornecedor — cada fornecedor gera um pedido separado.
    /// Retorna os IDs dos pedidos criados.
    /// </summary>
    [HasPermission(Permissions.ComprasView)]
    [HttpPost("gerar-pedido")]
    public async Task<IActionResult> GerarPedido([FromBody] GerarPedidoCompraDto dto)
    {
        if (!dto.Itens.Any())
            return BadRequest(new { erro = "Selecione ao menos um produto." });

        dto.CriadoPor = _tenant.UserName;

        try
        {
            var ids = await _service.GerarPedidosCompraAsync(dto);
            return Ok(new
            {
                mensagem       = $"{ids.Count()} pedido(s) de compra gerado(s) com sucesso.",
                pedidosGerados = ids
            });
        }
        catch (ArgumentException ex)  { return BadRequest(new { erro = ex.Message }); }
        catch (Exception ex)          { return StatusCode(500, new { erro = ex.Message }); }
    }
}
