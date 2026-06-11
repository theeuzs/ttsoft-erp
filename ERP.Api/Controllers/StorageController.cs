using ERP.Application.Interfaces;
using ERP.Infrastructure.Services;
using ERP.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StorageController : ControllerBase
{
    private readonly IStorageService  _storage;
    private readonly IRequestTenant   _tenant;
    private readonly AppDbContext     _ctx;

    private static readonly HashSet<string> _tiposPermitidos =
        ["image/jpeg", "image/png", "image/webp"];

    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    public StorageController(
        IStorageService storage,
        IRequestTenant  tenant,
        AppDbContext    ctx)
    {
        _storage = storage;
        _tenant  = tenant;
        _ctx     = ctx;
    }

    /// <summary>
    /// Faz upload da imagem de um produto do tenant autenticado.
    /// Valida ownership: retorna 404 se o produto não pertencer ao tenant.
    /// </summary>
    [HttpPost("produto/{produtoId:guid}/imagem")]
    public async Task<IActionResult> UploadImagemProduto(
        Guid produtoId, IFormFile arquivo, CancellationToken ct)
    {
        // HasQueryFilter + TenantQuery já filtram por tenant — null = não existe ou é de outro tenant
        if (!await _ctx.Products.AnyAsync(p => p.Id == produtoId, ct))
            return NotFound(new { erro = "Produto não encontrado." });

        var erro = ValidarArquivo(arquivo);
        if (erro != null) return BadRequest(new { erro });

        await using var stream = arquivo.OpenReadStream();
        var url = await _storage.UploadImagemProdutoAsync(
            _tenant.TenantId, produtoId, stream, arquivo.ContentType, ct);

        return Ok(new { url, mensagem = "Imagem enviada com sucesso." });
    }

    /// <summary>
    /// Remove a imagem de um produto do tenant autenticado.
    /// Valida ownership antes de deletar.
    /// </summary>
    [HttpDelete("produto/{produtoId:guid}/imagem")]
    public async Task<IActionResult> DeletarImagemProduto(Guid produtoId, CancellationToken ct)
    {
        if (!await _ctx.Products.AnyAsync(p => p.Id == produtoId, ct))
            return NotFound(new { erro = "Produto não encontrado." });

        await _storage.DeletarImagemProdutoAsync(_tenant.TenantId, produtoId, ct);
        return NoContent();
    }

    /// <summary>
    /// Faz upload de foto de comprovante de entrega do tenant autenticado.
    /// Valida ownership antes de aceitar o arquivo.
    /// </summary>
    [HttpPost("entrega/{entregaId:guid}/foto")]
    public async Task<IActionResult> UploadFotoEntrega(
        Guid entregaId, IFormFile arquivo, CancellationToken ct)
    {
        if (!await _ctx.Entregas.AnyAsync(e => e.Id == entregaId, ct))
            return NotFound(new { erro = "Entrega não encontrada." });

        var erro = ValidarArquivo(arquivo);
        if (erro != null) return BadRequest(new { erro });

        await using var stream = arquivo.OpenReadStream();
        var url = await _storage.UploadFotoEntregaAsync(
            _tenant.TenantId, entregaId, stream, arquivo.ContentType, ct);

        return Ok(new { url, mensagem = "Foto de entrega enviada com sucesso." });
    }

    /// <summary>Lista todas as fotos de uma entrega do tenant autenticado.</summary>
    [HttpGet("entrega/{entregaId:guid}/fotos")]
    public async Task<IActionResult> ListarFotosEntrega(Guid entregaId, CancellationToken ct)
    {
        if (!await _ctx.Entregas.AnyAsync(e => e.Id == entregaId, ct))
            return NotFound(new { erro = "Entrega não encontrada." });

        var urls = await _storage.ListarFotosEntregaAsync(_tenant.TenantId, entregaId, ct);
        return Ok(new { entregaId, fotos = urls });
    }

    private static string? ValidarArquivo(IFormFile arquivo)
    {
        if (arquivo == null || arquivo.Length == 0)
            return "Nenhum arquivo enviado.";

        if (!_tiposPermitidos.Contains(arquivo.ContentType.ToLower()))
            return $"Tipo não permitido: {arquivo.ContentType}. Use JPEG, PNG ou WebP.";

        if (arquivo.Length > MaxBytes)
            return $"Arquivo muito grande: {arquivo.Length / 1024 / 1024:F1}MB. Máximo: 5MB.";

        return null;
    }
}