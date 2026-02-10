using System.Net.Http.Json;
using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;

namespace FormValidationExperiments.Web.Services;

/// <summary>
/// HttpClient-based implementation that calls the Web API for LOD case operations.
/// </summary>
public class LineOfDutyCaseHttpService : ILineOfDutyCaseService
{
    private readonly HttpClient _http;

    public LineOfDutyCaseHttpService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<LineOfDutyCase>> GetAllCasesAsync()
    {
        return await _http.GetFromJsonAsync<List<LineOfDutyCase>>("api/cases")
               ?? new List<LineOfDutyCase>();
    }

    public async Task<PagedResult<LineOfDutyCase>> GetCasesPagedAsync(int skip, int take, string? filter = null, string? orderBy = null)
    {
        var queryParams = new List<string> { $"skip={skip}", $"take={take}" };
        
        if (!string.IsNullOrEmpty(filter))
            queryParams.Add($"filter={Uri.EscapeDataString(filter)}");
        
        if (!string.IsNullOrEmpty(orderBy))
            queryParams.Add($"orderBy={Uri.EscapeDataString(orderBy)}");

        var url = $"api/cases/paged?{string.Join("&", queryParams)}";
        
        return await _http.GetFromJsonAsync<PagedResult<LineOfDutyCase>>(url)
               ?? new PagedResult<LineOfDutyCase>();
    }

    public async Task<CaseViewModelsDto> GetCaseViewModelsAsync(string caseId)
    {
        return await _http.GetFromJsonAsync<CaseViewModelsDto>($"api/cases/{caseId}/viewmodels");
    }

    public async Task<CaseInfoModel> SaveCaseAsync(string caseId, CaseViewModelsDto dto)
    {
        var response = await _http.PutAsJsonAsync($"api/cases/{caseId}", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CaseInfoModel>();
    }
}
