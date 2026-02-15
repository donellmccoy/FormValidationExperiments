using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FormValidationExperiments.Shared.Mapping;
using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;
using Radzen;

#nullable enable

namespace FormValidationExperiments.Web.Services;

/// <summary>
/// OData-based service for all LOD case operations.
/// Uses OData endpoints exclusively; view model mapping is performed client-side
/// via <see cref="LineOfDutyCaseMapper"/>.
/// </summary>
public class LineOfDutyCaseODataService : ILineOfDutyCaseService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Uri _baseUri;

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

    public async Task<CaseViewModelsDto?> GetCaseViewModelsAsync(string caseId, CancellationToken cancellationToken = default)
    {
        // Fetch raw entity from OData, filtering by business key CaseId
        var uri = new Uri(_baseUri, "Cases");
        uri = uri.GetODataUri(
            filter: $"CaseId eq '{caseId}'",
            top: 1,
            expand: "Documents,Authorities,TimelineSteps($expand=ResponsibleAuthority),Appeals($expand=AppellateAuthority),MEDCON,INCAP");

        var response = await _http.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var odata = await response.Content
            .ReadFromJsonAsync<ODataResponse<LineOfDutyCase>>(_jsonOptions, cancellationToken);

        var lodCase = odata?.Value?.FirstOrDefault();
        if (lodCase is null)
            return null;

        // Map to view models client-side
        return LineOfDutyCaseMapper.ToCaseViewModelsDto(lodCase);
    }

    public async Task<CaseInfoModel> SaveCaseAsync(string caseId, CaseViewModelsDto dto, CancellationToken cancellationToken = default)
    {
        // Fetch the current entity from OData
        var uri = new Uri(_baseUri, "Cases");
        uri = uri.GetODataUri(
            filter: $"CaseId eq '{caseId}'",
            top: 1,
            expand: "Documents,Authorities,TimelineSteps($expand=ResponsibleAuthority),Appeals($expand=AppellateAuthority),MEDCON,INCAP");

        var fetchResponse = await _http.GetAsync(uri, cancellationToken);
        fetchResponse.EnsureSuccessStatusCode();

        var odata = await fetchResponse.Content
            .ReadFromJsonAsync<ODataResponse<LineOfDutyCase>>(_jsonOptions, cancellationToken);

        var lodCase = odata?.Value?.FirstOrDefault()
            ?? throw new InvalidOperationException($"Case '{caseId}' not found.");

        // Apply view model changes to the entity client-side
        LineOfDutyCaseMapper.ApplyAll(dto, lodCase);

        // PUT the updated entity back via OData
        var putUri = new Uri(_baseUri, $"Cases({lodCase.Id})");
        var saveResponse = await _http.PutAsJsonAsync(putUri, lodCase, _jsonOptions, cancellationToken);
        saveResponse.EnsureSuccessStatusCode();

        // Return refreshed CaseInfoModel
        return LineOfDutyCaseMapper.ToCaseInfoModel(lodCase);
    }
}
