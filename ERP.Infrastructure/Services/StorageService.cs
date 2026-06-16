using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>Interface pública do serviço de armazenamento de arquivos.</summary>
public interface IStorageService
{
    /// <summary>Faz upload de imagem de produto. Retorna URL pública (container público).</summary>
    Task<string> UploadImagemProdutoAsync(Guid tenantId, Guid produtoId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Faz upload de foto de comprovante de entrega.
    /// Retorna SAS URL de 5 min (container PRIVADO — LGPD).
    /// </summary>
    Task<string> UploadFotoEntregaAsync(Guid tenantId, Guid entregaId, Stream stream, string contentType, CancellationToken ct = default);

    /// <summary>Remove a imagem de um produto.</summary>
    Task DeletarImagemProdutoAsync(Guid tenantId, Guid produtoId, CancellationToken ct = default);

    /// <summary>
    /// Lista todas as fotos de uma entrega.
    /// Retorna SAS URLs de 5 min (container PRIVADO — LGPD).
    /// </summary>
    Task<IReadOnlyList<string>> ListarFotosEntregaAsync(Guid tenantId, Guid entregaId, CancellationToken ct = default);

    /// <summary>
    /// Gera SAS URL de 5 min para uma foto específica de entrega (1.7.5).
    /// Valida que o blob pertence ao tenant antes de gerar a URL.
    /// </summary>
    Task<string> GerarSasFotoEntregaAsync(Guid tenantId, Guid entregaId, string fileName, CancellationToken ct = default);
}

public class StorageService : IStorageService
{
    private readonly BlobServiceClient       _client;
    private readonly string                  _containerProdutos;
    private readonly string                  _containerEntregas;
    private readonly ILogger<StorageService> _logger;

    /// <summary>Validade do SAS para fotos de entrega (container privado).</summary>
    private const int SasExpiryMinutes = 5;

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

    // ── Produtos (container PÚBLICO — imagens de catálogo) ────────────────────

    public async Task<string> UploadImagemProdutoAsync(
        Guid tenantId, Guid produtoId, Stream stream, string contentType, CancellationToken ct = default)
    {
        var container = await GetOrCreateContainerAsync(_containerProdutos, PublicAccessType.Blob, ct);

        var ext      = ContentTypeToExt(contentType);
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
        // 1.7.5: container privado — fotos de entrega contêm PII (local de entrega, cliente)
        var container = await GetOrCreatePrivateContainerAsync(_containerEntregas, ct);

        var ext      = contentType == "image/png" ? ".png" : ".jpg";
        var blobName = $"{tenantId}/{entregaId}/{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}{ext}";
        var blob     = container.GetBlobClient(blobName);

        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);

        _logger.LogInformation("Foto de entrega {EntregaId} (tenant {TenantId}) enviada como blob privado: {BlobName}",
            entregaId, tenantId, blobName);

        // Retorna SAS de curta duração em vez de URL pública (container é privado)
        return GerarSas(blob, SasExpiryMinutes).ToString();
    }

    public async Task<IReadOnlyList<string>> ListarFotosEntregaAsync(
        Guid tenantId, Guid entregaId, CancellationToken ct = default)
    {
        var container = _client.GetBlobContainerClient(_containerEntregas);
        var sasUrls   = new List<string>();

        // Prefixo duplo {tenantId}/{entregaId}/ — listagem naturalmente isolada
        await foreach (var blobItem in container.GetBlobsAsync(
            BlobTraits.None, BlobStates.All,
            prefix: $"{tenantId}/{entregaId}/", cancellationToken: ct))
        {
            var blob = container.GetBlobClient(blobItem.Name);
            // 1.7.5: retorna SAS URL em vez de URL pública — container é privado
            sasUrls.Add(GerarSas(blob, SasExpiryMinutes).ToString());
        }

        return sasUrls;
    }

    public async Task<string> GerarSasFotoEntregaAsync(
        Guid tenantId, Guid entregaId, string fileName, CancellationToken ct = default)
    {
        // fileName = apenas o nome do arquivo (ex: "20260615130000000.jpg")
        // blobName inclui o prefixo de tenant/entrega para isolamento
        var blobName  = $"{tenantId}/{entregaId}/{fileName}";
        var container = _client.GetBlobContainerClient(_containerEntregas);
        var blob      = container.GetBlobClient(blobName);

        if (!await blob.ExistsAsync(ct))
            throw new FileNotFoundException($"Foto não encontrada: {fileName}");

        return GerarSas(blob, SasExpiryMinutes).ToString();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Gera uma SAS URL com permissão de leitura por <paramref name="minutes"/> minutos.
    /// Requer que <see cref="BlobServiceClient"/> tenha sido criado com account key
    /// (não SAS token) — que é o caso quando se usa connection string completa do Azure.
    /// </summary>
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

    /// <summary>
    /// Cria (ou obtém) o container de entregas garantindo acesso PRIVADO.
    /// Se o container já existia como público, força a política para None
    /// para satisfazer o requisito LGPD de não expor PII em URL pública.
    /// </summary>
    private async Task<BlobContainerClient> GetOrCreatePrivateContainerAsync(
        string name, CancellationToken ct)
    {
        var container = _client.GetBlobContainerClient(name);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        // Força privado mesmo se o container já existia com acesso público
        try
        {
            await container.SetAccessPolicyAsync(PublicAccessType.None, cancellationToken: ct);
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogWarning(
                "Não foi possível confirmar container '{Container}' como privado: {Error}",
                name, ex.Message);
        }

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