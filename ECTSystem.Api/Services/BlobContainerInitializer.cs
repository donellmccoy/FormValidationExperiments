using Azure.Storage.Blobs;

namespace ECTSystem.Api.Services;

/// <summary>
/// Ensures the blob container exists when the API starts up.
/// Extends BackgroundService so container creation does not block host startup.
/// </summary>
public sealed class BlobContainerInitializer : BackgroundService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BlobContainerInitializer> _logger;

    public BlobContainerInitializer(
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        ILogger<BlobContainerInitializer> logger)
    {
        _blobServiceClient = blobServiceClient;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var containerName = _configuration.GetValue<string>("BlobStorage:ContainerName") ?? "lod-documents";
        var container = _blobServiceClient.GetBlobContainerClient(containerName);

        try
        {
            var created = await container.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

            if (created?.Value != null)
                _logger.LogInformation("Created blob container '{Container}'", containerName);
        }
        catch (Exception ex) when (ex is Azure.RequestFailedException or AggregateException)
        {
            _logger.LogWarning(ex, "Could not reach blob storage to ensure container '{Container}' exists. " +
                "Blob operations will fail until storage is available.", containerName);
        }
    }
}
