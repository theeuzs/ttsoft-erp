using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/contas-receber")]
[Authorize]
public class ContasReceberController : ControllerBase
{
    private readonly IContaReceberService _service;
    private readonly AsaasService         _asaas;
    private readonly AppDbContext         _db;

    public ContasReceberController(
        IContaReceberService service,
        AsaasService asaas,
        AppDbContext db)
    {
        _service = service;
        _asaas   = asaas;
        _db      = db;
    }

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
    [HttpPost("{id:guid}/baixa-total")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DarBaixaTotal(Guid id)
    {
        await _service.DarBaixaTotalAsync(id);
        return NoContent();
    }

    /// <summary>Dá baixa parcial em uma conta.</summary>
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
    /// <summary>
    /// Gera boleto bancário via Asaas para uma conta a receber.
    /// Requer Asaas:ApiKey configurado nas variáveis de ambiente.
    /// </summary>
    [HttpPost("{id:guid}/gerar-boleto")]
    public async Task<IActionResult> GerarBoleto(Guid id)
    {
        var conta = await _db.ContasReceber
            .Include(c => c.Customer)
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync();

        if (conta is null) return NotFound();
        if (!string.IsNullOrEmpty(conta.AsaasPaymentId))
            return Ok(new { conta.BoletoUrl, conta.InvoiceUrl, conta.BoletoBarCode, conta.AsaasStatus });

        if (conta.Customer is null)
            return BadRequest(new { erro = "Conta sem cliente vinculado." });

        // 1. Obter/criar cliente no Asaas
        var cpfCnpj = conta.Customer.Document ?? "";
        if (string.IsNullOrEmpty(cpfCnpj))
            return BadRequest(new { erro = "Cliente sem CPF/CNPJ cadastrado. Preencha antes de gerar boleto." });

        var asaasClientId = await _asaas.ObterOuCriarClienteAsync(
            conta.Customer.Name, cpfCnpj, conta.Customer.Email, conta.Customer.Phone);

        if (asaasClientId is null)
            return StatusCode(502, new { erro = "Não foi possível registrar o cliente no Asaas. Verifique a API Key." });

        // 2. Gerar boleto
        var resultado = await _asaas.GerarBoletoAsync(
            asaasClientId,
            conta.ValorTotal - conta.ValorRecebido,
            conta.DataVencimento,
            $"{conta.Descricao} — Parcela {conta.NumeroParcela}/{conta.TotalParcelas}");

        if (resultado is null)
            return StatusCode(502, new { erro = "Erro ao gerar boleto no Asaas." });

        // 3. Salvar IDs na conta
        conta.AsaasPaymentId = resultado.AsaasPaymentId;
        conta.BoletoUrl      = resultado.BoletoUrl;
        conta.InvoiceUrl     = resultado.InvoiceUrl;
        conta.BoletoBarCode  = resultado.BoletoBarCode;
        conta.AsaasStatus    = resultado.Status;
        await _db.SaveChangesAsync();

        return Ok(new {
            resultado.BoletoUrl,
            resultado.InvoiceUrl,
            resultado.BoletoBarCode,
            resultado.Status
        });
    }

}

public record BaixaParcialDto(decimal Valor);