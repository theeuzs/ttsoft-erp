using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ERP.Infrastructure.Services;

/// <summary>Interface pública do serviço de armazenamento de arquivos.</summary>
public interface IStorageService
{
    /// <summary>
    /// Faz upload de imagem de produto.
    /// 1.9: valida magic bytes + remove EXIF via re-encode (SkiaSharp).
    /// Retorna URL pública (container público — catálogo).
    /// </summary>
    Task<string> UploadImagemProdutoAsync(Guid tenantId, Guid produtoId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Faz upload de foto de comprovante de entrega.
    /// 1.7.5: container PRIVADO (LGPD). 1.9: magic bytes + GPS/EXIF removidos via re-encode.
    /// Retorna SAS URL de 5 min.
    /// </summary>
    Task<string> UploadFotoEntregaAsync(Guid tenantId, Guid entregaId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>Remove a imagem de um produto.</summary>
    Task DeletarImagemProdutoAsync(Guid tenantId, Guid produtoId, CancellationToken ct = default);

    /// <summary>Lista todas as fotos de uma entrega. Retorna SAS URLs de 5 min.</summary>
    Task<IReadOnlyList<string>> ListarFotosEntregaAsync(Guid tenantId, Guid entregaId, CancellationToken ct = default);

    /// <summary>Gera SAS URL de 5 min para uma foto específica de entrega (1.7.5).</summary>
    Task<string> GerarSasFotoEntregaAsync(Guid tenantId, Guid entregaId, string fileName, CancellationToken ct = default);

    // S15 FIX: checagens de existência movidas do StorageController — controller
    // não deve tocar AppDbContext diretamente. Ficam aqui (não num service à parte)
    // porque são precondição direta das operações de storage acima: não faz
    // sentido subir/listar/gerar SAS pra um produto ou entrega que não existe.
    /// <summary>Verifica se o produto existe (qualquer tenant — filtro já aplicado pelo HasQueryFilter).</summary>
    Task<bool> ProdutoExisteAsync(Guid produtoId, CancellationToken ct = default);

    /// <summary>Verifica se a entrega existe (qualquer tenant — filtro já aplicado pelo HasQueryFilter).</summary>
    Task<bool> EntregaExisteAsync(Guid entregaId, CancellationToken ct = default);
}

public class StorageService : IStorageService
{
    private readonly BlobServiceClient       _client;
    private readonly string                  _containerProdutos;
    private readonly string                  _containerEntregas;
    private readonly ILogger<StorageService> _logger;
    private readonly ERP.Persistence.Context.AppDbContext _ctx;

    private const int SasExpiryMinutes = 5;

    public StorageService(
        IConfiguration config, ILogger<StorageService> logger, ERP.Persistence.Context.AppDbContext ctx)
    {
        _ctx = ctx;
        var connStr = config["AzureStorage:ConnectionString"]
            ?? throw new InvalidOperationException(
                "AzureStorage:ConnectionString não configurada. " +
                "Adicione no Azure App Service → Configuration → AzureStorage__ConnectionString.");

        _client = new BlobServiceClient(connStr);

        // INTENCIONAL: container de produtos é PÚBLICO (catálogo acessível pelo portal/WPF).
        // Container de entregas é PRIVADO — fotos contêm PII (rosto, assinatura, endereço, placa).
        // Não "uniformizar" para Blob — a diferença é deliberada (LGPD / 1.7.5).
        _containerProdutos = config["AzureStorage:ContainerProdutos"] ?? "produto-imagens";
        _containerEntregas = config["AzureStorage:ContainerEntregas"] ?? "entrega-fotos";
        _logger            = logger;
    }

    // ── Produtos (container PÚBLICO — catálogo) ───────────────────────────────

    public async Task<string> UploadImagemProdutoAsync(
        Guid tenantId, Guid produtoId, Stream stream, string contentType, CancellationToken ct = default)
    {
        // 1.9: magic bytes + EXIF removido via re-encode (SkiaSharp)
        var (cleanStream, realContentType, ext) = await ProcessarImagemAsync(stream, ct);
        await using (cleanStream)
        {
            var container = await GetOrCreateContainerAsync(_containerProdutos, PublicAccessType.Blob, ct);
            var blobName  = $"{tenantId}/{produtoId}{ext}";
            var blob      = container.GetBlobClient(blobName);

            await blob.UploadAsync(cleanStream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType  = realContentType,
                    CacheControl = "public, max-age=31536000"
                }
            }, ct);

            _logger.LogInformation("Imagem produto {ProdutoId} (tenant {TenantId}): {Url}",
                produtoId, tenantId, blob.Uri);
            return blob.Uri.ToString();
        }
    }

