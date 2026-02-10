using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Web.Data;
using FormValidationExperiments.Web.Models;

namespace FormValidationExperiments.Web.Services;

/// <summary>
/// Service for performing Line of Duty database operations against the in-memory EF Core database.
/// </summary>
public class LODDatabaseService : ILODDatabaseService
{
    private readonly IDbContextFactory<LODDbContext> _contextFactory;

    public LODDatabaseService(IDbContextFactory<LODDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ──────────────────────────── Case Operations ────────────────────────────

    public async Task<List<LineOfDutyCase>> GetAllCasesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Cases
            .Include(c => c.Documents)
            .Include(c => c.Appeals)
            .Include(c => c.Authorities)
            .Include(c => c.TimelineSteps)
            .Include(c => c.MEDCON)
            .Include(c => c.INCAP)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<LineOfDutyCase> GetCaseByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Cases
            .Include(c => c.Documents)
            .Include(c => c.Appeals)
                .ThenInclude(a => a.AppellateAuthority)
            .Include(c => c.Authorities)
            .Include(c => c.TimelineSteps)
                .ThenInclude(t => t.ResponsibleAuthority)
            .Include(c => c.MEDCON)
            .Include(c => c.INCAP)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<LineOfDutyCase> GetCaseByCaseIdAsync(string caseId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Cases
            .Include(c => c.Documents)
            .Include(c => c.Appeals)
                .ThenInclude(a => a.AppellateAuthority)
            .Include(c => c.Authorities)
            .Include(c => c.TimelineSteps)
                .ThenInclude(t => t.ResponsibleAuthority)
            .Include(c => c.MEDCON)
            .Include(c => c.INCAP)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CaseId == caseId);
    }

    public async Task<LineOfDutyCase> CreateCaseAsync(LineOfDutyCase lodCase)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync();
        return lodCase;
    }

    public async Task<LineOfDutyCase> UpdateCaseAsync(LineOfDutyCase lodCase)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Cases.Update(lodCase);
        await context.SaveChangesAsync();
        return lodCase;
    }

    public async Task<bool> DeleteCaseAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lodCase = await context.Cases.FindAsync(id);
        if (lodCase is null)
            return false;

        context.Cases.Remove(lodCase);
        await context.SaveChangesAsync();
        return true;
    }

    // ──────────────────────────── Document Operations ────────────────────────────

    public async Task<List<LineOfDutyDocument>> GetDocumentsByCaseIdAsync(int caseId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Documents
            .Where(d => d.LineOfDutyCaseId == caseId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<LineOfDutyDocument> AddDocumentAsync(LineOfDutyDocument document)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Documents.Add(document);
        await context.SaveChangesAsync();
        return document;
    }

    public async Task<bool> DeleteDocumentAsync(int documentId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var document = await context.Documents.FindAsync(documentId);
        if (document is null)
            return false;

        context.Documents.Remove(document);
        await context.SaveChangesAsync();
        return true;
    }

    // ──────────────────────────── Appeal Operations ────────────────────────────

    public async Task<List<LineOfDutyAppeal>> GetAppealsByCaseIdAsync(int caseId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Appeals
            .Include(a => a.AppellateAuthority)
            .Where(a => a.LineOfDutyCaseId == caseId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<LineOfDutyAppeal> AddAppealAsync(LineOfDutyAppeal appeal)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Appeals.Add(appeal);
        await context.SaveChangesAsync();
        return appeal;
    }

    // ──────────────────────────── Authority Operations ────────────────────────────

    public async Task<List<LineOfDutyAuthority>> GetAuthoritiesByCaseIdAsync(int caseId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Authorities
            .Where(a => a.LineOfDutyCaseId == caseId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<LineOfDutyAuthority> AddAuthorityAsync(LineOfDutyAuthority authority)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Authorities.Add(authority);
        await context.SaveChangesAsync();
        return authority;
    }

    // ──────────────────────────── Timeline Operations ────────────────────────────

    public async Task<List<TimelineStep>> GetTimelineStepsByCaseIdAsync(int caseId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.TimelineSteps
            .Include(t => t.ResponsibleAuthority)
            .Where(t => t.LineOfDutyCaseId == caseId)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<TimelineStep> AddTimelineStepAsync(TimelineStep step)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TimelineSteps.Add(step);
        await context.SaveChangesAsync();
        return step;
    }

    public async Task<TimelineStep> UpdateTimelineStepAsync(TimelineStep step)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.TimelineSteps.Update(step);
        await context.SaveChangesAsync();
        return step;
    }
}
