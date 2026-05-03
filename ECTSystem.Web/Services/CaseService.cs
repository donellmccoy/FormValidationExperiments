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

public class CaseService(
    EctODataContext context,
    HttpClient httpClient,
    ILogger<CaseService> logger,
    [Microsoft.Extensions.DependencyInjection.FromKeyedServices(ECTSystem.Web.Extensions.ServiceCollectionExtensions.ODataJsonOptionsKey)] System.Text.Json.JsonSerializerOptions jsonOptions)
    : ODataServiceBase(context, httpClient, logger, jsonOptions), ICaseService
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
        await EnsureSuccessOrThrowAsync(response, "POST odata/Cases/ByCurrentState", cancellationToken);

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

    /// <summary>
    /// Checks out a case by invoking the bound OData action via <see cref="DataServiceContext"/>
    /// instead of <see cref="HttpClient"/>. The action is registered in the EDM as
    /// <c>Cases({key})/Checkout</c> with an optional <c>RowVersion</c> body parameter.
    /// </summary>
    public async Task<LineOfDutyCase?> CheckOutCaseViaODataAsync(int caseId, byte[] rowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var result = await TryCheckoutAsync(caseId, rowVersion, cancellationToken);

        if (result.Case is not null)
        {
            return result.Case;
        }

        // Only retry if the failure was a concurrency conflict (HTTP 409). The server
        // returns 409 for both "Checkout conflict" (already checked out by another user)
        // and "Concurrency conflict" (stale RowVersion). Refresh the RowVersion and
        // retry once — but only if the case is not actually checked out by someone else.
        if (result.StatusCode != 409)
        {
            return null;
        }

        var (freshRowVersion, isCheckedOutByOther) = await TryGetFreshCheckoutStateAsync(caseId, cancellationToken);

        if (freshRowVersion is null || isCheckedOutByOther || freshRowVersion.SequenceEqual(rowVersion))
        {
            return null;
        }

        Logger.LogInformation(
            "Retrying checkout for case {CaseId} with refreshed RowVersion after stale-token 409.",
            caseId);

        var retry = await TryCheckoutAsync(caseId, freshRowVersion, cancellationToken);
        return retry.Case;
    }

    private async Task<(LineOfDutyCase? Case, int? StatusCode)> TryCheckoutAsync(int caseId, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var actionUri = new Uri(Context.BaseUri, $"Cases({caseId})/Checkout");
        var parameters = new[] { new BodyOperationParameter("RowVersion", rowVersion) };

        try
        {
            // Checkout returns Ok(existing) on the server, so request the entity back.
            var response = await Context.ExecuteAsync<LineOfDutyCase>(
                actionUri, "POST", singleResult: true, parameters)
                .WaitAsync(cancellationToken);

            return (response.SingleOrDefault(), null);
        }
        catch (DataServiceClientException ex)
        {
            Logger.LogWarning(ex, "Checkout (OData client) failed for case {CaseId}: status={Status}", caseId, ex.StatusCode);
            return (null, ex.StatusCode);
        }
        catch (DataServiceRequestException ex)
        {
            Logger.LogWarning(ex, "Checkout (OData client) request failed for case {CaseId}", caseId);
            return (null, null);
        }
        catch (DataServiceQueryException ex)
        {
            Logger.LogWarning(ex, "Checkout (OData client) query failed for case {CaseId}: status={Status}", caseId, ex.Response?.StatusCode);
            return (null, ex.Response?.StatusCode);
        }
    }

    private async Task<(byte[]? RowVersion, bool IsCheckedOutByOther)> TryGetFreshCheckoutStateAsync(int caseId, CancellationToken cancellationToken)
    {
        try
        {
            var uri = new Uri(
                Context.BaseUri,
                $"Cases({caseId})?$select=RowVersion,IsCheckedOut,CheckedOutBy");

            var response = await Context.ExecuteAsync<LineOfDutyCase>(uri, "GET", singleResult: true)
                .WaitAsync(cancellationToken);

            var fresh = response.SingleOrDefault();
            if (fresh is null)
            {
                return (null, false);
            }

            return (fresh.RowVersion, fresh.IsCheckedOut);
        }
        catch (Exception ex) when (ex is DataServiceClientException or DataServiceRequestException or DataServiceQueryException)
        {
            Logger.LogWarning(ex, "Failed to refresh checkout state for case {CaseId} after 409.", caseId);
            return (null, false);
        }
    }

    /// <summary>
    /// Checks in a case by invoking the bound OData action via <see cref="DataServiceContext"/>
    /// instead of <see cref="HttpClient"/>. The action is registered in the EDM as
    /// <c>Cases({key})/Checkin</c> with an optional <c>RowVersion</c> body parameter.
    /// </summary>
    /// <remarks>
    /// The Checkin controller returns <c>NoContent</c>, so the non-generic
    /// <see cref="DataServiceContext.ExecuteAsync(Uri, string, OperationParameter[])"/> overload is used.
    /// </remarks>
    public async Task<bool> CheckInCaseViaODataAsync(int caseId, byte[] rowVersion, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var (success, statusCode) = await TryCheckinAsync(caseId, rowVersion, cancellationToken);

        if (success)
        {
            return true;
        }

        // Mirror the checkout retry: on a 409 stale-RowVersion conflict, fetch the
        // current RowVersion and retry once. The Checkin controller only permits the
        // user who checked the case out (or an Admin); if the fresh fetch shows the
        // case is no longer checked out we cannot recover.
        if (statusCode != 409)
        {
            return false;
        }

        var (freshRowVersion, isCheckedOut) = await TryGetFreshCheckoutStateAsync(caseId, cancellationToken);

        if (freshRowVersion is null || !isCheckedOut || freshRowVersion.SequenceEqual(rowVersion))
        {
            return false;
        }

        Logger.LogInformation(
            "Retrying checkin for case {CaseId} with refreshed RowVersion after stale-token 409.",
            caseId);

        var (retrySuccess, _) = await TryCheckinAsync(caseId, freshRowVersion, cancellationToken);
        return retrySuccess;
    }

    private async Task<(bool Success, int? StatusCode)> TryCheckinAsync(int caseId, byte[] rowVersion, CancellationToken cancellationToken)
    {
        var actionUri = new Uri(Context.BaseUri, $"Cases({caseId})/Checkin");
        var parameters = new[] { new BodyOperationParameter("RowVersion", rowVersion) };

        try
        {
            _ = await Context.ExecuteAsync(actionUri, "POST", parameters)
                .WaitAsync(cancellationToken);

            return (true, null);
        }
        catch (DataServiceClientException ex)
        {
            Logger.LogWarning(ex, "Checkin (OData client) failed for case {CaseId}: status={Status}", caseId, ex.StatusCode);
            return (false, ex.StatusCode);
        }
        catch (DataServiceRequestException ex)
        {
            Logger.LogWarning(ex, "Checkin (OData client) request failed for case {CaseId}", caseId);
            return (false, null);
        }
        catch (DataServiceQueryException ex)
        {
            Logger.LogWarning(ex, "Checkin (OData client) query failed for case {CaseId}: status={Status}", caseId, ex.Response?.StatusCode);
            return (false, ex.Response?.StatusCode);
        }
    }
}
