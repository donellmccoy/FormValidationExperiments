using System.Net.Http.Json;
using System.Text.Json;
using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;

#nullable enable

namespace FormValidationExperiments.Web.Services;

/// <summary>
/// HttpClient-based implementation that calls the Web API for LOD case operations.
/// </summary>
public class LineOfDutyCaseHttpService : ILineOfDutyCaseService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _jsonOptions;

    public LineOfDutyCaseHttpService(HttpClient http, JsonSerializerOptions jsonOptions)
    {
        _http = http;
        _jsonOptions = jsonOptions;
    }

    public async Task<PagedResult<LineOfDutyCase>> GetCasesPagedAsync(int skip, int take, string? filter = null, string? orderBy = null)
    {
        var queryParams = new List<string> { $"skip={skip}", $"take={take}" };
        
        if (!string.IsNullOrEmpty(filter))
            queryParams.Add($"filter={Uri.EscapeDataString(filter)}");
        
        if (!string.IsNullOrEmpty(orderBy))
            queryParams.Add($"orderBy={Uri.EscapeDataString(orderBy)}");

        var url = $"api/cases/paged?{string.Join("&", queryParams)}";

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PagedResult<LineOfDutyCase>>(_jsonOptions)
               ?? new PagedResult<LineOfDutyCase>();
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
