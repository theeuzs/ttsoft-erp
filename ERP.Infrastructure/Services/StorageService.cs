using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>Interface pública do serviço de armazenamento de arquivos.</summary>
public interface IStorageService
{
    /// <summary>
    /// Faz upload de imagem de produto.
    /// 1.9: valida magic bytes antes do upload (Content-Type declarado pelo cliente é ignorado).
    /// Retorna URL pública (container público — catálogo).
    /// </summary>
    Task<string> UploadImagemProdutoAsync(Guid tenantId, Guid produtoId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Faz upload de foto de comprovante de entrega.
    /// 1.7.5: container PRIVADO (LGPD). 1.9: magic bytes validados.
    /// Retorna SAS URL de 5 min.
    /// </summary>
    Task<string> UploadFotoEntregaAsync(Guid tenantId, Guid entregaId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>Remove a imagem de um produto.</summary>
    Task DeletarImagemProdutoAsync(Guid tenantId, Guid produtoId, CancellationToken ct = default);

    /// <summary>Lista todas as fotos de uma entrega. Retorna SAS URLs de 5 min.</summary>
    Task<IReadOnlyList<string>> ListarFotosEntregaAsync(Guid tenantId, Guid entregaId, CancellationToken ct = default);

    /// <summary>Gera SAS URL de 5 min para uma foto específica de entrega (1.7.5).</summary>
    Task<string> GerarSasFotoEntregaAsync(Guid tenantId, Guid entregaId, string fileName, CancellationToken ct = default);
}

public class StorageService : IStorageService
{
    private readonly BlobServiceClient       _client;
    private readonly string                  _containerProdutos;
    private readonly string                  _containerEntregas;
    private readonly ILogger<StorageService> _logger;

    private const int SasExpiryMinutes = 5;

    public StorageService(IConfiguration config, ILogger<StorageService> logger)
    {
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
        // 1.9: valida magic bytes — rejeita arquivos com Content-Type forjado
        var (realContentType, ext) = await ValidarMagicBytesAsync(stream, ct);

        var container = await GetOrCreateContainerAsync(_containerProdutos, PublicAccessType.Blob, ct);
        var blobName  = $"{tenantId}/{produtoId}{ext}";
        var blob      = container.GetBlobClient(blobName);

        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType  = realContentType,   // usa tipo detectado, não o do cliente
                CacheControl = "public, max-age=31536000"
            }
        }, ct);

        _logger.LogInformation("Imagem produto {ProdutoId} (tenant {TenantId}): {Url}",
            produtoId, tenantId, blob.Uri);
        return blob.Uri.ToString();
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
        // 1.9: magic bytes validados — Content-Type declarado pelo cliente é ignorado
        //
        // TODO (EXIF/LGPD): adicionar remoção de metadados GPS quando uma biblioteca
        // free-commercial for escolhida (SkiaSharp MIT ou Magick.NET Apache 2.0).
        // Hoje a foto chega ao Azure com EXIF intacto — o container privado + SAS 5min
        // mitiga o risco de exposição, mas geolocalização ainda está nos bytes.
        var (realContentType, ext) = await ValidarMagicBytesAsync(stream, ct);

        var container = await GetOrCreatePrivateContainerAsync(_containerEntregas, ct);
        var blobName  = $"{tenantId}/{entregaId}/{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var blob      = container.GetBlobClient(blobName);

        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = realContentType }
        }, ct);

        _logger.LogInformation("Foto entrega {EntregaId} (tenant {TenantId}): {BlobName}",
            entregaId, tenantId, blobName);
        return GerarSas(blob, SasExpiryMinutes).ToString();
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
    /// 1.9 — Valida magic bytes do arquivo sem dependências externas.
    ///
    /// O Content-Type HTTP é enviado pelo cliente e pode ser forjado trivialmente.
    /// Um atacante autenticado pode enviar um .html ou .svg com script malicioso
    /// declarando Content-Type: image/jpeg. Esta função lê os primeiros bytes reais
    /// do arquivo e identifica o formato por assinatura binária (magic bytes),
    /// independente do que o cliente declarou.
    ///
    /// Assinaturas verificadas:
    ///   JPEG : FF D8 FF
    ///   PNG  : 89 50 4E 47 0D 0A 1A 0A
    ///   WebP : 52 49 46 46 ?? ?? ?? ?? 57 45 42 50  (RIFF....WEBP)
    ///
    /// Garante que a extensão e o Content-Type gravados no Azure sejam os reais,
    /// não os declarados pelo cliente.
    /// </summary>
    private static async Task<(string ContentType, string Extension)>
        ValidarMagicBytesAsync(Stream input, CancellationToken ct)
    {
        input.Position = 0;
        var header = new byte[12];
        var read   = await input.ReadAsync(header.AsMemory(0, 12), ct);
        input.Position = 0; // reset: stream será lido novamente no upload

        if (read < 3)
            throw new InvalidOperationException("Arquivo inválido ou corrompido.");

        // JPEG: FF D8 FF
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ("image/jpeg", ".jpg");

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (read >= 8 &&
            header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
            header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A)
            return ("image/png", ".png");

        // WebP: RIFF????WEBP (bytes 0-3 = RIFF, bytes 8-11 = WEBP)
        if (read >= 12 &&
            header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
            header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
            return ("image/webp", ".webp");

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
}