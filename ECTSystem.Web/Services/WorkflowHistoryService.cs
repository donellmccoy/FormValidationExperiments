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
    /// Posts every entry in a single OData <c>$batch</c> round-trip via
    /// <see cref="ODataServiceBase.BatchPostJsonAsync{TRequest,TResponse}"/>.
    /// </summary>
    /// <remarks>
    /// Replaces the previous N+1 sequential POST loop. The server's
    /// <c>DefaultODataBatchHandler</c> (wired in
    /// <c>ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs</c>) executes each
    /// sub-request and returns them in order; results are returned to the caller in
    /// the same order as <paramref name="entries"/>. An empty input list short-circuits
    /// without any HTTP traffic.
    /// </remarks>
    public async Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return [];
        }

        // EnteredDate / ExitDate are server-stamped via TimeProvider (§2.7 N1) — we
        // deliberately do not send any timestamps here.
        var dtos = entries
            .Select(e => new CreateWorkflowStateHistoryDto
            {
                LineOfDutyCaseId = e.LineOfDutyCaseId,
                WorkflowState = e.WorkflowState
            })
            .ToList();

        return await BatchPostJsonAsync<CreateWorkflowStateHistoryDto, WorkflowStateHistory>(
            entitySetPath: "WorkflowStateHistory",
            bodies: dtos,
            operation: "POST odata/WorkflowStateHistory",
            ct: cancellationToken);
    }

    public async Task<WorkflowStateHistory> UpdateHistoryEndDateAsync(int entryId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);

        // ExitDate is server-stamped via TimeProvider (§2.7 N1).
        // We must include the property name in the PATCH body so the server's
        // Delta<T> sees it as a changed property and triggers the close-out;
        // the value itself is discarded server-side. The value MUST be a UTC
        // DateTime (Kind=Utc) so System.Text.Json emits a trailing 'Z' — the
        // ASP.NET Core OData input formatter rejects DateTime strings without
        // a timezone designator (Edm.DateTimeOffset requires offset/Z) and
        // returns 400 with "The input was not valid.".
        var patchBody = new { ExitDate = (DateTime?)DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc) };

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
