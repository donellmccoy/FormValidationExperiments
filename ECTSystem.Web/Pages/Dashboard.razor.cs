using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Radzen;
using ECTSystem.Shared.Enums;

namespace ECTSystem.Web.Pages;

public partial class Dashboard : ComponentBase
{
    [Inject]
    private IDataService CaseService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; }

    private bool isLoading = true;
    private string userName = "User";
    
    private int actionRequiredCount = 0;
    private int bookmarkedCount = 0;
    private int myActiveCasesCount = 0;
    private int completedCasesCount = 0;

    private IEnumerable<LineOfDutyCase> actionRequiredCases = [];
    private IEnumerable<LineOfDutyCase> bookmarkedCases = [];

    private int selectedTimeframe = 1;
    private string selectedNewOption = "New";
    private List<string> newOptions = ["New", "Import"];

    private class CasesOverTimeItem
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
    }
    private List<CasesOverTimeItem> casesOverTimeData = [];

    private class CasesByStatusItem
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
    private List<CasesByStatusItem> casesByStatusData = [];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            userName = user.Identity.Name ?? "User";
        }

        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        isLoading = true;
        try
        {
            // Load Bookmarked Cases
            var bookmarksResult = await CaseService.GetBookmarkedCasesAsync(top: 5, orderby: "Id desc", count: true);
            bookmarkedCases = bookmarksResult.Value;
            bookmarkedCount = bookmarksResult.Count;

            // Load Action Required Cases (Mocking this for now by fetching cases in specific states)
            // In a real app, this would filter by the user's role and the case's workflow state
            var actionRequiredResult = await CaseService.GetCasesAsync(
                filter: $"WorkflowState eq '{WorkflowState.UnitCommanderReview}' or WorkflowState eq '{WorkflowState.MedicalTechnicianReview}'", 
                top: 5, 
                orderby: "Id desc", 
                count: true);
            actionRequiredCases = actionRequiredResult.Value;
            actionRequiredCount = actionRequiredResult.Count;

            // Load My Active Cases (Mocking this by fetching cases where MemberName contains the user's name)
            // In a real app, this would filter by MemberId or ServiceNumber matching the logged-in user
            var myActiveCasesResult = await CaseService.GetCasesAsync(
                filter: $"contains(MemberName, '{userName}') and WorkflowState ne '{WorkflowState.Completed}' and WorkflowState ne '{WorkflowState.Closed}' and WorkflowState ne '{WorkflowState.Cancelled}'",
                top: 1,
                count: true);
            myActiveCasesCount = myActiveCasesResult.Count;

            var completedCasesResult = await CaseService.GetCasesAsync(
                filter: $"WorkflowState eq '{WorkflowState.Completed}'",
                top: 1,
                count: true);
            completedCasesCount = completedCasesResult.Count;

            // Mock chart data
            casesOverTimeData =
            [
                new() { Date = "Oct 18", Count = 2 },
                new() { Date = "Oct 25", Count = 5 },
                new() { Date = "Nov 01", Count = 3 },
                new() { Date = "Nov 08", Count = 8 },
                new() { Date = "Nov 15", Count = 6 },
                new() { Date = "Nov 22", Count = 10 }
            ];

            casesByStatusData =
            [
                new() { Status = "Unit CC Review", Count = 12 },
                new() { Status = "Med Tech Review", Count = 8 },
                new() { Status = "Legal Review", Count = 5 },
                new() { Status = "Completed", Count = 20 }
            ];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading dashboard data: {ex}");
        }
        finally
        {
            isLoading = false;
        }
    }

    private void OnCreateCase()
    {
        Navigation.NavigateTo("case/new");
    }

    private BadgeStyle GetBadgeStyle(WorkflowState state)
    {
        return state switch
        {
            WorkflowState.Completed => BadgeStyle.Success,
            WorkflowState.Closed => BadgeStyle.Light,
            WorkflowState.Cancelled => BadgeStyle.Danger,
            WorkflowState.MemberInformationEntry => BadgeStyle.Info,
            _ => BadgeStyle.Warning
        };
    }
}