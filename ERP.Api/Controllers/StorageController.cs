using ERP.Api.Security;
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

    // Pre-check de Content-Type para UX rápido (devolve erro antes de ler o body).
    // NÃO é verificação de segurança — a validação real de formato é feita pelo
    // StorageService via magic bytes (SixLabors.ImageSharp, 1.9).
    private static readonly HashSet<string> _tiposPermitidos =
        ["image/jpeg", "image/png", "image/webp"];

    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    public StorageController(IStorageService storage, IRequestTenant tenant, AppDbContext ctx)
    {
        _storage = storage;
        _tenant  = tenant;
        _ctx     = ctx;
    }

    // ── Produtos ──────────────────────────────────────────────────────────────

    [HasPermission(Permissions.ProductEdit)]
    [RequestSizeLimit(5_242_880)]
    [HttpPost("produto/{produtoId:guid}/imagem")]
    public async Task<IActionResult> UploadImagemProduto(
        Guid produtoId, IFormFile arquivo, CancellationToken ct)
    {
        if (!await _ctx.Products.AnyAsync(p => p.Id == produtoId, ct))
            return NotFound(new { erro = "Produto não encontrado." });

        var erro = ValidarArquivo(arquivo);
        if (erro != null) return BadRequest(new { erro });

        try
        {
            await using var stream = arquivo.OpenReadStream();
            var url = await _storage.UploadImagemProdutoAsync(
                _tenant.TenantId, produtoId, stream, arquivo.ContentType, ct);
            return Ok(new { url, mensagem = "Imagem enviada com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            // magic bytes inválidos ou formato não permitido (1.9)
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HasPermission(Permissions.ProductEdit)]
    [HttpDelete("produto/{produtoId:guid}/imagem")]
    public async Task<IActionResult> DeletarImagemProduto(Guid produtoId, CancellationToken ct)
    {
        if (!await _ctx.Products.AnyAsync(p => p.Id == produtoId, ct))
            return NotFound(new { erro = "Produto não encontrado." });

        await _storage.DeletarImagemProdutoAsync(_tenant.TenantId, produtoId, ct);
        return NoContent();
    }

    // ── Entregas (container PRIVADO — LGPD) ──────────────────────────────────

    [RequestSizeLimit(5_242_880)]
    [HttpPost("entrega/{entregaId:guid}/foto")]
    public async Task<IActionResult> UploadFotoEntrega(
        Guid entregaId, IFormFile arquivo, CancellationToken ct)
    {
        if (!await _ctx.Entregas.AnyAsync(e => e.Id == entregaId, ct))
            return NotFound(new { erro = "Entrega não encontrada." });

        var erro = ValidarArquivo(arquivo);
        if (erro != null) return BadRequest(new { erro });

        try
        {
            await using var stream = arquivo.OpenReadStream();
            var sasUrl = await _storage.UploadFotoEntregaAsync(
                _tenant.TenantId, entregaId, stream, arquivo.ContentType, ct);
            return Ok(new
            {
                url              = sasUrl,
                expiresInSeconds = 300,
                mensagem         = "Foto enviada sem EXIF. A URL expira em 5 minutos."
            });
        }
        catch (InvalidOperationException ex)
        {
            // magic bytes inválidos ou formato não permitido (1.9)
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpGet("entrega/{entregaId:guid}/fotos")]
    public async Task<IActionResult> ListarFotosEntrega(Guid entregaId, CancellationToken ct)
    {
        if (!await _ctx.Entregas.AnyAsync(e => e.Id == entregaId, ct))
            return NotFound(new { erro = "Entrega não encontrada." });

        var sasUrls = await _storage.ListarFotosEntregaAsync(_tenant.TenantId, entregaId, ct);
        return Ok(new { entregaId, fotos = sasUrls, expiresInSeconds = 300 });
    }

    [HttpGet("entrega/{entregaId:guid}/foto/{*fileName}")]
    public async Task<IActionResult> GetSasFotoEntrega(
        Guid entregaId, string fileName, CancellationToken ct)
    {
        if (!await _ctx.Entregas.AnyAsync(e => e.Id == entregaId, ct))
            return NotFound(new { erro = "Entrega não encontrada." });

        try
        {
            var sasUrl = await _storage.GerarSasFotoEntregaAsync(
                _tenant.TenantId, entregaId, fileName, ct);
            return Ok(new { url = sasUrl, expiresInSeconds = 300 });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { erro = "Foto não encontrada." });
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? ValidarArquivo(IFormFile arquivo)
    {
        if (arquivo == null || arquivo.Length == 0)
            return "Nenhum arquivo enviado.";

        if (arquivo.Length > MaxBytes)
            return $"Arquivo muito grande: {arquivo.Length / 1024 / 1024:F1}MB. Máximo: 5MB.";

        // Pre-check de Content-Type para UX — falha rápida sem ler o body.
        // Não é validação de segurança: o StorageService valida magic bytes via ImageSharp.
        if (!_tiposPermitidos.Contains(arquivo.ContentType.ToLower()))
            return $"Tipo não permitido: {arquivo.ContentType}. Use JPEG, PNG ou WebP.";

        return null;
    }
}