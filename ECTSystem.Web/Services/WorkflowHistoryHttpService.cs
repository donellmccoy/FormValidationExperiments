using ECTSystem.Shared.Models;
using PanoramicData.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData HTTP service for workflow state history operations.
/// Maps to <c>WorkflowStateHistoriesController</c>.
/// </summary>
public class WorkflowHistoryHttpService : ODataServiceBase, IWorkflowHistoryService
{
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
