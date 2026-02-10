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
    private bool isLoading = false;  // Start as false so the grid renders and LoadData fires

    protected override async Task OnInitializedAsync()
    {
        // LoadData will be called automatically by RadzenDataGrid after first render
    }

    private async Task LoadData(LoadDataArgs args)
    {
        isLoading = true;

        try
        {
            var skip = args.Skip ?? 0;
            var take = args.Top ?? 10;
            var filter = string.IsNullOrEmpty(args.Filter) ? null : args.Filter;
            var orderBy = string.IsNullOrEmpty(args.OrderBy) ? null : args.OrderBy;

            var result = await CaseService.GetCasesPagedAsync(skip, take, filter, orderBy);
            
            cases = result.Items;
            count = result.TotalCount;
        }
        catch (Exception ex)
        {
            // Handle error - in a real app, log this
            Console.WriteLine($"Error loading cases: {ex.Message}");
            cases = new List<LineOfDutyCase>();
            count = 0;
        }
        finally
        {
            isLoading = false;
        }
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }
}
