using Azure.Storage.Blobs;

namespace ECTSystem.Api.Services;

/// <summary>
/// Ensures the blob container exists when the API starts up.
/// </summary>
public sealed class BlobContainerInitializer : IHostedService
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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var containerName = _configuration.GetValue<string>("BlobStorage:ContainerName") ?? "lod-documents";
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        var created = await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        if (created?.Value != null)
            _logger.LogInformation("Created blob container '{Container}'", containerName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
