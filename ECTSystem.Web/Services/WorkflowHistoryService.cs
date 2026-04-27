using System.Net;
using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class WorkflowHistoryService : ODataServiceBase, IWorkflowHistoryService
{
    public WorkflowHistoryService(EctODataContext context, HttpClient httpClient, ILogger<WorkflowHistoryService> logger)
        : base(context, httpClient, logger) { }

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

        var dto = new CreateWorkflowStateHistoryDto
        {
            LineOfDutyCaseId = entry.LineOfDutyCaseId,
            WorkflowState = entry.WorkflowState,
            EnteredDate = entry.EnteredDate,
            ExitDate = entry.ExitDate
        };

        var response = await HttpClient.PostAsJsonAsync("odata/WorkflowStateHistory", dto, JsonOptions, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "POST odata/WorkflowStateHistory", cancellationToken);

        return (await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions, cancellationToken))!;
    }

    public async Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var savedEntries = new List<WorkflowStateHistory>(entries.Count);

        foreach (var entry in entries)
        {
            var dto = new CreateWorkflowStateHistoryDto
            {
                LineOfDutyCaseId = entry.LineOfDutyCaseId,
                WorkflowState = entry.WorkflowState,
                EnteredDate = entry.EnteredDate,
                ExitDate = entry.ExitDate
            };

            var response = await HttpClient.PostAsJsonAsync("odata/WorkflowStateHistory", dto, JsonOptions, cancellationToken);
            await EnsureSuccessOrThrowAsync(response, "POST odata/WorkflowStateHistory (batch)", cancellationToken);

            var saved = await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions, cancellationToken);
            savedEntries.Add(saved!);
        }

        return savedEntries;
    }

    public async Task<WorkflowStateHistory> UpdateHistoryEndDateAsync(int entryId, DateTime endDate, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);

        var patchBody = new { ExitDate = endDate };

        var request = new HttpRequestMessage(HttpMethod.Patch, $"odata/WorkflowStateHistory({entryId})")
        {
            Content = JsonContent.Create(patchBody, options: JsonOptions)
        };

        var response = await HttpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, $"PATCH odata/WorkflowStateHistory({entryId})", cancellationToken);

        // OData Updated() returns 204 No Content by default — no body to deserialize.
        if (response.StatusCode is HttpStatusCode.NoContent)
        {
            return null!;
        }

        return (await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions, cancellationToken))!;
    }
}
