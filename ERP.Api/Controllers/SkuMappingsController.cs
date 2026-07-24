// ── ERP.Api/Controllers/SkuMappingsController.cs ───────────────────────────
using ERP.Api.Security;
using ERP.Application.DTOs;
using ERP.Application.Interfaces;
using ERP.Domain.Entities;
using ERP.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

/// <summary>
/// Tela de SkuMapping — deixa o lojista ligar "produto do marketplace" a
/// "produto do ERP" sem SQL. Rotas aninhadas em /saleschannels/{id}/... porque
/// mapeamento só faz sentido no contexto de um canal já conectado.
/// </summary>
[ApiController]
[Route("api/saleschannels/{salesChannelId:guid}")]
public class SkuMappingsController : ControllerBase
{
    private readonly IUnitOfWork _uow;
    private readonly IEnumerable<IChannelDispatcher> _dispatchers;

    public SkuMappingsController(IUnitOfWork uow, IEnumerable<IChannelDispatcher> dispatchers)
    {
        _uow         = uow;
        _dispatchers = dispatchers;
    }

    /// <summary>
    /// Anúncios do canal, já cruzados com o que já está mapeado — pra tela
    /// mostrar de cara o que ainda precisa de atenção ("⚠ Não mapeado").
    /// </summary>
    [HttpGet("anuncios")]
    [HasPermission(Permissions.ConfigView)]
    public async Task<IActionResult> ListarAnuncios(Guid salesChannelId)
    {
        var canal = await _uow.OrderSync.GetCanalByIdAsync(salesChannelId);
        if (canal is null) return NotFound("Canal não encontrado.");

        var dispatcher = _dispatchers.FirstOrDefault(d => d.Tipo == canal.Tipo);
        if (dispatcher is null)
            return Ok(new { anuncios = Array.Empty<object>(), mensagem = $"Tipo {canal.Tipo} ainda não suporta listar anúncios." });

        var (sucesso, mensagem, anuncios) = await dispatcher.BuscarAnunciosAsync(canal);
        if (!sucesso) return StatusCode(502, new { mensagem });

        var mapeamentos = await _uow.OrderSync.GetMapeamentosPorCanalAsync(salesChannelId);
        var porSku = mapeamentos.ToDictionary(m => m.SkuExterno, m => m);

        var resultado = anuncios.Select(a =>
        {
            porSku.TryGetValue(a.SkuExterno, out var mapeamento);
            return new AnuncioComMapeamentoDto(
                a.ItemId, a.SkuExterno, a.Titulo,
                Mapeado: mapeamento is not null,
                ProductId: mapeamento?.ProductId,
                ProductNome: mapeamento?.Product?.Name);
        }).ToList();

        return Ok(resultado);
    }

    /// <summary>Mapeamentos já existentes pra esse canal.</summary>
    [HttpGet("mapeamentos")]
    [HasPermission(Permissions.ConfigView)]
    public async Task<IActionResult> ListarMapeamentos(Guid salesChannelId)
    {
        var mapeamentos = await _uow.OrderSync.GetMapeamentosPorCanalAsync(salesChannelId);
        var resultado = mapeamentos.Select(m => new SkuMappingDto(
            m.Id, m.SkuExterno, m.ProductId, m.Product?.Name ?? "(produto não encontrado)"));
        return Ok(resultado);
    }

    /// <summary>Cria um novo mapeamento SKU externo → Product.</summary>
    [HttpPost("mapeamentos")]
    [HasPermission(Permissions.ConfigView)]
    public async Task<IActionResult> Mapear(Guid salesChannelId, [FromBody] CriarSkuMappingDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SkuExterno))
            return BadRequest("SkuExterno é obrigatório.");

        var canal = await _uow.OrderSync.GetCanalByIdAsync(salesChannelId);
        if (canal is null) return NotFound("Canal não encontrado.");

        var jaExiste = await _uow.OrderSync.GetSkuMappingAsync(salesChannelId, dto.SkuExterno);
        if (jaExiste is not null)
            return Conflict($"O SKU '{dto.SkuExterno}' já está mapeado nesse canal.");

        var mapeamento = new SkuMapping
        {
            SalesChannelId  = salesChannelId,
            SkuExterno      = dto.SkuExterno,
            ProductId       = dto.ProductId,
            BufferSeguranca = dto.BufferSeguranca ?? 0,
        };

        mapeamento = await _uow.OrderSync.AdicionarMapeamentoAsync(mapeamento);
        return Ok(new SkuMappingDto(mapeamento.Id, mapeamento.SkuExterno, mapeamento.ProductId, ""));
    }
}
