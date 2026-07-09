using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConciliacaoController : ControllerBase
{
    private readonly IConciliacaoService _conciliacao;
    public ConciliacaoController(IConciliacaoService conciliacao) => _conciliacao = conciliacao;

    /// <summary>
    /// Recebe CSV do extrato da operadora de cartão e cruza com vendas do período.
    /// Suporta formato genérico com colunas: Data, Valor, Estabelecimento/Descrição.
    /// Formatos testados: Cielo, Rede, Stone, GetNet (CSV padrão).
    /// </summary>
    [HasPermission(Permissions.FinanceiroView)]
    [RequestSizeLimit(2_097_152)] // S9: 2 MB — evita DoS por upload gigante
    [HttpPost("importar-extrato")]
    public async Task<IActionResult> ImportarExtrato(
        [FromForm] IFormFile arquivo,
        [FromQuery] string? separador = null,
        CancellationToken ct = default)
    {
        if (arquivo is null || arquivo.Length == 0)
            return BadRequest(new { erro = "Arquivo CSV não enviado." });

        if (!arquivo.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { erro = "Apenas arquivos .csv são aceitos." });

        try
        {
            using var stream = arquivo.OpenReadStream();
            var resultado = await _conciliacao.ImportarExtratoAsync(stream, separador, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }
}