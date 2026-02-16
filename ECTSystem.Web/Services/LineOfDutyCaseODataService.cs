using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData-based service for all LOD case operations.
/// Uses OData endpoints exclusively; view model mapping is performed client-side
/// via <see cref="LineOfDutyCaseMapper"/>.
/// </summary>
public class LineOfDutyCaseODataService : IDataService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Uri _baseUri;

    /// <summary>
    /// Properties to exclude from PATCH serialization.
    /// Navigation/collection properties are managed via separate endpoints (e.g., SyncAuthorities).
    /// Key/FK properties are identified by the URL key — Delta&lt;T&gt;.Patch() would overwrite
    /// them if sent, so they are excluded here rather than filtering server-side.
    /// </summary>
    private static readonly HashSet<string> ExcludedPatchProperties = new(StringComparer.Ordinal)
    {
        // Key / foreign-key properties — identified by URL, not body
        nameof(LineOfDutyCase.Id),
        nameof(LineOfDutyCase.MemberId),
        nameof(LineOfDutyCase.MEDCONId),
        nameof(LineOfDutyCase.INCAPId),

        // Navigation / collection properties — managed via separate endpoints
        nameof(LineOfDutyCase.TimelineSteps),
        nameof(LineOfDutyCase.Authorities),
        nameof(LineOfDutyCase.Documents),
        nameof(LineOfDutyCase.WitnessStatements),
        nameof(LineOfDutyCase.Appeals),
        nameof(LineOfDutyCase.Notifications),
        nameof(LineOfDutyCase.Member),
        nameof(LineOfDutyCase.MEDCON),
        nameof(LineOfDutyCase.INCAP),
        nameof(LineOfDutyCase.AuditComments),
    };

    /// <summary>
    /// PascalCase JSON options used for OData PATCH requests.
    /// OData's Delta&lt;T&gt; deserializer matches property names against the EDM
    /// (which uses PascalCase), so the client must NOT send camelCase for PATCH bodies.
    /// A type modifier excludes navigation/collection properties from serialization
    /// so the PATCH body contains only scalar values — no separate DTO is needed.
    /// </summary>
    private static readonly JsonSerializerOptions PatchJsonOptions = new()
    {
        PropertyNamingPolicy = null, // PascalCase — matches EDM property names
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                static typeInfo =>
                {
                    if (typeInfo.Type != typeof(LineOfDutyCase))
                        return;

                    foreach (var prop in typeInfo.Properties)
                    {
                        if (ExcludedPatchProperties.Contains(prop.Name))
                        {
                            prop.ShouldSerialize = static (_, _) => false;
                        }
                    }
                }
            }
        }
    };

    public LineOfDutyCaseODataService(HttpClient http, JsonSerializerOptions jsonOptions)
    {
        _http = http;
        _jsonOptions = jsonOptions;
        _baseUri = new Uri(http.BaseAddress!, "odata/");
    }

    public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null,
        int? top = null,
        int? skip = null,
        string? orderby = null,
        bool? count = null,
        CancellationToken cancellationToken = default)
    {
        var uri = new Uri(_baseUri, "Cases");
        uri = uri.GetODataUri(filter: filter, top: top, skip: skip, orderby: orderby, count: count);

        var response = await _http.GetAsync(uri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"OData error ({response.StatusCode}): {errorBody}");
            response.EnsureSuccessStatusCode();
        }

        // Deserialize using our JsonSerializerOptions (with JsonStringEnumConverter).
        // Radzen's ReadAsync uses its own internal settings that can't deserialize string enums.
        var odata = await response.Content
            .ReadFromJsonAsync<ODataResponse<LineOfDutyCase>>(_jsonOptions);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = odata?.Value ?? [],
            Count = odata?.Count ?? 0
        };
    }

    /// <summary>
    /// DTO that matches the OData JSON response shape:
    /// <c>{ "@odata.context": "...", "@odata.count": 102, "value": [...] }</c>
    /// </summary>
    private sealed class ODataResponse<T>
    {
        [JsonPropertyName("@odata.context")]
        public string? Context { get; set; }

        [JsonPropertyName("@odata.count")]
        public int Count { get; set; }

        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];
    }

    public async Task<LineOfDutyCase?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default)
    {
        // Fetch raw entity from OData, filtering by business key CaseId
        var uri = new Uri(_baseUri, "Cases");
        uri = uri.GetODataUri(
            filter: $"CaseId eq '{caseId}'",
            top: 1,
            expand: "Documents,Authorities,TimelineSteps($expand=ResponsibleAuthority),Appeals($expand=AppellateAuthority),Member,MEDCON,INCAP,Notifications");

        var response = await _http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var odata = await response.Content
            .ReadFromJsonAsync<ODataResponse<LineOfDutyCase>>(_jsonOptions, cancellationToken);

        return odata?.Value?.FirstOrDefault();
    }

    public async Task<LineOfDutyCase> SaveCaseAsync(LineOfDutyCase lodCase, CancellationToken cancellationToken = default)
    {
        // Serialize the entity directly with PatchJsonOptions — the TypeInfoResolver
        // modifier excludes navigation/collection properties so only scalars are sent.
        // OData Delta<T> matches PascalCase property names against the EDM.
        var patchUri = new Uri(_baseUri, $"Cases({lodCase.Id})");
        var saveResponse = await _http.PatchAsJsonAsync(patchUri, lodCase, PatchJsonOptions, cancellationToken);

        if (!saveResponse.IsSuccessStatusCode)
        {
            var errorBody = await saveResponse.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"PATCH error ({saveResponse.StatusCode}): {errorBody}");
            saveResponse.EnsureSuccessStatusCode();
        }

        return lodCase;
    }
}
