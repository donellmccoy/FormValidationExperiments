using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;
using Radzen;

#nullable enable

namespace FormValidationExperiments.Web.Services;

/// <summary>
/// OData-based service that queries LOD cases from the OData endpoint.
/// Uses Radzen's <see cref="ODataExtensions.GetODataUri"/> helper to build query URIs,
/// but deserializes responses with our configured JsonSerializerOptions (which include
/// <see cref="JsonStringEnumConverter"/>) because Radzen's built-in ReadAsync
/// doesn't support custom enum converters.
/// Non-OData operations (view models, save) still use standard REST endpoints.
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
        bool? count = null)
    {
        var uri = new Uri(_baseUri, "Cases");
        uri = uri.GetODataUri(filter: filter, top: top, skip: skip, orderby: orderby, count: count);

        var response = await _http.GetAsync(uri);

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

    public async Task<CaseViewModelsDto> GetCaseViewModelsAsync(string caseId)
    {
        var response = await _http.GetAsync($"api/cases/{caseId}/viewmodels");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CaseViewModelsDto>(_jsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize CaseViewModelsDto");
    }

    public async Task<CaseInfoModel> SaveCaseAsync(string caseId, CaseViewModelsDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/cases/{caseId}", dto, _jsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CaseInfoModel>(_jsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize CaseInfoModel");
    }
}
