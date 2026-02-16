using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

/// <summary>
/// Service for performing Line of Duty database operations.
/// </summary>
public class DataService :
    IDataService,
    ILineOfDutyDocumentService,
    ILineOfDutyAppealService,
    ILineOfDutyAuthorityService,
    ILineOfDutyTimelineService,
    ILineOfDutyNotificationService
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    // Long-lived context for IQueryable-based OData queries (disposed by DI scope)
    private EctDbContext _queryContext;

    public DataService(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ──────────────────────────── Case CRUD Operations ────────────────────────────

    public IQueryable<LineOfDutyCase> GetCasesQueryable()
    {
        // Create a long-lived context — OData needs the query to remain open
        // until the response is serialized. The context will be disposed by the DI scope.
        _queryContext = _contextFactory.CreateDbContext();
        return _queryContext.Cases.AsNoTracking();
    }

    public async Task<LineOfDutyCase> GetCaseByKeyAsync(int key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await CaseWithIncludes(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == key, ct);
    }

    public async Task<LineOfDutyCase> CreateCaseAsync(LineOfDutyCase lodCase, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync(ct);
        return lodCase;
    }

    public async Task<LineOfDutyCase> UpdateCaseAsync(int key, LineOfDutyCase update, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == key, ct);
        if (existing is null)
        {
            return null;
        }

        // 1. Update scalar properties on the root entity
        update.Id = key;
        update.MemberId = existing.MemberId;   // preserve FK — not editable via form
        update.MEDCONId = existing.MEDCONId;
        update.INCAPId = existing.INCAPId;
        context.Entry(existing).CurrentValues.SetValues(update);

        // 2. Synchronize the Authorities collection
        SyncAuthorities(context, existing, update.Authorities);

        // 3. Update MEDCON / INCAP scalar properties
        if (existing.MEDCON is not null && update.MEDCON is not null)
        {
            context.Entry(existing.MEDCON).CurrentValues.SetValues(update.MEDCON);
        }

        if (existing.INCAP is not null && update.INCAP is not null)
        {
            context.Entry(existing.INCAP).CurrentValues.SetValues(update.INCAP);
        }

        await context.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteCaseAsync(int key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var lodCase = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == key, ct);
        if (lodCase is null)
        {
            return false;
        }

        context.TimelineSteps.RemoveRange(lodCase.TimelineSteps);
        context.Authorities.RemoveRange(lodCase.Authorities);
        context.Documents.RemoveRange(lodCase.Documents);
        context.Appeals.RemoveRange(lodCase.Appeals);
        context.Notifications.RemoveRange(lodCase.Notifications);
        context.Cases.Remove(lodCase);
        await context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Applies a partial update using OData Delta semantics — only the properties
    /// present in the Delta are written to the entity via <see cref="Delta{T}.Patch"/>.
    /// </summary>
    public async Task<LineOfDutyCase> PatchCaseAsync(
        int key,
        Delta<LineOfDutyCase> delta,
        CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.Cases.FindAsync([key], ct);
        if (existing is null)
        {
            return null;
        }

        delta.Patch(existing);
        await context.SaveChangesAsync(ct);
        return existing;
    }

    private static IQueryable<LineOfDutyCase> CaseWithIncludes(EctDbContext context)
    {
        return context.Cases
            .AsSplitQuery()
            .Include(c => c.Documents)
            .Include(c => c.Authorities)
            .Include(c => c.TimelineSteps).ThenInclude(t => t.ResponsibleAuthority)
            .Include(c => c.Appeals).ThenInclude(a => a.AppellateAuthority)
            .Include(c => c.Member)
            .Include(c => c.MEDCON)
            .Include(c => c.INCAP)
            .Include(c => c.Notifications);
    }

    private static void SyncAuthorities(
        EctDbContext context,
        LineOfDutyCase existing,
        List<LineOfDutyAuthority> incoming)
    {
        incoming ??= [];

        var incomingIds = incoming.Where(a => a.Id != 0).Select(a => a.Id).ToHashSet();
        var toRemove = existing.Authorities.Where(a => !incomingIds.Contains(a.Id)).ToList();
        foreach (var auth in toRemove)
        {
            existing.Authorities.Remove(auth);
            context.Authorities.Remove(auth);
        }

        foreach (var updatedAuth in incoming)
        {
            var existingAuth = updatedAuth.Id != 0
                ? existing.Authorities.FirstOrDefault(a => a.Id == updatedAuth.Id)
                : null;

            if (existingAuth is not null)
            {
                context.Entry(existingAuth).CurrentValues.SetValues(updatedAuth);
            }
            else
            {
                updatedAuth.Id = 0;
                updatedAuth.LineOfDutyCaseId = existing.Id;
                existing.Authorities.Add(updatedAuth);
            }
        }
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

    public async Task<LineOfDutyDocument> UploadDocumentAsync(int caseId, string fileName, string contentType, string documentType, string description, Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (bytes.Length > MaxDocumentSize)
        {
            throw new ArgumentException($"File size exceeds the maximum allowed size of {MaxDocumentSize / (1024 * 1024)} MB.");
        }

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
        {
            return false;
        }

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
        {
            throw new InvalidOperationException($"TimelineStep with Id {step.Id} not found.");
        }

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
        {
            return false;
        }

        notification.IsRead = true;
        notification.ReadDate = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }
}
