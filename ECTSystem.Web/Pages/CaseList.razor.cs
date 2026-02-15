using System.Text.RegularExpressions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace ECTSystem.Web.Pages;

public partial class CaseList : ComponentBase
{
    [Inject]
    private IDataService CaseService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    private ODataEnumerable<LineOfDutyCase> cases;

    private int count;

    private bool isLoading;

    protected override async Task OnInitializedAsync()
    {
        await LoadData(new LoadDataArgs { Skip = 0, Top = 10 });
    }

    private async Task LoadData(LoadDataArgs args)
    {
        isLoading = true;

        try
        {
            var result = await CaseService.GetCasesAsync(
                filter: args.Filter,
                top: args.Top,
                skip: args.Skip,
                orderby: args.OrderBy,
                count: true);

            cases = result.Value.AsODataEnumerable();
            count = result.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cases: {ex.Message}");
            cases = null;
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

    private void OnCreateCase()
    {
        Navigation.NavigateTo("/case/create");
    }
}
