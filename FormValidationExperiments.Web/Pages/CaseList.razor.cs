using System.Text.RegularExpressions;
using FormValidationExperiments.Web.Models;
using FormValidationExperiments.Web.Services;
using Microsoft.AspNetCore.Components;

namespace FormValidationExperiments.Web.Pages;

public partial class CaseList : ComponentBase
{
    [Inject]
    private ILineOfDutyCaseService CaseService { get; set; }

    private List<LineOfDutyCase> cases;
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        cases = await CaseService.GetAllCasesAsync();
        isLoading = false;
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }
}