    public async Task DeletarImagemProdutoAsync(Guid tenantId, Guid produtoId, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(_containerProdutos);
        foreach (var ext in new[] { ".jpg", ".png", ".webp" })
        {
            var blob = container.GetBlobClient($"{tenantId}/{produtoId}{ext}");
            await blob.DeleteIfExistsAsync(cancellationToken: ct);
        }
    }

    // ── Entregas (container PRIVADO — LGPD) ──────────────────────────────────

    public async Task<string> UploadFotoEntregaAsync(
        Guid tenantId, Guid entregaId, Stream stream, string contentType, CancellationToken ct = default)
    {
        // 1.7.5: container privado — fotos de entrega contêm PII
        // 1.9: magic bytes + GPS/EXIF removidos via re-encode (LGPD — Art. 5º, II)
        //      Câmeras de celular embutem coordenadas GPS no EXIF. O re-encode pelo
        //      SkiaSharp garante que os metadados não chegam ao Azure Blob Storage.
        var (cleanStream, realContentType, ext) = await ProcessarImagemAsync(stream, ct);
        await using (cleanStream)
        {
            var container = await GetOrCreatePrivateContainerAsync(_containerEntregas, ct);
            var blobName  = $"{tenantId}/{entregaId}/{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{ext}";
            var blob      = container.GetBlobClient(blobName);

            await blob.UploadAsync(cleanStream, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = realContentType }
            }, ct);

