using System.Net;
using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side OData service for reading and appending <see cref="WorkflowStateHistory"/>
/// entries scoped to a single <see cref="LineOfDutyCase"/>.
/// </summary>
/// <remarks>
/// <para>
/// All write paths follow the §2.7 N1 contract: the client never sends <c>EnteredDate</c>
/// or <c>ExitDate</c> values. The server stamps both timestamps from its injected
/// <c>TimeProvider</c> so that history rows have monotonic, clock-skew-free UTC values.
/// <see cref="UpdateHistoryEndDateAsync"/> deliberately sends a sentinel <c>ExitDate</c> in
/// the PATCH body purely so OData's <c>Delta&lt;T&gt;</c> sees the property as changed and
/// triggers the server-side close-out; the value itself is discarded.
/// </para>
/// <para>
/// <see cref="GetWorkflowStateHistoriesAsync"/> always wraps the caller-supplied
/// <c>$filter</c> in parentheses and ANDs it with a mandatory <c>LineOfDutyCaseId eq {id}</c>
/// predicate so a tenant boundary cannot be bypassed by a hostile filter expression.
/// </para>
/// </remarks>
public class WorkflowHistoryService : ODataServiceBase, IWorkflowHistoryService
{
    public WorkflowHistoryService(
        EctODataContext context,
        HttpClient httpClient,
        ILogger<WorkflowHistoryService> logger,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices(ECTSystem.Web.Extensions.ServiceCollectionExtensions.ODataJsonOptionsKey)] System.Text.Json.JsonSerializerOptions jsonOptions)
        : base(context, httpClient, logger, jsonOptions) { }

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

        // EnteredDate / ExitDate are server-stamped via TimeProvider
        // (§2.7 N1) — we deliberately do not send any timestamps here.
        var dto = new CreateWorkflowStateHistoryDto
        {
            LineOfDutyCaseId = entry.LineOfDutyCaseId,
            WorkflowState = entry.WorkflowState
        };

        var response = await HttpClient.PostAsJsonAsync("odata/WorkflowStateHistory", dto, JsonOptions, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "POST odata/WorkflowStateHistory", cancellationToken);

        return (await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions, cancellationToken))!;
    }

    /// <summary>
    /// Sequentially POSTs each entry to <c>odata/WorkflowStateHistory</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Known N+1:</strong> this method makes one HTTP round-trip per entry. The API
    /// already wires <c>DefaultODataBatchHandler</c> via <c>app.UseODataBatching()</c>, so a
    /// future refactor can collapse this to a single <c>$batch</c> request once a typed
    /// batch helper exists in <see cref="ODataServiceBase"/>. Today's caller writes only a
    /// handful of rows per case transition, so the round-trip cost is bounded; revisit if
    /// bulk imports start using this path.
    /// </para>
    /// </remarks>
    public async Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var savedEntries = new List<WorkflowStateHistory>(entries.Count);

        foreach (var entry in entries)
        {
            // EnteredDate / ExitDate are server-stamped via TimeProvider
            // (§2.7 N1) — we deliberately do not send any timestamps here.
            var dto = new CreateWorkflowStateHistoryDto
            {
                LineOfDutyCaseId = entry.LineOfDutyCaseId,
                WorkflowState = entry.WorkflowState
            };

            var response = await HttpClient.PostAsJsonAsync("odata/WorkflowStateHistory", dto, JsonOptions, cancellationToken);
            await EnsureSuccessOrThrowAsync(response, "POST odata/WorkflowStateHistory (batch)", cancellationToken);

            var saved = await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions, cancellationToken);
            savedEntries.Add(saved!);
        }

        return savedEntries;
    }

    public async Task<WorkflowStateHistory> UpdateHistoryEndDateAsync(int entryId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);

        // ExitDate is server-stamped via TimeProvider (§2.7 N1).
        // We must include the property name in the PATCH body so the server's
        // Delta<T> sees it as a changed property and triggers the close-out;
        // the value itself is discarded server-side.
        var patchBody = new { ExitDate = (DateTime?)DateTime.MinValue };

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
