using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly ISaleService         _saleService;
    private readonly IContaReceberService _contaService;
    private readonly IRequestTenant       _tenant;

    public SalesController(
        ISaleService         saleService,
        IContaReceberService contaService,
        IRequestTenant       tenant)
    {
        _saleService  = saleService;
        _contaService = contaService;
        _tenant       = tenant;
    }

    /// <summary>Lista vendas com filtro opcional por período e vendedor.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] DateTime? from     = null,
        [FromQuery] DateTime? to       = null,
        [FromQuery] string?   sellerId = null)
    {
        var inicio = from ?? DateTime.Today.AddDays(-30);
        var fim    = to   ?? DateTime.Today.AddDays(1);
        var sales  = await _saleService.GetAllAsync(inicio, fim, sellerId);
        return Ok(sales);
    }

    /// <summary>Retorna detalhe completo de uma venda (itens + pagamentos).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var sale = await _saleService.GetDetailAsync(id);
        return sale is null ? NotFound() : Ok(sale);
    }

    /// <summary>Retorna todas as parcelas de uma venda parcelada.</summary>
    [HttpGet("{id:guid}/parcelas")]
    public async Task<IActionResult> GetParcelas(Guid id)
    {
        var parcelas = await _contaService.GetParcelasByVendaAsync(id);
        return Ok(parcelas);
    }

    /// <summary>
    /// Cria uma nova venda a partir do PDV Web ou de qualquer cliente da API.
    /// O payload aceita múltiplos pagamentos (Payments) e itens com FatorConversao.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SaleDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Create([FromBody] CreateSaleDto dto)
    {
        if (dto.Items is null || dto.Items.Count == 0)
            return BadRequest(new { erro = "A venda deve conter ao menos um item." });

        // Garante que o vendedor e o usuário vêm do token quando não informados
        if (string.IsNullOrWhiteSpace(dto.SellerName))
            dto.SellerName = _tenant.UserName;

        if (dto.UsuarioId == Guid.Empty)
            dto.UsuarioId = _tenant.UserId;

        // Pagamento padrão: se veio sem Payments mas com FormaPagamento (legado PDV Web)
        if ((dto.Payments is null || dto.Payments.Count == 0) && dto.Items.Count > 0)
        {
            var total = dto.Items.Sum(i => i.TotalItem > 0 ? i.TotalItem : i.Quantity * i.UnitPrice)
                      - dto.DiscountAmount;
            dto.Payments = [new CreateSalePaymentDto
            {
                PaymentMethod = ERP.Domain.Enums.PaymentMethod.Dinheiro,
                Amount        = Math.Max(0, total)
            }];
        }

        try
        {
            var sale = await _saleService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = sale.Id }, sale);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    /// <summary>Cancela uma venda. Estorna estoque automaticamente.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelSaleRequestDto dto)
    {
        try
        {
            await _saleService.CancelAsync(id, dto.Motivo);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { erro = ex.Message }); }
    }

    /// <summary>Relatório de vendas por período com agrupamento por vendedor.</summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetReport(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string?  sellerName = null)
    {
        var report = await _saleService.GetSalesReportAsync(startDate, endDate, sellerName);
        return Ok(report);
    }
}

public record CancelSaleRequestDto(string Motivo = "Cancelamento via API");