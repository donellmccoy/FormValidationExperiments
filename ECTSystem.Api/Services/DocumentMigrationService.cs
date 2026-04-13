using Microsoft.EntityFrameworkCore;
using ECTSystem.Persistence.Data;

namespace ECTSystem.Api.Services;

/// <summary>
/// One-time migration tool that copies existing inline document Content to blob storage.
/// Queries documents where Content is non-empty and BlobPath is empty, uploads each to
/// blob storage, sets BlobPath, and clears Content. Processes in configurable batches.
/// </summary>
public sealed class DocumentMigrationService
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<DocumentMigrationService> _logger;

    public DocumentMigrationService(
        IDbContextFactory<EctDbContext> contextFactory,
        IBlobStorageService blobStorage,
        ILogger<DocumentMigrationService> logger)
    {
        _contextFactory = contextFactory;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        const int batchSize = 50;
        var totalMigrated = 0;

        _logger.LogInformation("Starting document content-to-blob migration");

        while (!ct.IsCancellationRequested)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            // Find documents with inline content that haven't been migrated yet
            var batch = await context.Documents
                .Where(d => d.Content != null && d.Content.Length > 0 && d.BlobPath == "")
                .OrderBy(d => d.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            foreach (var doc in batch)
            {
                using var stream = new MemoryStream(doc.Content);
                var blobPath = await _blobStorage.UploadAsync(
                    stream, doc.FileName, doc.ContentType, doc.LineOfDutyCaseId, ct);

                doc.BlobPath = blobPath;
                doc.Content = Array.Empty<byte>();

                _logger.LogInformation(
                    "Migrated document {DocumentId} for case {CaseId} to blob {BlobPath}",
                    doc.Id, doc.LineOfDutyCaseId, blobPath);
            }

            await context.SaveChangesAsync(ct);
            totalMigrated += batch.Count;

            _logger.LogInformation("Migrated {Count} documents so far", totalMigrated);
        }

        _logger.LogInformation("Migration complete. Total documents migrated: {Total}", totalMigrated);
    }
}
