using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using PanoramicData.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData HTTP service for LOD case CRUD operations.
/// Implements <see cref="ICaseService"/> using the <c>Cases</c> OData entity set.
/// Uses a scalar-only PATCH strategy for updates: reflects <see cref="LineOfDutyCase"/>
/// properties at startup to identify primitives, strings, enums, and date types, then
/// sends only those properties in PATCH requests to avoid OData <c>Delta&lt;T&gt;</c>
/// binding failures when navigation properties are present.
/// </summary>
public class CaseHttpService : ODataServiceBase, ICaseService
{
    /// <summary>
    /// Cached array of <see cref="LineOfDutyCase"/> properties that are scalar
    /// (primitive, string, enum, DateTime, DateTimeOffset, Guid, decimal).
    /// Built once via reflection at class load time and reused for every PATCH request.
    /// Used by <see cref="BuildScalarPatchBody"/> to produce a PATCH body
    /// that excludes navigation properties and collections, preventing OData model
    /// validation errors.
    /// </summary>
    private static readonly PropertyInfo[] ScalarProperties = typeof(LineOfDutyCase)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p =>
        {
            if (!p.CanWrite) return false;
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            return t.IsPrimitive || t == typeof(string) || t.IsEnum
                || t == typeof(DateTime) || t == typeof(DateTimeOffset)
                || t == typeof(Guid) || t == typeof(decimal);
        })
        .ToArray();

    /// <summary>
    /// Initializes a new instance of the <see cref="CaseHttpService"/> class.
    /// </summary>
    /// <param name="client">The typed OData client for CRUD operations against the <c>Cases</c> entity set.</param>
    /// <param name="httpClient">The raw HTTP client used for custom REST endpoints (e.g., case transitions, check-out/check-in).</param>
    public CaseHttpService(ODataClient client, HttpClient httpClient)
        : base(client, httpClient) { }

    /// <inheritdoc />
    public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null,
        int? top = null,
        int? skip = null,
        string? orderby = null,
        string? select = null,
        bool? count = null,
        CancellationToken cancellationToken = default)
    {
        var query = Client.For<LineOfDutyCase>("Cases");

        if (!string.IsNullOrEmpty(filter))
        {
            query = query.Filter(filter);
        }

        if (top.HasValue)
        {
            query = query.Top(top.Value);
        }

        if (skip.HasValue)
        {
            query = query.Skip(skip.Value);
        }

        if (!string.IsNullOrEmpty(orderby))
        {
            query = query.OrderBy(orderby);
        }

        if (!string.IsNullOrEmpty(select))
        {
            query = query.Select(select);
        }

        if (count == true)
        {
            query = query.Count();
        }

        var response = await Client.GetAsync(query, cancellationToken);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = response.Value?.ToList() ?? [],
            Count = (int)(response.Count ?? 0)
        };
    }

    /// <inheritdoc />
    public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesByCurrentStateAsync(
        WorkflowState[]? includeStates = null,
        WorkflowState[]? excludeStates = null,
        string? filter = null,
        int? top = null,
        int? skip = null,
        string? orderby = null,
        bool? count = null,
        CancellationToken cancellationToken = default)
    {
        var includeCsv = includeStates is { Length: > 0 }
            ? string.Join(",", includeStates)
            : "";
        var excludeCsv = excludeStates is { Length: > 0 }
            ? string.Join(",", excludeStates)
            : "";

        var basePath = $"odata/Cases/ByCurrentState(includeStates='{includeCsv}',excludeStates='{excludeCsv}')";
        var url = BuildNavigationPropertyUrl(basePath, filter, top, skip, orderby, count);

        var httpResponse = await HttpClient.GetAsync(url, cancellationToken);
        httpResponse.EnsureSuccessStatusCode();

        var data = await httpResponse.Content.ReadFromJsonAsync<ODataCountResponse<LineOfDutyCase>>(ODataJsonOptions, cancellationToken);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = data?.Value ?? [],
            Count = data?.Count ?? 0
        };
    }

    /// <inheritdoc />
    public async Task<LineOfDutyCase?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default)
    {
        var query = Client.For<LineOfDutyCase>("Cases")
            .Filter($"CaseId eq '{caseId}'")
            .Top(1)
            .Expand("Authorities," +
                    "Appeals($expand=AppellateAuthority)," +
                    "Member,MEDCON,INCAP,Notifications,WorkflowStateHistories");

        var response = await Client.GetAsync(query, cancellationToken);

        return response.Value?.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<LineOfDutyCase> SaveCaseAsync(LineOfDutyCase lodCase, CancellationToken cancellationToken = default)
    {
        if (lodCase.Id == 0)
        {
            return await Client.CreateAsync("Cases", lodCase, null, cancellationToken) ?? lodCase;
        }
        else
        {
            var updated = await Client.UpdateAsync<LineOfDutyCase>("Cases", lodCase.Id, BuildScalarPatchBody(lodCase), null, cancellationToken);

            return updated ?? lodCase;
        }
    }

    /// <inheritdoc />
    public async Task<CaseTransitionResponse> TransitionCaseAsync(int caseId, CaseTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentNullException.ThrowIfNull(request);

        var savedEntries = new List<WorkflowStateHistory>(request.HistoryEntries.Count);

        foreach (var entry in request.HistoryEntries)
        {
            var postResponse = await HttpClient.PostAsJsonAsync(
                "odata/WorkflowStateHistories", entry, ODataJsonOptions, cancellationToken);
            postResponse.EnsureSuccessStatusCode();

            var saved = await postResponse.Content.ReadFromJsonAsync<WorkflowStateHistory>(ODataJsonOptions, cancellationToken);
            if (saved is not null)
            {
                savedEntries.Add(saved);
            }
        }

        // Re-fetch the case with expanded WorkflowStateHistories so
        // CurrentWorkflowState computes correctly from the latest history.
        var updatedCase = await GetCaseAsync(caseId.ToString(), cancellationToken);

        return new CaseTransitionResponse
        {
            Case = updatedCase,
            HistoryEntries = savedEntries
        };
    }

    /// <inheritdoc />
    public async Task<bool> CheckOutCaseAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var response = await HttpClient.PostAsync($"odata/Cases({caseId})/Checkout", null, cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<bool> CheckInCaseAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var response = await HttpClient.PostAsync($"odata/Cases({caseId})/Checkin", null, cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Builds a dictionary containing only scalar property values from the entity.
    /// This prevents navigation properties and collections from being serialized
    /// into the PATCH body, which would cause OData Delta&lt;T&gt; binding failures.
    /// </summary>
    private static Dictionary<string, object?> BuildScalarPatchBody(LineOfDutyCase lodCase)
    {
        var dict = new Dictionary<string, object?>(ScalarProperties.Length);

        foreach (var prop in ScalarProperties)
        {
            dict[prop.Name] = prop.GetValue(lodCase);
        }

        return dict;
    }
}
