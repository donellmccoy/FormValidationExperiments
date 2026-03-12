using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for Line of Duty API operations.
/// </summary>
public interface IDataService
{
    /// <summary>
    /// Queries LOD cases via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single LOD case by CaseId with all navigation properties.
    /// </summary>
    Task<LineOfDutyCase?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves (PUTs) a LOD case entity. Returns the saved entity.
    /// </summary>
    Task<LineOfDutyCase> SaveCaseAsync(LineOfDutyCase lodCase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically transitions a LOD case to a new workflow state and persists the
    /// associated history entries in a single server-side transaction.
    /// Returns the updated case and server-persisted history entries.
    /// </summary>
    Task<CaseTransitionResponse> TransitionCaseAsync(int caseId, CaseTransitionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches members by name, rank, unit, or service number.
    /// </summary>
    Task<List<Member>> SearchMembersAsync(string searchText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries cases bookmarked by the current user via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a bookmark for the given case.
    /// </summary>
    Task AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes any bookmark for the given case.
    /// </summary>
    Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the current user has bookmarked the given case.
    /// </summary>
    Task<bool> IsBookmarkedAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches all documents for the given case.
    /// </summary>
    Task<List<LineOfDutyDocument>> GetDocumentsAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries documents for a case via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<LineOfDutyDocument>> GetDocumentsAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries workflow state histories for a case via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<WorkflowStateHistory>> GetWorkflowStateHistoriesAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a document file to the given case via the Documents API.
    /// </summary>
    Task<LineOfDutyDocument> UploadDocumentAsync(int caseId, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from the given case via the Documents API.
    /// </summary>
    Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a workflow step history snapshot entry for a LOD case.
    /// </summary>
    Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends multiple workflow step history snapshot entries atomically for a LOD case.
    /// </summary>
    Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(IEnumerable<WorkflowStateHistory> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the filled AF Form 348 PDF for the specified case.
    /// </summary>
    Task<byte[]> GetForm348PdfAsync(int caseId, CancellationToken cancellationToken = default);
}
