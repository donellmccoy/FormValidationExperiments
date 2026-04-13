using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace ECTSystem.Api.Services;

public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        var containerName = configuration.GetValue<string>("BlobStorage:ContainerName") ?? "lod-documents";
        _container = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, int caseId, CancellationToken ct)
    {
        // Generate a unique blob name: cases/{caseId}/{guid}-{sanitizedFileName}
        var sanitized = Path.GetFileName(fileName); // strip directory traversal
        var blobName = $"cases/{caseId}/{Guid.NewGuid():N}-{sanitized}";
        var blobClient = _container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers }, ct);

        return blobName;
    }

    public async Task<Stream> OpenReadAsync(string blobPath, CancellationToken ct)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        return await blobClient.OpenReadAsync(cancellationToken: ct);
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public Uri GenerateDownloadSasUri(string blobPath, string fileName, TimeSpan expiry)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry),
            ContentDisposition = $"attachment; filename=\"{fileName}\""
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder);
    }

    public async Task DeleteBatchAsync(IEnumerable<string> blobPaths, CancellationToken ct)
    {
        foreach (var path in blobPaths)
        {
            await DeleteAsync(path, ct);
        }
    }
}
