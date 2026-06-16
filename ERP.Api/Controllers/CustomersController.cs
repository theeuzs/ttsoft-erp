// S1.1 — VULN #1 CORRIGIDA: [AllowAnonymous] removido de GetPublico.
// O endpoint agora requer autenticação.
// Retorna APENAS Id, Name, City — sem Document, Phone, Email (PII).
// O Portal do Cliente (/cliente) deve ser movido para um fluxo autenticado
// ou usar um token OTP enviado por SMS/WhatsApp para identificação segura.

using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;

    public CustomersController(ICustomerService service) => _service = service;

    /// <summary>Lista clientes com paginação e busca por nome/CPF/CNPJ.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CustomerDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page     = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? search = null)
    {
        var result = await _service.GetPagedAsync(page, pageSize, search);
        return Ok(result);
    }

    /// <summary>Busca cliente por ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var customer = await _service.GetByIdAsync(id);
        return customer is null ? NotFound() : Ok(customer);
    }

    /// <summary>
    /// Busca cliente por CPF/CNPJ — portal de auto-atendimento.
    /// REQUER AUTENTICAÇÃO: retorna apenas Id, Name e City (sem PII sensível).
    /// Para acesso público futuro, implementar OTP via SMS/WhatsApp.
    /// </summary>
    [HttpGet("publico")]
    [Authorize]   // S1.1: era [AllowAnonymous] — violação LGPD corrigida
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetPublico([FromQuery] string document)
    {
        if (string.IsNullOrWhiteSpace(document) || document.Length < 11)
            return BadRequest(new { erro = "Informe um CPF (11 dígitos) ou CNPJ (14 dígitos)." });

        var docNorm  = new string(document.Where(char.IsDigit).ToArray());
        var clientes = await _service.SearchAsync(docNorm);

        // Retorna APENAS dados não-sensíveis — sem Document, Phone, Email
        var resultado = clientes
            .Where(c => !string.IsNullOrEmpty(c.Document) &&
                        new string(c.Document.Where(char.IsDigit).ToArray()) == docNorm)
            .Select(c => new { c.Id, c.Name, c.City })
            .ToList();

        return Ok(resultado);
    }

    /// <summary>Cria novo cliente.</summary>
    [HasPermission(Permissions.CustomerEdit)]
    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
    {
        try
        {
            var customer = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { erros = ex.Errors.Select(e => e.ErrorMessage) });
        }
    }

    /// <summary>Atualiza dados do cliente.</summary>
    [HasPermission(Permissions.CustomerEdit)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateCustomerDto dto)
    {
        try
        {
            var customer = await _service.UpdateAsync(id, dto);
            return Ok(customer);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>Remove cliente.</summary>
    [HasPermission(Permissions.CustomerDelete)]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try { await _service.DeleteAsync(id); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}