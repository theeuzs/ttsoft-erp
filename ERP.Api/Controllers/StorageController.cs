// ── ERP.Api/Controllers/StorageController.cs ─────────────────────────────────
// 2.2 — Endpoints de upload de imagem de produto e foto de entrega.
// Recebe multipart/form-data, valida tipo e tamanho, delega ao IStorageService.
// ─────────────────────────────────────────────────────────────────────────────
using ERP.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StorageController : ControllerBase
{
    private readonly IStorageService _storage;

    private static readonly HashSet<string> _tiposPermitidos =
        ["image/jpeg", "image/png", "image/webp"];

    private const long MaxBytes = 5 * 1024 * 1024; // 5MB

    public StorageController(IStorageService storage) => _storage = storage;

    /// <summary>
    /// Faz upload da imagem de um produto.
    /// Substitui a imagem anterior automaticamente (mesmo ID = mesmo blob).
    /// </summary>
    [HttpPost("produto/{produtoId:guid}/imagem")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UploadImagemProduto(
        Guid produtoId, IFormFile arquivo, CancellationToken ct)
    {
        var erro = ValidarArquivo(arquivo);
        if (erro != null) return BadRequest(new { erro });

        await using var stream = arquivo.OpenReadStream();
        var url = await _storage.UploadImagemProdutoAsync(
            produtoId, stream, arquivo.ContentType, ct);

        return Ok(new { url, mensagem = "Imagem enviada com sucesso." });
    }

    /// <summary>
    /// Remove a imagem de um produto.
    /// </summary>
    [HttpDelete("produto/{produtoId:guid}/imagem")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeletarImagemProduto(Guid produtoId, CancellationToken ct)
    {
        await _storage.DeletarImagemProdutoAsync(produtoId, ct);
        return NoContent();
    }

    /// <summary>
    /// Faz upload de foto de comprovante de entrega.
    /// Permite múltiplas fotos por entrega.
    /// </summary>
    [HttpPost("entrega/{entregaId:guid}/foto")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UploadFotoEntrega(
        Guid entregaId, IFormFile arquivo, CancellationToken ct)
    {
        var erro = ValidarArquivo(arquivo);
        if (erro != null) return BadRequest(new { erro });

        await using var stream = arquivo.OpenReadStream();
        var url = await _storage.UploadFotoEntregaAsync(
            entregaId, stream, arquivo.ContentType, ct);

        return Ok(new { url, mensagem = "Foto de entrega enviada com sucesso." });
    }

    /// <summary>Lista todas as fotos de uma entrega.</summary>
    [HttpGet("entrega/{entregaId:guid}/fotos")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ListarFotosEntrega(Guid entregaId, CancellationToken ct)
    {
        var urls = await _storage.ListarFotosEntregaAsync(entregaId, ct);
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
