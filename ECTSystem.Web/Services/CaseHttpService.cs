using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using ECTSystem.Shared.Models;
using PanoramicData.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData HTTP service for LOD case CRUD operations.
/// Maps to <c>CasesController</c>.
/// </summary>
public class CaseHttpService : ODataServiceBase, ICaseService
{
    /// <summary>
    /// Cached array of <see cref="LineOfDutyCase"/> properties that are scalar
    /// (primitive, string, enum, DateTime, DateTimeOffset, Guid, decimal).
    /// Used by <see cref="BuildScalarPatchBody"/> to produce a PATCH body
    /// that excludes navigation properties and collections.
    /// </summary>
    private static readonly PropertyInfo[] ScalarProperties = typeof(LineOfDutyCase)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p =>
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            return t.IsPrimitive || t == typeof(string) || t.IsEnum
                || t == typeof(DateTime) || t == typeof(DateTimeOffset)
                || t == typeof(Guid) || t == typeof(decimal);
        })
        .ToArray();

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
    public async Task<LineOfDutyCase?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default)
    {
        var query = Client.For<LineOfDutyCase>("Cases")
            .Filter($"CaseId eq '{caseId}'")
            .Top(1)
            .Expand("Documents,Authorities," +
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

        var patchContent = JsonContent.Create(
            new { WorkflowState = request.NewWorkflowState },
            options: ODataJsonOptions);

        var patchResponse = await HttpClient.PatchAsync($"odata/Cases({caseId})", patchContent, cancellationToken);
        patchResponse.EnsureSuccessStatusCode();

        var updatedCase = (await patchResponse.Content.ReadFromJsonAsync<LineOfDutyCase>(ODataJsonOptions, cancellationToken))!;

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

        var content = JsonContent.Create(new { IsCheckedOut = true }, options: ODataJsonOptions);
        var response = await HttpClient.PatchAsync($"odata/Cases({caseId})", content, cancellationToken);

        return response.IsSuccessStatusCode;
    }

    /// <inheritdoc />
    public async Task<bool> CheckInCaseAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var content = JsonContent.Create(new { IsCheckedOut = false }, options: ODataJsonOptions);
        var response = await HttpClient.PatchAsync($"odata/Cases({caseId})", content, cancellationToken);

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
