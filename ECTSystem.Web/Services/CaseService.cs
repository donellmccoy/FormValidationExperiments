using System.Net.Http.Json;
using System.Text.Json;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class CaseService(EctODataContext context, HttpClient httpClient, ILogger<CaseService> logger) : ODataServiceBase(context, httpClient, logger), ICaseService
{
    private const string FullExpand = "Authorities,Appeals($expand=AppellateAuthority),Member,MEDCON,INCAP,Notifications,WorkflowStateHistories";

    public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null,
        int? top = null,
        int? skip = null,
        string? orderby = null,
        string? select = null,
        bool? count = null,
        string? expand = null,
        CancellationToken cancellationToken = default)
    {
        var query = Context.Cases;

        if (!string.IsNullOrEmpty(filter))
        {
            query = query.AddQueryOption("$filter", filter);
        }

        if (top.HasValue)
        {
            query = query.AddQueryOption("$top", top.Value);
        }

        if (skip.HasValue)
        {
            query = query.AddQueryOption("$skip", skip.Value);
        }

        if (!string.IsNullOrEmpty(orderby))
        {
            query = query.AddQueryOption("$orderby", orderby);
        }

        if (!string.IsNullOrEmpty(select))
        {
            query = query.AddQueryOption("$select", select);
        }

        if (!string.IsNullOrEmpty(expand))
        {
            query = query.AddQueryOption("$expand", expand);
        }

        if (count == true)
        {
            var (items, totalCount) = await ExecutePagedQueryAsync(query, cancellationToken);

            return new ODataServiceResult<LineOfDutyCase>
            {
                Value = items,
                Count = totalCount
            };
        }

        var results = await ExecuteQueryAsync(query, cancellationToken);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = results,
            Count = results.Count
        };
    }

    public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesByCurrentStateAsync(
        WorkflowState[]? includeStates = null,
        WorkflowState[]? excludeStates = null,
        string? filter = null,
        int? top = null,
        int? skip = null,
        string? orderby = null,
        string? select = null,
        bool? count = null,
        string? expand = null,
        CancellationToken cancellationToken = default)
    {
        var url = BuildNavigationPropertyUrl("odata/Cases/ByCurrentState", filter, top, skip, orderby, count, select, expand);

        var body = new
        {
            includeStates = includeStates ?? Array.Empty<WorkflowState>(),
            excludeStates = excludeStates ?? Array.Empty<WorkflowState>()
        };

        var response = await HttpClient.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ODataCountResponse<LineOfDutyCase>>(JsonOptions, cancellationToken);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = result?.Value ?? [],
            Count = result?.Count ?? 0
        };
    }

    public async Task<(LineOfDutyCase? Case, bool? IsBookmarked)> GetCaseAsync(string caseId, CancellationToken cancellationToken = default)
    {
        bool? isBookmarked = null;

        void OnReceivingResponse(object? sender, ReceivingResponseEventArgs args)
        {
            var headerValue = args.ResponseMessage?.GetHeader("X-Case-IsBookmarked");
            if (!string.IsNullOrEmpty(headerValue) && bool.TryParse(headerValue, out var val))
            {
                isBookmarked = val;
            }
        }

        Context.ReceivingResponse += OnReceivingResponse;

        try
        {
            var query = Context.Cases
                .AddQueryOption("$filter", $"CaseId eq '{caseId}'")
                .AddQueryOption("$top", 1)
                .AddQueryOption("$expand", FullExpand);

            var results = await ExecuteQueryAsync(query, cancellationToken);

            return (results.FirstOrDefault(), isBookmarked);
        }
        finally
        {
            Context.ReceivingResponse -= OnReceivingResponse;
        }
    }

    public async Task<LineOfDutyCase> SaveCaseAsync(LineOfDutyCase lodCase, CancellationToken cancellationToken = default)
    {
        if (lodCase.Id == 0)
        {
            var createDto = new CreateCaseDto
            {
                MemberId = lodCase.MemberId,
                ProcessType = lodCase.ProcessType,
                Component = lodCase.Component,
                MemberName = lodCase.MemberName,
                MemberRank = lodCase.MemberRank,
                ServiceNumber = lodCase.ServiceNumber,
                MemberDateOfBirth = lodCase.MemberDateOfBirth,
                Unit = lodCase.Unit,
                FromLine = lodCase.FromLine,
                IncidentType = lodCase.IncidentType,
                IncidentDate = lodCase.IncidentDate,
                IncidentDescription = lodCase.IncidentDescription,
                IncidentDutyStatus = lodCase.IncidentDutyStatus
            };

            var createResponse = await HttpClient.PostAsJsonAsync("odata/Cases", createDto, JsonOptions, cancellationToken);
            await EnsureSuccessOrThrowAsync(createResponse, "POST odata/Cases", cancellationToken);

            return (await createResponse.Content.ReadFromJsonAsync<LineOfDutyCase>(JsonOptions, cancellationToken))!;
        }

        // Capture navigation data before the PATCH — the slim response
        // only includes scalar fields (RowVersion, etc.), so we need to
        // restore navigation properties afterward.
        var documents = lodCase.Documents;
        var authorities = lodCase.Authorities;
        var appeals = lodCase.Appeals;
        var member = lodCase.Member;
        var medcon = lodCase.MEDCON;
        var incap = lodCase.INCAP;
        var notifications = lodCase.Notifications;
        var witnessStatements = lodCase.WitnessStatements;
        var auditComments = lodCase.AuditComments;
        var workflowStateHistories = lodCase.WorkflowStateHistories;

        // Detach from OData context if tracked.
        if (Context.GetEntityDescriptor(lodCase) != null)
        {
            Context.Detach(lodCase);
        }

        var updateDto = CaseDtoMapper.ToUpdateDto(lodCase);

        var request = new HttpRequestMessage(HttpMethod.Patch, $"odata/Cases({lodCase.Id})")
        {
            Content = JsonContent.Create(updateDto, options: JsonOptions)
        };

        if (lodCase.RowVersion is { Length: > 0 })
        {
            request.Headers.IfMatch.Add(
                new System.Net.Http.Headers.EntityTagHeaderValue(
                    $"\"{ Convert.ToBase64String(lodCase.RowVersion) }\""));
        }

        var response = await HttpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, $"PATCH odata/Cases({lodCase.Id})", cancellationToken);

        var updatedCase = (await response.Content.ReadFromJsonAsync<LineOfDutyCase>(JsonOptions, cancellationToken))!;

        // Restore captured navigation properties
        updatedCase.Documents = documents;
        updatedCase.Authorities = authorities;
        updatedCase.Appeals = appeals;
        updatedCase.Member = member;
        updatedCase.MEDCON = medcon;
        updatedCase.INCAP = incap;
        updatedCase.Notifications = notifications;
        updatedCase.WitnessStatements = witnessStatements;
        updatedCase.AuditComments = auditComments;
        updatedCase.WorkflowStateHistories = workflowStateHistories;

        return updatedCase;
    }

    public async Task<bool> CheckOutCaseAsync(int caseId, byte[] rowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        try
        {
            var response = await HttpClient.PostAsJsonAsync(
                $"odata/Cases({caseId})/Checkout",
                new { RowVersion = rowVersion },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
            Logger.LogWarning(
                "Checkout failed for case {CaseId}: status={StatusCode} title={ProblemTitle} detail={ProblemDetail}",
                caseId,
                (int)response.StatusCode,
                problem?.Title,
                problem?.Detail);

            return false;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogWarning(ex, "Checkout request failed for case {CaseId}", caseId);
            return false;
        }
    }

    public async Task<bool> CheckInCaseAsync(int caseId, byte[] rowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        try
        {
            var response = await HttpClient.PostAsJsonAsync(
                $"odata/Cases({caseId})/Checkin",
                new { RowVersion = rowVersion },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var problem = await TryReadProblemDetailsAsync(response, cancellationToken);
            Logger.LogWarning(
                "Checkin failed for case {CaseId}: status={StatusCode} title={ProblemTitle} detail={ProblemDetail}",
                caseId,
                (int)response.StatusCode,
                problem?.Title,
                problem?.Detail);

            return false;
        }
        catch (HttpRequestException ex)
        {
            Logger.LogWarning(ex, "Checkin request failed for case {CaseId}", caseId);
            return false;
        }
    }
}