            _logger.LogInformation("Foto entrega {EntregaId} (tenant {TenantId}): {BlobName}",
                entregaId, tenantId, blobName);
            return GerarSas(blob, SasExpiryMinutes).ToString();
        }
    }

    public async Task<IReadOnlyList<string>> ListarFotosEntregaAsync(
        Guid tenantId, Guid entregaId, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(_containerEntregas);
        var sasUrls   = new List<string>();

        await foreach (var blobItem in container.GetBlobsAsync(
            BlobTraits.None, BlobStates.All,
            prefix: $"{tenantId}/{entregaId}/", cancellationToken: ct))
        {
            var blob = container.GetBlobClient(blobItem.Name);
            sasUrls.Add(GerarSas(blob, SasExpiryMinutes).ToString());
        }

        return sasUrls;
    }

    public async Task<string> GerarSasFotoEntregaAsync(
        Guid tenantId, Guid entregaId, string fileName, CancellationToken ct = default)
    {
        var blobName  = $"{tenantId}/{entregaId}/{fileName}";
        var container = _client.GetBlobContainerClient(_containerEntregas);
        var blob      = container.GetBlobClient(blobName);

        if (!await blob.ExistsAsync(ct))
            throw new FileNotFoundException($"Foto não encontrada: {fileName}");

        return GerarSas(blob, SasExpiryMinutes).ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 1.9 — Valida magic bytes, confirma que é uma imagem real e remove TODOS os metadados
    /// via re-encode com SkiaSharp (MIT).
    ///
    /// Fluxo de segurança:
    ///   1. Lê os primeiros 12 bytes e identifica o formato por assinatura binária.
    ///      Um .html renomeado como .jpg tem magic bytes 3C 21 44 4F — rejeitado.
    ///   2. Decodifica com SKBitmap.Decode — se falhar, o arquivo é inválido/corrompido.
    ///   3. Re-encode via SkiaSharp: o bitmap decodificado é salvo em novo stream.
    ///      O SkiaSharp NÃO preserva metadados no encode — EXIF, GPS, IPTC, XMP
    ///      desaparecem porque o encoder escreve apenas pixels + cabeçalho mínimo.
    ///
    /// O stream retornado é sempre um MemoryStream novo — o original não é modificado.
    /// </summary>
    private static async Task<(MemoryStream Stream, string ContentType, string Extension)>
        ProcessarImagemAsync(Stream input, CancellationToken ct)
    {
        // Lê tudo em memória (limite: 5MB enforçado pelo [RequestSizeLimit] no controller)
        input.Position = 0;
        using var ms   = new MemoryStream();
        await input.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        // 1. Detecta formato por magic bytes — independente do Content-Type HTTP
        var (contentType, skFormat, ext) = DetectarFormato(bytes);

        // 2. Decodifica e valida integridade (SKBitmap.Decode retorna null se inválido)
        using var bitmap = SKBitmap.Decode(bytes);
        if (bitmap == null)
            throw new InvalidOperationException(
                "Arquivo não é uma imagem válida ou está corrompido.");

        // 3. Re-encode sem metadados: SkiaSharp escreve apenas pixels no output
        var quality = skFormat == SKEncodedImageFormat.Jpeg ? 85 : 100;
        using var encoded = bitmap.Encode(skFormat, quality);
        if (encoded == null)
            throw new InvalidOperationException(
                $"Falha ao processar imagem no formato {ext}.");

        var output = new MemoryStream();
        encoded.SaveTo(output);
        output.Position = 0;
        return (output, contentType, ext);
    }

    /// <summary>
    /// Detecta o formato real pelo conteúdo binário, não pelo header HTTP.
    /// Suporta JPEG, PNG e WebP — os únicos formatos aceitos pelo sistema.
    /// </summary>
    private static (string ContentType, SKEncodedImageFormat Format, string Extension)
        DetectarFormato(byte[] bytes)
    {
        if (bytes.Length < 3)
            throw new InvalidOperationException("Arquivo inválido ou corrompido.");

        // JPEG: FF D8 FF
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return ("image/jpeg", SKEncodedImageFormat.Jpeg, ".jpg");

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return ("image/png", SKEncodedImageFormat.Png, ".png");

        // WebP: RIFF????WEBP (bytes 0-3 = RIFF, bytes 8-11 = WEBP)
        if (bytes.Length >= 12 &&
            bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return ("image/webp", SKEncodedImageFormat.Webp, ".webp");

        throw new InvalidOperationException(
            "Arquivo não é uma imagem válida. Envie JPEG, PNG ou WebP.");
    }

    private static Uri GerarSas(BlobClient blob, int minutes)
    {
        var sasBuilder = new BlobSasBuilder(
            BlobSasPermissions.Read,
            DateTimeOffset.UtcNow.AddMinutes(minutes))
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName          = blob.Name
        };
        return blob.GenerateSasUri(sasBuilder);
    }

    private async Task<BlobContainerClient> GetOrCreateContainerAsync(
        string name, PublicAccessType access, CancellationToken ct)
    {
        var container = _client.GetBlobContainerClient(name);
        await container.CreateIfNotExistsAsync(access, cancellationToken: ct);
        return container;
    }

    private async Task<BlobContainerClient> GetOrCreatePrivateContainerAsync(
        string name, CancellationToken ct)
    {
        var container = _client.GetBlobContainerClient(name);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        try
        {
            await container.SetAccessPolicyAsync(PublicAccessType.None, cancellationToken: ct);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning("Não foi possível confirmar container '{Container}' como privado: {Error}",
                name, ex.Message);
        }
        return container;
    }

    // S15 FIX: movidas do StorageController — ver comentário na interface.
    public async Task<bool> ProdutoExisteAsync(Guid produtoId, CancellationToken ct = default)
        => await _ctx.Products.AnyAsync(p => p.Id == produtoId, ct);

    public async Task<bool> EntregaExisteAsync(Guid entregaId, CancellationToken ct = default)
        => await _ctx.Entregas.AnyAsync(e => e.Id == entregaId, ct);
}