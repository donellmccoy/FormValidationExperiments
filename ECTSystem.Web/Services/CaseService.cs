using System.Net.Http.Json;
using System.Text.Json;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class CaseService(EctODataContext context, HttpClient httpClient) : ODataServiceBase(context, httpClient), ICaseService
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
            Context.AddObject("Cases", lodCase);
            await Context.SaveChangesAsync(cancellationToken);

            Context.Detach(lodCase);

            return lodCase;
        }

        // Capture navigation data before SaveChangesAsync — the slim PATCH
        // response only includes WorkflowStateHistories and scalar fields
        // (RowVersion, etc.), so the OData client may null out other nav
        // properties when it applies the response in-place.
        var documents = lodCase.Documents;
        var authorities = lodCase.Authorities;
        var appeals = lodCase.Appeals;
        var member = lodCase.Member;
        var medcon = lodCase.MEDCON;
        var incap = lodCase.INCAP;
        var notifications = lodCase.Notifications;
        var witnessStatements = lodCase.WitnessStatements;
        var auditComments = lodCase.AuditComments;

        if (Context.GetEntityDescriptor(lodCase) == null)
        {
            Context.AttachTo("Cases", lodCase);
        }
        Context.UpdateObject(lodCase);
        await Context.SaveChangesAsync(cancellationToken);

        Context.Detach(lodCase);

        // Restore captured navigation properties
        lodCase.Documents = documents;
        lodCase.Authorities = authorities;
        lodCase.Appeals = appeals;
        lodCase.Member = member;
        lodCase.MEDCON = medcon;
        lodCase.INCAP = incap;
        lodCase.Notifications = notifications;
        lodCase.WitnessStatements = witnessStatements;
        lodCase.AuditComments = auditComments;

        return lodCase;
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

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
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

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
