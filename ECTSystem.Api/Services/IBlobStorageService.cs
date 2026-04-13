namespace ECTSystem.Api.Services;

/// <summary>
/// Abstraction for binary document storage.
/// Production: Azure Blob Storage. Development: Azurite or local file system.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a file stream to blob storage and returns the relative blob path.
    /// </summary>
    /// <param name="stream">The file content stream.</param>
    /// <param name="fileName">The original file name (used for content-disposition, not the blob name).</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="caseId">The parent case ID (used as a virtual directory prefix).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The relative blob path (e.g., "cases/42/a1b2c3d4-report.pdf").</returns>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, int caseId, CancellationToken ct = default);

    /// <summary>
    /// Opens a read-only stream to the blob content.
    /// Use for proxy-streaming downloads through the API.
    /// </summary>
    Task<Stream> OpenReadAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Deletes a blob by its relative path.
    /// </summary>
    Task DeleteAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Generates a time-limited read-only SAS URI for direct client download.
    /// </summary>
    /// <param name="blobPath">The relative blob path.</param>
    /// <param name="fileName">The original file name for Content-Disposition.</param>
    /// <param name="expiry">How long the SAS URI should be valid.</param>
    /// <returns>A fully-qualified SAS URI.</returns>
    Uri GenerateDownloadSasUri(string blobPath, string fileName, TimeSpan expiry);

    /// <summary>
    /// Deletes multiple blobs by their relative paths (batch cleanup).
    /// </summary>
    Task DeleteBatchAsync(IEnumerable<string> blobPaths, CancellationToken ct = default);
}
