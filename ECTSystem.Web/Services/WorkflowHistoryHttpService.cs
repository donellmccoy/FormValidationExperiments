using System.Net.Http.Json;
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

        var url = BuildNavigationPropertyUrl($"odata/Cases({caseId})/WorkflowStateHistories", filter, top, skip, orderby, count);
        var response = await HttpClient.GetFromJsonAsync<ODataCountResponse<WorkflowStateHistory>>(url, ODataJsonOptions, cancellationToken);

        return new ODataServiceResult<WorkflowStateHistory>
        {
            Value = response?.Value?.ToList() ?? [],
            Count = response?.Count ?? 0
        };
    }

    /// <inheritdoc />
    public async Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var response = await HttpClient.PostAsJsonAsync("odata/WorkflowStateHistories", entry, ODataJsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(ODataJsonOptions, cancellationToken))!;
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
