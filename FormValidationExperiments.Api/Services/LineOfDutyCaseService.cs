using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Api.Data;
using FormValidationExperiments.Shared.Models;
using System.Linq.Dynamic.Core;
using PagedResult = FormValidationExperiments.Shared.ViewModels.PagedResult<FormValidationExperiments.Shared.Models.LineOfDutyCase>;
using System.Text.RegularExpressions;

namespace FormValidationExperiments.Api.Services;

/// <summary>
/// Service for performing Line of Duty database operations.
/// </summary>
public partial class LineOfDutyCaseService :
    ILineOfDutyCaseService,
    ILineOfDutyDocumentService,
    ILineOfDutyAppealService,
    ILineOfDutyAuthorityService,
    ILineOfDutyTimelineService
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    /// <summary>
    /// Allowed property names for Dynamic LINQ filter/orderBy expressions.
    /// </summary>
    private static readonly FrozenSet<string> AllowedFilterProperties = new[]
    {
        "Id", "CaseId", "ProcessType", "Component", "MemberName", "MemberRank",
        "Unit", "IncidentType", "IncidentDate", "IncidentDutyStatus",
        "FinalFinding", "InitiationDate", "CompletionDate", "IsInterimLOD"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[\w\s\.\,\=\!\<\>\&\|\(\)""%\-\/\?\:]+$")]
    private static partial Regex SafeExpressionPattern();

    [GeneratedRegex(@"\b([A-Za-z_]\w*)\b")]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex(@"""[^""]*""")]
    private static partial Regex QuotedStringPattern();

    public LineOfDutyCaseService(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // ──────────────────────────── Filter Transformation ────────────────────────────

    /// <summary>
    /// Transforms a Radzen DataGrid filter expression into a form that
    /// System.Linq.Dynamic.Core can parse.
    /// <para>
    /// Radzen v9 generates lambda-style filters such as:
    /// <c>x => (((x == null) ?  : x.CaseId) ?? "").Contains("512")</c>
    /// </para>
    /// <para>
    /// Dynamic LINQ's <c>.Where(string)</c> expects just the expression body with
    /// properties referenced directly (e.g. <c>(CaseId ?? "").Contains("512")</c>).
    /// </para>
    /// </summary>
    private static string TransformRadzenFilter(string filter)
    {
        // 1. Detect and strip the lambda parameter declaration: "x => body" → "body"
        var lambdaMatch = Regex.Match(filter, @"^\s*(\w+)\s*=>\s*");
        if (!lambdaMatch.Success)
            return filter; // Not a lambda expression — return as-is

        var paramName = lambdaMatch.Groups[1].Value;
        var body = filter[lambdaMatch.Length..];

        // 2. Remove null-safe entity-check ternary:
        //    "(param == null) ? <optional-default> : <expr>" → "<expr>"
        //    Handles empty true-branch, quoted strings, or "null" as the default value.
        body = Regex.Replace(body,
            $@"\(\s*{Regex.Escape(paramName)}\s*==\s*null\s*\)\s*\?\s*(?:""[^""]*""|null)?\s*:\s*",
            string.Empty);

        // 3. Replace remaining parameter-dot references: "param.Property" → "Property"
        body = Regex.Replace(body, $@"\b{Regex.Escape(paramName)}\.", string.Empty);

        // 4. Strip fully-qualified enum casts:
        //    "(Namespace.SubNs.EnumType)0" → "0"
        //    Radzen generates these for enum column filters.
        body = Regex.Replace(body, @"\(\s*(?:[\w]+\.)+[\w]+\s*\)\s*(?=\d)", string.Empty);

        // 5. Simplify Radzen DateTime expressions:
        //    "DateTime.SpecifyKind(DateTime.Parse("2025-05-10", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), DateTimeKind.Unspecified)"
        //    → "DateTime.Parse(\"2025-05-10\")"
        //    Dynamic LINQ doesn't support CultureInfo, DateTimeStyles, or DateTimeKind types.
        body = Regex.Replace(body,
            @"DateTime\.SpecifyKind\s*\(\s*DateTime\.Parse\s*\(\s*""([^""]+)""\s*,\s*CultureInfo\.\w+\s*,\s*DateTimeStyles\.\w+\s*\)\s*,\s*DateTimeKind\.\w+\s*\)",
            @"DateTime.Parse(""$1"")");

        return body;
    }

    // ──────────────────────────── Validation ────────────────────────────

    /// <summary>
    /// Validates that a Dynamic LINQ expression only references allowed properties
    /// and contains no dangerous characters.
    /// </summary>
    private static void ValidateDynamicExpression(string expression)
    {
        if (!SafeExpressionPattern().IsMatch(expression))
            throw new ArgumentException("Filter/orderBy expression contains invalid characters.");

        // Strip quoted string literals so their contents aren't treated as identifiers
        var stripped = QuotedStringPattern().Replace(expression, string.Empty);

        // Extract identifiers (words that aren't keywords, literals, or method names)
        var identifiers = IdentifierPattern().Matches(stripped)
            .Select(m => m.Value)
            .Where(v => !IsKeywordOrLiteral(v))
            .ToList();

        foreach (var id in identifiers)
        {
            if (!AllowedFilterProperties.Contains(id))
                throw new ArgumentException($"Property '{id}' is not allowed in filter/orderBy expressions.");
        }
    }

    private static bool IsKeywordOrLiteral(string value)
    {
        return value is "and" or "or" or "not" or "null" or "true" or "false"
            or "asc" or "ascending" or "desc" or "descending"
            or "it" or "np" or "new" or "iif" or "as" or "is" or "x"
            or "DateTime" or "String" or "Int32" or "Int64" or "Boolean"
            or "Double" or "Decimal" or "Single" or "Byte" or "Guid" or "TimeSpan"
            // Type conversion methods used by Radzen DataGrid for non-string columns
            or "Convert" or "ToInt32" or "ToInt64" or "ToDouble" or "ToDecimal"
            or "ToByte" or "ToSingle" or "ToBoolean" or "ToDateTime" or "Parse"
            // Dynamic LINQ methods used by Radzen DataGrid filtering
            or "Contains" or "StartsWith" or "EndsWith"
            or "ToLower" or "ToUpper" or "ToString" or "Trim"
            or "Length" or "Substring" or "IndexOf" or "Replace" or "Equals"
            or "Year" or "Month" or "Day" or "Hour" or "Minute" or "Second"
            // DateTime helpers used by Radzen DataGrid date filters
            or "SpecifyKind" or "DateTimeKind" or "Unspecified" or "Utc" or "Local"
            or "CultureInfo" or "InvariantCulture" or "DateTimeStyles" or "RoundtripKind"
            // Nullable helpers
            or "Value" or "HasValue"
            || int.TryParse(value, out _);
    }

    // ──────────────────────────── Case Operations ────────────────────────────

    private static IQueryable<LineOfDutyCase> CaseWithIncludes(EctDbContext context)
    {
        return context.Cases
            .AsSplitQuery()
            .Include(c => c.Documents)
            .Include(c => c.Authorities)
            .Include(c => c.TimelineSteps).ThenInclude(t => t.ResponsibleAuthority)
            .Include(c => c.Appeals).ThenInclude(a => a.AppellateAuthority)
            .Include(c => c.MEDCON)
            .Include(c => c.INCAP);
    }

    public async Task<PagedResult> GetCasesPagedAsync(
        int skip, int take, string? filter = null, string? orderBy = null, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 100);

        IQueryable<LineOfDutyCase> query = context.Cases.AsNoTracking();

        if (!string.IsNullOrEmpty(filter))
        {
            filter = TransformRadzenFilter(filter);
            ValidateDynamicExpression(filter);
            query = query.Where(filter);
        }

        if (!string.IsNullOrEmpty(orderBy))
        {
            orderBy = TransformRadzenFilter(orderBy);
            ValidateDynamicExpression(orderBy);
            query = query.OrderBy(orderBy);
        }
        else
        {
            query = query.OrderByDescending(c => c.CaseId);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query.Skip(skip).Take(take).ToListAsync(ct);

        return new PagedResult
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<LineOfDutyCase?> GetCaseByIdAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await CaseWithIncludes(context).AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<LineOfDutyCase?> GetCaseByCaseIdAsync(string caseId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        return await CaseWithIncludes(context).AsNoTracking().FirstOrDefaultAsync(c => c.CaseId == caseId, ct);
    }

    public async Task<LineOfDutyCase> CreateCaseAsync(LineOfDutyCase lodCase, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync(ct);
        return lodCase;
    }

    public async Task<LineOfDutyCase> UpdateCaseAsync(LineOfDutyCase lodCase, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([lodCase.Id], ct);
        if (existing is null)
            throw new InvalidOperationException($"Case with Id {lodCase.Id} not found.");

        context.Entry(existing).CurrentValues.SetValues(lodCase);
        await context.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<LineOfDutyCase?> UpdateCaseAsync(string caseId, Action<LineOfDutyCase> applyChanges, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.CaseId == caseId, ct);
        if (existing is null)
            return null;

        applyChanges(existing);
        await context.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteCaseAsync(int id, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var lodCase = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (lodCase is null)
            return false;

        context.TimelineSteps.RemoveRange(lodCase.TimelineSteps);
        context.Authorities.RemoveRange(lodCase.Authorities);
        context.Documents.RemoveRange(lodCase.Documents);
        context.Appeals.RemoveRange(lodCase.Appeals);
        context.Cases.Remove(lodCase);
        await context.SaveChangesAsync(ct);
        return true;
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

    public async Task<LineOfDutyDocument?> GetDocumentByIdAsync(int documentId, CancellationToken ct = default)
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

    public async Task<byte[]?> GetDocumentContentAsync(int documentId, CancellationToken ct = default)
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
}
