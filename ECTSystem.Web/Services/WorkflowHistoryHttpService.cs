using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class WorkflowHistoryHttpService : ODataServiceBase, IWorkflowHistoryService
{
    public WorkflowHistoryHttpService(EctODataContext context, HttpClient httpClient)
        : base(context, httpClient) { }

    public async Task<ODataServiceResult<WorkflowStateHistory>> GetWorkflowStateHistoriesAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var caseFilter = $"LineOfDutyCaseId eq {caseId}";
        var combinedFilter = string.IsNullOrEmpty(filter) ? caseFilter : $"({caseFilter}) and ({filter})";

        var query = Context.WorkflowStateHistories
            .AddQueryOption("$filter", combinedFilter);

        if (top.HasValue)
            query = query.AddQueryOption("$top", top.Value);

        if (skip.HasValue)
            query = query.AddQueryOption("$skip", skip.Value);

        if (!string.IsNullOrEmpty(orderby))
            query = query.AddQueryOption("$orderby", orderby);

        if (count == true)
        {
            var (items, totalCount) = await ExecutePagedQueryAsync(query, cancellationToken);

            return new ODataServiceResult<WorkflowStateHistory>
            {
                Value = items,
                Count = totalCount
            };
        }

        var results = await ExecuteQueryAsync(query, cancellationToken);

        return new ODataServiceResult<WorkflowStateHistory>
        {
            Value = results,
            Count = results.Count
        };
    }

    public async Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var dto = ToDto(entry);

        var response = await HttpClient.PostAsJsonAsync("odata/WorkflowStateHistories", dto, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize created workflow state history entry.");
    }

    public async Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var dtos = entries.Select(ToDto).ToList();

        var response = await HttpClient.PostAsJsonAsync("odata/WorkflowStateHistories/Batch", dtos, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<WorkflowStateHistory>>(JsonOptions, cancellationToken) ?? [];
    }

    private static CreateWorkflowStateHistoryDto ToDto(WorkflowStateHistory entry) => new()
    {
        LineOfDutyCaseId = entry.LineOfDutyCaseId,
        WorkflowState = entry.WorkflowState,
        Status = entry.Status,
        StartDate = entry.StartDate,
        EndDate = entry.EndDate
    };

    public async Task<WorkflowStateHistory> UpdateHistoryEndDateAsync(int entryId, DateTime endDate, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);

        var patch = new { EndDate = endDate };

        var response = await HttpClient.PatchAsJsonAsync(
            $"odata/WorkflowStateHistories({entryId})",
            patch,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("Server returned null after patching workflow state history entry.");
    }
}
