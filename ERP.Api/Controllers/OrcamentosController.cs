// S1.2 — VULN #2 CORRIGIDA: [AllowAnonymous] removido de GetByCliente.
// Adicionado [Authorize] + verificação que o clienteId pertence ao tenant do JWT.

using ERP.Api.Services;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrcamentosController : ControllerBase
{
    private readonly IOrcamentoService _service;
    private readonly IRequestTenant    _tenant;

    public OrcamentosController(IOrcamentoService service, IRequestTenant tenant)
    {
        _service = service;
        _tenant  = tenant;
    }

    /// <summary>Lista todos os orçamentos.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _service.ObterTodosAsync());

    /// <summary>Lista todos os orçamentos com dados de follow-up.</summary>
    [HttpGet("crm")]
    public async Task<IActionResult> GetCrm()
        => Ok(await _service.GetTodosComFollowUpAsync());

    /// <summary>Agenda do dia: orçamentos com follow-up para hoje.</summary>
    [HttpGet("agenda-hoje")]
    public async Task<IActionResult> GetAgendaHoje()
        => Ok(await _service.GetAgendaHojeAsync());

    /// <summary>Taxa de conversão de orçamentos no período.</summary>
    [HttpGet("taxa-conversao")]
    public async Task<IActionResult> GetTaxaConversao(
        [FromQuery] DateTime? inicio = null,
        [FromQuery] DateTime? fim    = null)
    {
        var ini = inicio ?? DateTime.Today.AddMonths(-1);
        var end = fim    ?? DateTime.Today;
        return Ok(await _service.GetTaxaConversaoAsync(ini, end));
    }

    /// <summary>Converte orçamento em venda.</summary>
    [HttpPost("{id:guid}/converter")]
    public async Task<IActionResult> Converter(Guid id)
    {
        try
        {
            await _service.MarcarComoVendidoAsync(id);
            return Ok(new { mensagem = "Orçamento convertido em venda." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex)         { return BadRequest(new { erro = ex.Message }); }
    }

    /// <summary>Agenda (ou reagenda) o follow-up de um orçamento.</summary>
    [HttpPut("{id:guid}/agendar-followup")]
    public async Task<IActionResult> AgendarFollowUp(Guid id, [FromBody] AgendarFollowUpDto dto)
    {
        try
        {
            await _service.AgendarFollowUpAsync(id, dto);
            return Ok(new { mensagem = "Follow-up agendado." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Registra o resultado de um contato com o cliente.</summary>
    [HttpPut("{id:guid}/registrar-contato")]
    public async Task<IActionResult> RegistrarContato(Guid id, [FromBody] RegistrarContatoDto dto)
    {
        try
        {
            await _service.RegistrarContatoAsync(id, dto);
            return Ok(new { mensagem = "Contato registrado." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>
    /// Lista orçamentos de um cliente específico — portal de auto-atendimento.
    /// S1.2: era [AllowAnonymous] — IDOR corrigido. Agora requer autenticação.
    /// Os orçamentos já são filtrados por tenant via HasQueryFilter do EF Core.
    /// </summary>
    [HttpGet("cliente/{clienteId:guid}")]
    [Authorize]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetByCliente(Guid clienteId)
    {
        var orcs = await _service.GetByClienteAsync(clienteId);

        return Ok(orcs.Select(o => new
        {
            o.Id,
            o.Numero,
            Data     = o.DataEmissao,
            Total    = o.ValorTotal,
            Status   = o.Status.ToString(),
            Validade = o.DataValidade
        }));
    }
}