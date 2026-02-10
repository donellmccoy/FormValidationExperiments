using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Api.Data;
using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;
using System.Linq.Dynamic.Core;

namespace FormValidationExperiments.Api.Services;

/// <summary>
/// Service for performing Line of Duty database operations against the in-memory EF Core database.
/// </summary>
public class LineOfDutyCaseService : ILineOfDutyCaseService
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    public LineOfDutyCaseService(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ──────────────────────────── Case Operations ────────────────────────────

    public async Task<List<LineOfDutyCase>> GetAllCasesAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Cases.ToListAsync();
    }

    public async Task<FormValidationExperiments.Shared.ViewModels.PagedResult<LineOfDutyCase>> GetCasesPagedAsync(int skip, int take, string? filter = null, string? orderBy = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        IQueryable<LineOfDutyCase> query = context.Cases;

        // Apply filtering if provided
        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Where(filter);
        }

        // Apply sorting if provided
        if (!string.IsNullOrEmpty(orderBy))
        {
            query = query.OrderBy(orderBy);
        }
        else
        {
            // Default sort by CaseId descending
            query = query.OrderByDescending(c => c.CaseId);
        }

        var totalCount = await query.CountAsync();
        var items = await query.Skip(skip).Take(take).ToListAsync();

        return new FormValidationExperiments.Shared.ViewModels.PagedResult<LineOfDutyCase>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<LineOfDutyCase> GetCaseByIdAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lodCase = await context.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (lodCase is not null)
            await LoadNavigationPropertiesAsync(context, lodCase);
        return lodCase;
    }

    public async Task<LineOfDutyCase> GetCaseByCaseIdAsync(string caseId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lodCase = await context.Cases.FirstOrDefaultAsync(c => c.CaseId == caseId);
        if (lodCase is not null)
            await LoadNavigationPropertiesAsync(context, lodCase);
        return lodCase;
    }

    private static async Task LoadNavigationPropertiesAsync(EctDbContext context, LineOfDutyCase lodCase)
    {
        await context.Entry(lodCase).Collection(c => c.Documents).LoadAsync();
        await context.Entry(lodCase).Collection(c => c.Authorities).LoadAsync();
        await context.Entry(lodCase).Collection(c => c.TimelineSteps).LoadAsync();
        await context.Entry(lodCase).Collection(c => c.Appeals).LoadAsync();
        await context.Entry(lodCase).Reference(c => c.MEDCON).LoadAsync();
        await context.Entry(lodCase).Reference(c => c.INCAP).LoadAsync();

        // Load nested navigations
        foreach (var appeal in lodCase.Appeals ?? [])
            await context.Entry(appeal).Reference(a => a.AppellateAuthority).LoadAsync();
        foreach (var step in lodCase.TimelineSteps ?? [])
            await context.Entry(step).Reference(t => t.ResponsibleAuthority).LoadAsync();
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
