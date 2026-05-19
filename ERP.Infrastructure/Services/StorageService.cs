// ── ERP.Infrastructure/Services/StorageService.cs ────────────────────────────
// 2.2 — Azure Blob Storage para imagens de produtos e fotos de entrega.
//
// Pacote necessário (já está no Azure SDK):
//   dotnet add ERP.Infrastructure package Azure.Storage.Blobs
//
// Configuração no appsettings.json:
//   "AzureStorage": {
//     "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
//     "ContainerProdutos": "produto-imagens",
//     "ContainerEntregas": "entrega-fotos"
//   }
//
// Configuração no Azure App Service → Configuration:
//   AzureStorage__ConnectionString = DefaultEndpointsProtocol=https;...
// ─────────────────────────────────────────────────────────────────────────────
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>Interface pública do serviço de armazenamento de arquivos.</summary>
public interface IStorageService
{
    /// <summary>Faz upload de imagem de produto. Retorna a URL pública.</summary>
    Task<string> UploadImagemProdutoAsync(Guid produtoId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>Faz upload de foto de comprovante de entrega. Retorna a URL pública.</summary>
    Task<string> UploadFotoEntregaAsync(Guid entregaId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>Remove a imagem de um produto.</summary>
    Task DeletarImagemProdutoAsync(Guid produtoId, CancellationToken ct = default);

    /// <summary>Lista todas as fotos de uma entrega.</summary>
    Task<IReadOnlyList<string>> ListarFotosEntregaAsync(Guid entregaId, CancellationToken ct = default);
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
        Guid produtoId, Stream stream, string contentType, CancellationToken ct = default)
    {
        var container = await GetOrCreateContainerAsync(_containerProdutos, PublicAccessType.Blob, ct);

        // Normaliza extensão pelo content-type
        var ext      = contentType switch
        {
            "image/jpeg" => ".jpg",
            "image/png"  => ".png",
            "image/webp" => ".webp",
            _            => ".jpg"
        };
        var blobName = $"{produtoId}{ext}";
        var blob     = container.GetBlobClient(blobName);

        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType  = contentType,
                CacheControl = "public, max-age=31536000" // 1 ano — imagens são imutáveis por ID
            }
        }, ct);

        _logger.LogInformation("Imagem do produto {ProdutoId} enviada: {Url}", produtoId, blob.Uri);
        return blob.Uri.ToString();
    }

    public async Task<string> UploadFotoEntregaAsync(
        Guid entregaId, Stream stream, string contentType, CancellationToken ct = default)
    {
        var container = await GetOrCreateContainerAsync(_containerEntregas, PublicAccessType.Blob, ct);

        // Múltiplas fotos por entrega — timestamp garante nome único
        var ext      = contentType == "image/png" ? ".png" : ".jpg";
        var blobName = $"{entregaId}/{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var blob     = container.GetBlobClient(blobName);

        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);

        _logger.LogInformation("Foto de entrega {EntregaId} enviada: {Url}", entregaId, blob.Uri);
        return blob.Uri.ToString();
    }

    public async Task DeletarImagemProdutoAsync(Guid produtoId, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(_containerProdutos);

        // Tenta deletar .jpg e .png (não sabe qual foi enviado)
        foreach (var ext in new[] { ".jpg", ".png", ".webp" })
        {
            var blob = container.GetBlobClient($"{produtoId}{ext}");
            await blob.DeleteIfExistsAsync(cancellationToken: ct);
        }
    }

    public async Task<IReadOnlyList<string>> ListarFotosEntregaAsync(
        Guid entregaId, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(_containerEntregas);
        var urls      = new List<string>();

        await foreach (var blob in container.GetBlobsAsync(
    BlobTraits.None, BlobStates.All,
    prefix: $"{entregaId}/", cancellationToken: ct))
        {
            var blobClient = container.GetBlobClient(blob.Name);
            urls.Add(blobClient.Uri.ToString());
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
}
