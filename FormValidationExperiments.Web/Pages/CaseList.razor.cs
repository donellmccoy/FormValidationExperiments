using System.Text.RegularExpressions;
using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace FormValidationExperiments.Web.Pages;

public partial class CaseList : ComponentBase
{
    [Inject]
    private ILineOfDutyCaseService CaseService { get; set; }

    private RadzenDataGrid<LineOfDutyCase> grid;

    private List<LineOfDutyCase> cases = [];

    private int count;

    private bool isLoading;

    private bool initialLoadComplete;

    protected override async Task OnInitializedAsync()
    {
        isLoading = true;

        try
        {
            var result = await CaseService.GetCasesPagedAsync(0, 10, null, null);
            cases = result.Items;
            count = result.TotalCount;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cases: {ex.Message}");
            cases = [];
            count = 0;
        }
        finally
        {
            isLoading = false;
            initialLoadComplete = true;
        }
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
            cases = [];
            count = 0;
        }
        finally
        {
            isLoading = false;
            initialLoadComplete = true;
        }
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }
}
