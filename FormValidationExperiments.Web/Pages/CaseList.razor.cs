using System.Text.RegularExpressions;
using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace FormValidationExperiments.Web.Pages;

public partial class CaseList : ComponentBase
{
    [Inject]
    private ILineOfDutyCaseService CaseService { get; set; }

    private List<LineOfDutyCase> cases = new();
    private int count;
    private bool isLoading = true;

    protected override async Task OnInitializedAsync()
    {
        // Initial load happens via LoadData event
        isLoading = false;
    }

    private async Task LoadData(LoadDataArgs args)
    {
        isLoading = true;

        var skip = args.Skip ?? 0;
        var take = args.Top ?? 10;
        var filter = args.Filter;
        var orderBy = args.OrderBy;

        var result = await CaseService.GetCasesPagedAsync(skip, take, filter, orderBy);
        
        cases = result.Items;
        count = result.TotalCount;

        isLoading = false;
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }
}
