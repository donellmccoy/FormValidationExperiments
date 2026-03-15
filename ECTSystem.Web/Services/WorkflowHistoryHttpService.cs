using ECTSystem.Shared.Models;
using PanoramicData.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData HTTP service for workflow state history operations.
/// Implements <see cref="IWorkflowHistoryService"/> using the <c>WorkflowStateHistories</c> OData entity set.
/// Supports querying history entries scoped to a single case (filters by <c>LineOfDutyCaseId</c>)
/// and creating new entries individually or in bulk. History entries record workflow state
/// transitions, actions, and status snapshots for the LOD determination audit trail.
/// </summary>
public class WorkflowHistoryHttpService : ODataServiceBase, IWorkflowHistoryService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowHistoryHttpService"/> class.
    /// </summary>
    /// <param name="client">The typed OData client for CRUD operations against the <c>WorkflowStateHistories</c> entity set.</param>
    /// <param name="httpClient">The raw HTTP client for any non-OData REST calls.</param>
    public WorkflowHistoryHttpService(ODataClient client, HttpClient httpClient)
        : base(client, httpClient) { }

    /// <inheritdoc />
    public async Task<ODataServiceResult<WorkflowStateHistory>> GetWorkflowStateHistoriesAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var caseFilter = $"LineOfDutyCaseId eq {caseId}";
        var combinedFilter = string.IsNullOrEmpty(filter) ? caseFilter : $"({caseFilter}) and ({filter})";

        var query = Client.For<WorkflowStateHistory>("WorkflowStateHistories")
            .Filter(combinedFilter);

        if (top.HasValue) query = query.Top(top.Value);
        if (skip.HasValue) query = query.Skip(skip.Value);
        if (!string.IsNullOrEmpty(orderby)) query = query.OrderBy(orderby);
        if (count == true) query = query.Count();

        var response = await Client.GetAsync(query, cancellationToken);

        return new ODataServiceResult<WorkflowStateHistory>
        {
            Value = response.Value?.ToList() ?? [],
            Count = (int)(response.Count ?? 0)
        };
    }

    /// <inheritdoc />
    public async Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var created = await Client.CreateAsync("WorkflowStateHistories", entry, null, cancellationToken);

        return created ?? entry;
    }

    /// <inheritdoc />
    public async Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var saved = new List<WorkflowStateHistory>(entries.Count);

        foreach (var entry in entries)
        {
            saved.Add(await AddHistoryEntryAsync(entry, cancellationToken));
        }

        return saved;
    }
}
