using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using ERP.Api.Security;
namespace ERP.Api.Controllers;

[ApiController]
[Route("api/contas-receber")]
[Authorize]
public class ContasReceberController : ControllerBase
{
    private readonly IContaReceberService _service;

    public ContasReceberController(IContaReceberService service) => _service = service;

    /// <summary>Lista contas a receber pendentes.</summary>
    [HttpGet("pendentes")]
    public async Task<IActionResult> GetPendentes()
        => Ok(await _service.GetPendentesAsync());

    /// <summary>Lista contas em atraso (vencidas e não pagas).</summary>
    [HttpGet("inadimplentes")]
    public async Task<IActionResult> GetInadimplentes()
        => Ok(await _service.GetInadimplentesAsync());

    /// <summary>Contas de um cliente específico.</summary>
    [HttpGet("cliente/{clienteId:guid}")]
    public async Task<IActionResult> GetPorCliente(Guid clienteId)
        => Ok(await _service.GetPorClienteAsync(clienteId));

    /// <summary>Resumo financeiro: total pendente, vencido, qtd. inadimplentes.</summary>
    [HttpGet("resumo")]
    public async Task<IActionResult> GetResumo()
    {
        var (totalPendente, totalVencido, qtdClientes) = await _service.GetResumoAsync();
        return Ok(new { totalPendente, totalVencido, qtdClientes });
    }

    /// <summary>
    /// Gera parcelas para uma venda a prazo.
    /// Cada parcela vira uma ContaReceber independente com vencimento escalonado.
    /// </summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPost("parcelar")]
    [ProducesResponseType(typeof(IEnumerable<ParcelaDto>), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Parcelar([FromBody] GerarParcelasDto dto)
    {
        if (dto.NumeroParcelas < 1 || dto.NumeroParcelas > 60)
            return BadRequest(new { erro = "Número de parcelas deve estar entre 1 e 60." });

        if (dto.ValorTotal <= 0)
            return BadRequest(new { erro = "Valor total deve ser maior que zero." });

        var parcelas = await _service.GerarParcelasAsync(dto);
        return StatusCode(201, parcelas);
    }

    /// <summary>Lista parcelas de um parcelamento.</summary>
    [HttpGet("parcelamento/{parcelamentoId:guid}")]
    public async Task<IActionResult> GetParcelas(Guid parcelamentoId)
        => Ok(await _service.GetParcelasByParcelamentoAsync(parcelamentoId));

    /// <summary>Dá baixa total em uma conta (paga por completo).</summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPost("{id:guid}/baixa-total")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DarBaixaTotal(Guid id)
    {
        await _service.DarBaixaTotalAsync(id);
        return NoContent();
    }

    /// <summary>Dá baixa parcial em uma conta.</summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPost("{id:guid}/baixa-parcial")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> DarBaixaParcial(
        Guid id, [FromBody] BaixaParcialDto dto)
    {
        if (dto.Valor <= 0)
            return BadRequest(new { erro = "Valor deve ser maior que zero." });

        await _service.DarBaixaParcialAsync(id, dto.Valor);
        return NoContent();
    }

    /// <summary>Cancela uma conta a prazo — diferente de baixa total, não conta
    /// como dinheiro recebido (útil quando o vendedor lançou errado, ou o
    /// cliente desistiu da compra a prazo).</summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarContaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo))
            return BadRequest(new { erro = "Motivo é obrigatório pra cancelar uma conta." });

        await _service.CancelarAsync(id, dto.Motivo);
        return NoContent();
    }

    /// <summary>Dá desconto numa conta específica.</summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPost("{id:guid}/desconto")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> DarDesconto(Guid id, [FromBody] DescontoContaDto dto)
    {
        if (dto.ValorDesconto <= 0)
            return BadRequest(new { erro = "Valor do desconto deve ser maior que zero." });

        await _service.DarDescontoAsync(id, dto.ValorDesconto, dto.Motivo);
        return NoContent();
    }

    /// <summary>
    /// Baixa várias contas de uma vez — pra quando o mesmo cliente tem contas
    /// abertas de vendedores diferentes e quer quitar tudo junto no caixa.
    /// </summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPost("baixa-em-lote")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> DarBaixaEmLote([FromBody] BaixaEmLoteDto dto)
    {
        if (dto.ContaIds is null || dto.ContaIds.Count == 0)
            return BadRequest(new { erro = "Selecione pelo menos uma conta." });
        if (dto.ValorAPagar <= 0)
            return BadRequest(new { erro = "Valor a pagar deve ser maior que zero." });

        await _service.DarBaixaEmLoteAsync(dto.ContaIds, dto.ValorAPagar, dto.ValorDesconto, dto.FormaPagamento);
        return NoContent();
    }

    /// <summary>
    /// Gera boleto bancário via Asaas para uma conta a receber.
    /// Requer Asaas:ApiKey configurado nas variáveis de ambiente.
    /// </summary>
    [HasPermission(Permissions.FinanceiroView)]
    [HttpPost("{id:guid}/gerar-boleto")]
    public async Task<IActionResult> GerarBoleto(Guid id)
    {
        var r = await _service.GerarBoletoAsync(id);

        return r.Status switch
        {
            GerarBoletoStatus.ContaNaoEncontrada          => NotFound(),
            GerarBoletoStatus.JaPossuiBoleto               => Ok(new { r.BoletoUrl, r.InvoiceUrl, r.BoletoBarCode, r.AsaasStatus }),
            GerarBoletoStatus.ClienteNaoVinculado          => BadRequest(new { erro = r.Erro }),
            GerarBoletoStatus.ClienteSemDocumento          => BadRequest(new { erro = r.Erro }),
            GerarBoletoStatus.FalhaAoRegistrarClienteAsaas => StatusCode(502, new { erro = r.Erro }),
            GerarBoletoStatus.FalhaAoGerarBoleto           => StatusCode(502, new { erro = r.Erro }),
            GerarBoletoStatus.AsaasIndisponivel            => StatusCode(503, new { erro = r.Erro }),
            _ => Ok(new { r.BoletoUrl, r.InvoiceUrl, r.BoletoBarCode, r.AsaasStatus })
        };
    }

}

public record BaixaParcialDto(decimal Valor);
public record CancelarContaDto(string Motivo);
public record DescontoContaDto(decimal ValorDesconto, string Motivo);
public record BaixaEmLoteDto(List<Guid> ContaIds, decimal ValorAPagar, decimal ValorDesconto, string FormaPagamento);