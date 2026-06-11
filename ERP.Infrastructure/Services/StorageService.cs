using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>Interface pública do serviço de armazenamento de arquivos.</summary>
public interface IStorageService
{
    /// <summary>Faz upload de imagem de produto. Retorna a URL pública.</summary>
    Task<string> UploadImagemProdutoAsync(Guid tenantId, Guid produtoId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>Faz upload de foto de comprovante de entrega. Retorna a URL pública.</summary>
    Task<string> UploadFotoEntregaAsync(Guid tenantId, Guid entregaId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>Remove a imagem de um produto.</summary>
    Task DeletarImagemProdutoAsync(Guid tenantId, Guid produtoId, CancellationToken ct = default);

    /// <summary>Lista todas as fotos de uma entrega.</summary>
    Task<IReadOnlyList<string>> ListarFotosEntregaAsync(Guid tenantId, Guid entregaId, CancellationToken ct = default);
}

public class StorageService : IStorageService
{
    private readonly BlobServiceClient       _client;
    private readonly string                  _containerProdutos;
    private readonly string                  _containerEntregas;
    private readonly ILogger<StorageService> _logger;

    public StorageService(IConfiguration config, ILogger<StorageService> logger)
    {
        var connStr = config["AzureStorage:ConnectionString"]
            ?? throw new InvalidOperationException(
                "AzureStorage:ConnectionString não configurada. " +
                "Adicione no Azure App Service → Configuration → AzureStorage__ConnectionString.");

        _client            = new BlobServiceClient(connStr);
        _containerProdutos = config["AzureStorage:ContainerProdutos"] ?? "produto-imagens";
        _containerEntregas = config["AzureStorage:ContainerEntregas"] ?? "entrega-fotos";
        _logger            = logger;
    }

    public async Task<string> UploadImagemProdutoAsync(
        Guid tenantId, Guid produtoId, Stream stream, string contentType, CancellationToken ct = default)
    {
        var container = await GetOrCreateContainerAsync(_containerProdutos, PublicAccessType.Blob, ct);

        var ext      = ContentTypeToExt(contentType);
        // Prefixo {tenantId}/ garante que blobs de tenants diferentes nunca colidam
        // e que a listagem por prefixo fica naturalmente isolada por tenant.
        var blobName = $"{tenantId}/{produtoId}{ext}";
        var blob     = container.GetBlobClient(blobName);

        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType  = contentType,
                CacheControl = "public, max-age=31536000"
            }
        }, ct);

        _logger.LogInformation("Imagem do produto {ProdutoId} (tenant {TenantId}) enviada: {Url}",
            produtoId, tenantId, blob.Uri);
        return blob.Uri.ToString();
    }

    public async Task<string> UploadFotoEntregaAsync(
        Guid tenantId, Guid entregaId, Stream stream, string contentType, CancellationToken ct = default)
    {
        var container = await GetOrCreateContainerAsync(_containerEntregas, PublicAccessType.Blob, ct);

        var ext      = contentType == "image/png" ? ".png" : ".jpg";
        var blobName = $"{tenantId}/{entregaId}/{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var blob     = container.GetBlobClient(blobName);

        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);

        _logger.LogInformation("Foto de entrega {EntregaId} (tenant {TenantId}) enviada: {Url}",
            entregaId, tenantId, blob.Uri);
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

    public async Task<IReadOnlyList<string>> ListarFotosEntregaAsync(
        Guid tenantId, Guid entregaId, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(_containerEntregas);
        var urls      = new List<string>();

        // Prefixo duplo {tenantId}/{entregaId}/ — listagem naturalmente isolada
        await foreach (var blob in container.GetBlobsAsync(
            BlobTraits.None, BlobStates.All,
            prefix: $"{tenantId}/{entregaId}/", cancellationToken: ct))
        {
            urls.Add(container.GetBlobClient(blob.Name).Uri.ToString());
        }

        return urls;
    }

    private async Task<BlobContainerClient> GetOrCreateContainerAsync(
        string name, PublicAccessType access, CancellationToken ct)
    {
        var container = _client.GetBlobContainerClient(name);
        await container.CreateIfNotExistsAsync(access, cancellationToken: ct);
        return container;
    }

    private static string ContentTypeToExt(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png"  => ".png",
        "image/webp" => ".webp",
        _            => ".jpg"
    };
}