// I.1 — [AllowAnonymous] removido de GetSaldo e GetHistorico.
using ERP.Api.Services;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HaverController : ControllerBase
{
    private readonly IHaverService  _haver;
    private readonly IRequestTenant _tenant;

    public HaverController(IHaverService haver, IRequestTenant tenant)
    {
        _haver  = haver;
        _tenant = tenant;
    }

    [HttpGet("saldo/{clienteId:guid}")]
    // I.1: era [AllowAnonymous] — qualquer um com o GUID acessava o saldo haver
    public async Task<IActionResult> GetSaldo(Guid clienteId)
    {
        var saldo = await _haver.ObterSaldoAsync(clienteId);
        return Ok(new { ClienteId = clienteId, Saldo = saldo });
    }

    [HttpGet("historico/{clienteId:guid}")]
    // I.1: era [AllowAnonymous]
    public async Task<IActionResult> GetHistorico(Guid clienteId)
    {
        var historico = await _haver.ObterHistoricoAsync(clienteId);
        return Ok(historico);
    }

    [HttpPost("credito")]
    public async Task<IActionResult> LancarCredito([FromBody] LancarHaverDto dto)
    {
        await _haver.LancarAsync(
            dto.ClienteId, dto.Valor, "Entrada",
            dto.Descricao ?? "Crédito lançado manualmente",
            _tenant.UserName);
        return Ok(new { mensagem = "Crédito lançado com sucesso." });
    }

    [HttpPost("debito")]
    public async Task<IActionResult> LancarDebito([FromBody] LancarHaverDto dto)
    {
        try
        {
            await _haver.LancarAsync(
                dto.ClienteId, dto.Valor, "Saida",
                dto.Descricao ?? "Uso de haver",
                _tenant.UserName);
            return Ok(new { mensagem = "Débito lançado com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }
}

public class LancarHaverDto
{
    public Guid    ClienteId { get; set; }
    public decimal Valor     { get; set; }
    public string? Descricao { get; set; }
    public Guid?   VendaId   { get; set; }
}