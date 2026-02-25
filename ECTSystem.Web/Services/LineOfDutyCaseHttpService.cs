using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
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
    private readonly HttpClient _httpClient;

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

    public LineOfDutyCaseHttpService(ODataClient client, HttpClient httpClient)
    {
        _client = client;
        _httpClient = httpClient;
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

        if (count == true)
        {
            query = query.Count();
        }

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
                    "Member,MEDCON,INCAP,Notifications,WorkflowStepHistories");

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

    public async Task<List<Member>> SearchMembersAsync(
        string searchText,
        CancellationToken cancellationToken = default)
    {
        // OData string literals escape single quotes by doubling them ('').
        // Do NOT use Uri.EscapeDataString here — the outer Uri.EscapeDataString(filter)
        // call below handles URL encoding. Applying it here creates double-encoding
        // that causes OData to misinterpret %27 as a literal ' inside the string,
        // producing a malformed filter and a 400 for names like O'Brien.
        var literal = searchText.Replace("'", "''");
        var filter = $"contains(tolower(LastName),tolower('{literal}'))" +
                     $" or contains(tolower(FirstName),tolower('{literal}'))" +
                     $" or contains(tolower(Rank),tolower('{literal}'))" +
                     $" or contains(tolower(Unit),tolower('{literal}'))" +
                     $" or contains(tolower(ServiceNumber),tolower('{literal}'))";

        // Match rank titles/abbreviations to pay grade strings stored in DB
        var matchingPayGrades = RankToPayGrade
            .Where(r => r.Key.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Value)
            .Distinct()
            .ToList();

        if (matchingPayGrades.Count > 0)
        {
            var rankClauses = string.Join(" or ",
                matchingPayGrades.Select(pg => $"Rank eq '{pg}'"));
            filter += $" or {rankClauses}";
        }

        // Match Component enum values by raw name or display name (e.g. "Reserve" → AirForceReserve)
        var matchingComponents = Enum.GetValues<ServiceComponent>()
            .Where(c =>
            {
                var name = c.ToString();
                var display = Regex.Replace(name, @"(\B[A-Z])", " $1");
                return name.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                       || display.Contains(searchText, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        if (matchingComponents.Count > 0)
        {
            var componentClauses = string.Join(" or ",
                matchingComponents.Select(c => $"Component eq ECTSystem.Shared.Enums.ServiceComponent'{c}'"));
            filter += $" or {componentClauses}";
        }

        var url = $"odata/Members?$filter={Uri.EscapeDataString(filter)}&$top=25&$orderby=LastName,FirstName";
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        var response = await _httpClient.GetFromJsonAsync<ODataResponse<Member>>(url, options, cancellationToken);
        return response?.Value ?? [];
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

    // Maps rank titles and abbreviations to pay grade strings stored in the DB.
    // Multiple keys can map to the same pay grade (e.g., "Major" and "Maj" both → "O-4").
    private static readonly Dictionary<string, string> RankToPayGrade = new(StringComparer.OrdinalIgnoreCase)
    {
        // Enlisted
        ["Airman Basic"] = "E-1",      ["AB"] = "E-1",
        ["Airman"] = "E-2",            ["Amn"] = "E-2",
        ["Airman First Class"] = "E-3",["A1C"] = "E-3",
        ["Senior Airman"] = "E-4",     ["SrA"] = "E-4",
        ["Staff Sergeant"] = "E-5",    ["SSgt"] = "E-5",
        ["Technical Sergeant"] = "E-6",["TSgt"] = "E-6",
        ["Master Sergeant"] = "E-7",   ["MSgt"] = "E-7",
        ["Senior Master Sergeant"] = "E-8", ["SMSgt"] = "E-8",
        ["Chief Master Sergeant"] = "E-9",  ["CMSgt"] = "E-9",
        // Officer
        ["Second Lieutenant"] = "O-1", ["2d Lt"] = "O-1", ["SecondLt"] = "O-1",
        ["First Lieutenant"] = "O-2",  ["1st Lt"] = "O-2",["FirstLt"] = "O-2",
        ["Captain"] = "O-3",           ["Capt"] = "O-3",
        ["Major"] = "O-4",             ["Maj"] = "O-4",
        ["Lieutenant Colonel"] = "O-5",["Lt Col"] = "O-5",["LtCol"] = "O-5",
        ["Colonel"] = "O-6",           ["Col"] = "O-6",
        ["Brigadier General"] = "O-7", ["Brig Gen"] = "O-7", ["BrigGen"] = "O-7",
        ["Major General"] = "O-8",     ["Maj Gen"] = "O-8",  ["MajGen"] = "O-8",
        ["Lieutenant General"] = "O-9",["Lt Gen"] = "O-9",   ["LtGen"] = "O-9",
        ["General"] = "O-10",          ["Gen"] = "O-10",
    };

    private static readonly JsonSerializerOptions ODataJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ──────────────────────────── Bookmark Operations ────────────────────────────

    public async Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(filter))
        {
            parts.Add($"$filter={filter}");
        }

        if (top.HasValue)
        {
            parts.Add($"$top={top.Value}");
        }

        if (skip.HasValue)
        {
            parts.Add($"$skip={skip.Value}");
        }

        if (!string.IsNullOrEmpty(orderby))
        {
            parts.Add($"$orderby={orderby}");
        }

        if (count == true)
        {
            parts.Add("$count=true");
        }

        var url = parts.Count > 0
            ? $"odata/Cases/Bookmarked?{string.Join("&", parts)}"
            : "odata/Cases/Bookmarked";
        var response = await _httpClient.GetFromJsonAsync<ODataCountResponse<LineOfDutyCase>>(
            url, ODataJsonOptions, cancellationToken);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = response?.Value ?? [],
            Count = response?.Count ?? 0
        };
    }

    public async Task AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        var payload = new { LineOfDutyCaseId = caseId };
        var response = await _httpClient.PostAsJsonAsync("odata/CaseBookmarks", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"odata/CaseBookmarks/DeleteByCaseId?caseId={caseId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> IsBookmarkedAsync(int caseId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<IsBookmarkedResponse>(
            $"odata/CaseBookmarks/IsBookmarked(caseId={caseId})", ODataJsonOptions, cancellationToken);
        return response?.Value ?? false;
    }

    public async Task<LineOfDutyDocument> UploadDocumentAsync(int caseId, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(string.Empty), "documentType");
        form.Add(new StringContent(string.Empty), "description");

        var response = await _httpClient.PostAsync($"api/cases/{caseId}/documents", form, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LineOfDutyDocument>(ODataJsonOptions, cancellationToken))!;
    }

    public async Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/cases/{caseId}/documents/{documentId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TimelineStep> SignTimelineStepAsync(int stepId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"odata/TimelineSteps({stepId})/Sign", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TimelineStep>(ODataJsonOptions, cancellationToken))!;
    }

    public async Task<TimelineStep> StartTimelineStepAsync(int stepId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"odata/TimelineSteps({stepId})/Start", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TimelineStep>(ODataJsonOptions, cancellationToken))!;
    }

    public async Task<WorkflowStepHistory> AddHistoryEntryAsync(WorkflowStepHistory entry, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("odata/WorkflowStepHistories", entry, ODataJsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkflowStepHistory>(ODataJsonOptions, cancellationToken))!;
    }

    private class IsBookmarkedResponse
    {
        [JsonPropertyName("value")]
        public bool Value { get; set; }
    }

    private class ODataCountResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];

        [JsonPropertyName("@odata.count")]
        public int Count { get; set; }
    }

    private class ODataResponse<T>
    {
        [JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];
    }
}
