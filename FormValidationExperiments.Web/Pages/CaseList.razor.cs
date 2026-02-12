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

    private List<LineOfDutyCase> cases = [];

    private int count;

    private bool isLoading;

    private async Task LoadData(LoadDataArgs args)
    {
        isLoading = true;

        try
        {
            var result = await CaseService.GetCasesPagedAsync(
                args.Skip ?? 0,
                args.Top ?? 10,
                args.Filter,
                args.OrderBy);

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
        }
    }

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }
}
