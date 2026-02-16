using System.Text.Json;
using System.Text.Json.Serialization;
using ECTSystem.Shared.Models;
using PanoramicData.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData client service using PanoramicData.OData.Client (HttpClient-based).
/// Replaces the raw HttpClient approach and Microsoft.OData.Client (which is
/// incompatible with Blazor WASM due to APM-style stream reading).
/// PanoramicData.OData.Client is fully async and built on HttpClient,
/// making it safe for the WASM browser sandbox.
/// </summary>
public class LineOfDutyCaseHttpService : IDataService
{
    private readonly ODataClient _client;

    /// <summary>
    /// Cached scalar property metadata for <see cref="LineOfDutyCase"/>.
    /// OData Delta&lt;T&gt; cannot bind navigation properties or collections,
    /// so PATCH requests must include only scalar (primitive, enum, DateTime, string) properties.
    /// </summary>
    private static readonly System.Reflection.PropertyInfo[] ScalarProperties =
        typeof(LineOfDutyCase).GetProperties()
            .Where(p =>
            {
                var type = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                return type.IsPrimitive
                    || type == typeof(string)
                    || type.IsEnum
                    || type == typeof(DateTime)
                    || type == typeof(DateTimeOffset)
                    || type == typeof(Guid)
                    || type == typeof(decimal);
            })
            .ToArray();

    public LineOfDutyCaseHttpService(ODataClient client)
    {
        _client = client;
    }

    public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null,
        int? top = null,
        int? skip = null,
        string? orderby = null,
        bool? count = null,
        CancellationToken cancellationToken = default)
    {
        var query = _client.For<LineOfDutyCase>("Cases");

        if (!string.IsNullOrEmpty(filter))
            query = query.Filter(filter);
        if (top.HasValue)
            query = query.Top(top.Value);
        if (skip.HasValue)
            query = query.Skip(skip.Value);
        if (!string.IsNullOrEmpty(orderby))
            query = query.OrderBy(orderby);
        if (count == true)
            query = query.Count();

        var response = await _client.GetAsync(query, cancellationToken);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = response.Value?.ToList() ?? [],
            Count = (int)(response.Count ?? 0)
        };
    }

    public async Task<LineOfDutyCase?> GetCaseAsync(
        string caseId,
        CancellationToken cancellationToken = default)
    {
        var query = _client.For<LineOfDutyCase>("Cases")
            .Filter($"CaseId eq '{caseId}'")
            .Top(1)
            .Expand("Documents,Authorities," +
                    "TimelineSteps($expand=ResponsibleAuthority)," +
                    "Appeals($expand=AppellateAuthority)," +
                    "Member,MEDCON,INCAP,Notifications");

        var response = await _client.GetAsync(query, cancellationToken);

        return response.Value?.FirstOrDefault();
    }

    public async Task<LineOfDutyCase> SaveCaseAsync(
        LineOfDutyCase lodCase,
        CancellationToken cancellationToken = default)
    {
        if (lodCase.Id == 0)
        {
            // POST — create new entity
            var created = await _client.CreateAsync("Cases", lodCase, null, cancellationToken);
            return created ?? lodCase;
        }
        else
        {
            // PATCH — send only scalar properties as a dictionary.
            // PanoramicData's UpdateAsync serializes the entity parameter into the
            // PATCH body. The API's Delta<LineOfDutyCase> cannot bind navigation
            // properties or collections (Member, MEDCON, Documents, etc.), so we
            // must strip them out. Sending a Dictionary<string, object?> produces
            // the same JSON as an anonymous object with only the desired fields.
            var patchBody = BuildScalarPatchBody(lodCase);
            var updated = await _client.UpdateAsync<LineOfDutyCase>(
                "Cases", lodCase.Id, patchBody, null, cancellationToken);
            return updated ?? lodCase;
        }
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
