using Microsoft.EntityFrameworkCore;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

/// <summary>
/// Service for performing Line of Duty database operations.
/// </summary>
public class LineOfDutyCaseService :
    ILineOfDutyDocumentService,
    ILineOfDutyAppealService,
    ILineOfDutyAuthorityService,
    ILineOfDutyTimelineService,
    ILineOfDutyNotificationService
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    public LineOfDutyCaseService(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ──────────────────────────── Document Operations ────────────────────────────

    private const long MaxDocumentSize = 10 * 1024 * 1024; // 10 MB

    public async Task<List<LineOfDutyDocument>> GetDocumentsByCaseIdAsync(int caseId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Documents
            .AsNoTracking()
            .Where(d => d.LineOfDutyCaseId == caseId)
            .Select(d => new LineOfDutyDocument
            {
                Id = d.Id,
                LineOfDutyCaseId = d.LineOfDutyCaseId,
                DocumentType = d.DocumentType,
                FileName = d.FileName,
                ContentType = d.ContentType,
                FileSize = d.FileSize,
                UploadDate = d.UploadDate,
                Description = d.Description
                // Content intentionally excluded
            })
            .ToListAsync(ct);
    }

    public async Task<LineOfDutyDocument> GetDocumentByIdAsync(int documentId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new LineOfDutyDocument
            {
                Id = d.Id,
                LineOfDutyCaseId = d.LineOfDutyCaseId,
                DocumentType = d.DocumentType,
                FileName = d.FileName,
                ContentType = d.ContentType,
                FileSize = d.FileSize,
                UploadDate = d.UploadDate,
                Description = d.Description
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<byte[]> GetDocumentContentAsync(int documentId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => d.Content)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<LineOfDutyDocument> UploadDocumentAsync(
        int caseId, string fileName, string contentType, string documentType,
        string description, Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (bytes.Length > MaxDocumentSize)
            throw new ArgumentException($"File size exceeds the maximum allowed size of {MaxDocumentSize / (1024 * 1024)} MB.");

        var document = new LineOfDutyDocument
        {
            LineOfDutyCaseId = caseId,
            FileName = fileName,
            ContentType = contentType,
            DocumentType = documentType,
            Description = description,
            Content = bytes,
            FileSize = bytes.Length,
            UploadDate = DateTime.UtcNow
        };

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Documents.Add(document);
        await context.SaveChangesAsync(ct);

        document.Content = null!; // Don't return content in response
        return document;
    }

    public async Task<bool> DeleteDocumentAsync(int documentId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents.FindAsync([documentId], ct);
        if (document is null)
            return false;

        context.Documents.Remove(document);
        await context.SaveChangesAsync(ct);
        return true;
    }

    // ──────────────────────────── Appeal Operations ────────────────────────────

    public async Task<List<LineOfDutyAppeal>> GetAppealsByCaseIdAsync(int caseId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Appeals
            .AsNoTracking()
            .Include(a => a.AppellateAuthority)
            .Where(a => a.LineOfDutyCaseId == caseId)
            .ToListAsync(ct);
    }

    public async Task<LineOfDutyAppeal> AddAppealAsync(LineOfDutyAppeal appeal, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Appeals.Add(appeal);
        await context.SaveChangesAsync(ct);
        return appeal;
    }

    // ──────────────────────────── Authority Operations ────────────────────────────

    public async Task<List<LineOfDutyAuthority>> GetAuthoritiesByCaseIdAsync(int caseId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Authorities
            .AsNoTracking()
            .Where(a => a.LineOfDutyCaseId == caseId)
            .ToListAsync(ct);
    }

    public async Task<LineOfDutyAuthority> AddAuthorityAsync(LineOfDutyAuthority authority, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Authorities.Add(authority);
        await context.SaveChangesAsync(ct);
        return authority;
    }

    // ──────────────────────────── Timeline Operations ────────────────────────────

    public async Task<List<TimelineStep>> GetTimelineStepsByCaseIdAsync(int caseId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.TimelineSteps
            .AsNoTracking()
            .Include(t => t.ResponsibleAuthority)
            .Where(t => t.LineOfDutyCaseId == caseId)
            .ToListAsync(ct);
    }

    public async Task<TimelineStep> AddTimelineStepAsync(TimelineStep step, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.TimelineSteps.Add(step);
        await context.SaveChangesAsync(ct);
        return step;
    }

    public async Task<TimelineStep> UpdateTimelineStepAsync(TimelineStep step, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.TimelineSteps.FindAsync([step.Id], ct);
        if (existing is null)
            throw new InvalidOperationException($"TimelineStep with Id {step.Id} not found.");

        context.Entry(existing).CurrentValues.SetValues(step);
        await context.SaveChangesAsync(ct);
        return existing;
    }

    // ──────────────────────────── Notification Operations ────────────────────────────

    public async Task<List<Notification>> GetNotificationsByCaseIdAsync(int caseId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await context.Notifications
            .AsNoTracking()
            .Where(n => n.LineOfDutyCaseId == caseId)
            .OrderByDescending(n => n.CreatedDate)
            .ToListAsync(ct);
    }

    public async Task<Notification> AddNotificationAsync(Notification notification, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var notification = await context.Notifications.FindAsync([notificationId], ct);
        if (notification is null)
            return false;

        notification.IsRead = true;
        notification.ReadDate = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }
}
