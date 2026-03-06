using System.Text.RegularExpressions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace ECTSystem.Web.Pages;

public partial class CaseList : ComponentBase, IDisposable
{
    [Inject]
    private IDataService CaseService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    [Inject]
    private ContextMenuService ContextMenuService { get; set; }

    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; }

    private RadzenDataGrid<LineOfDutyCase> _grid;
    private ODataEnumerable<LineOfDutyCase> cases;
    private IList<LineOfDutyCase> _selectedCases = [];
    private HashSet<int> bookmarkedCaseIds = [];
    private int count;
    private bool isLoading;
    private string searchText = string.Empty;
    private LoadDataArgs _lastArgs;
    private CancellationTokenSource _loadCts = new();
    private CancellationTokenSource _searchCts = new();

    private async Task LoadData(LoadDataArgs args)
    {
        _lastArgs = args;

        // Cancel any previous in-flight request
        await _loadCts.CancelAsync();
        _loadCts.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        isLoading = true;

        try
        {
            var filter = CombineFilters(args.Filter, BuildSearchFilter(searchText));
            Console.WriteLine($"[CaseList] Filter: {filter ?? "(none)"}");

            var result = await CaseService.GetCasesAsync(
                filter: filter,
                top: args.Top,
                skip: args.Skip,
                orderby: args.OrderBy,
                count: true,
                cancellationToken: ct);

            cases = result.Value.AsODataEnumerable();
            count = result.Count;

            var firstItem = cases.FirstOrDefault();
            if (firstItem != null && !_selectedCases.Any(c => c.Id == firstItem.Id))
            {
                _selectedCases = [firstItem];
            }
            else if (firstItem == null)
            {
                _selectedCases = [];
            }

            await LoadBookmarkStates(ct);
        }
        catch (OperationCanceledException)
        {
            // Request was superseded by a newer one or component disposed — ignore
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading cases: {ex}");
            cases = null;
            count = 0;
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task LoadBookmarkStates(CancellationToken ct = default)
    {
        bookmarkedCaseIds.Clear();

        if (cases == null)
        {
            return;
        }

        foreach (var lodCase in cases)
        {
            ct.ThrowIfCancellationRequested();
            if (await CaseService.IsBookmarkedAsync(lodCase.Id, ct))
            {
                bookmarkedCaseIds.Add(lodCase.Id);
            }
        }
    }

    private async Task ToggleBookmark(LineOfDutyCase lodCase)
    {
        var isBookmarked = bookmarkedCaseIds.Contains(lodCase.Id);

        if (isBookmarked)
        {
            await CaseService.RemoveBookmarkAsync(lodCase.Id);
            bookmarkedCaseIds.Remove(lodCase.Id);
            NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);
        }
        else
        {
            await CaseService.AddBookmarkAsync(lodCase.Id);
            bookmarkedCaseIds.Add(lodCase.Id);
            NotificationService.Notify(NotificationSeverity.Success, "Bookmark Added", $"Case {lodCase.CaseId} added to bookmarks.", closeOnClick: true);
        }

        await BookmarkCountService.RefreshAsync();
    }

    private async Task OnSearchInput(ChangeEventArgs args)
    {
        searchText = args.Value?.ToString() ?? string.Empty;

        await _searchCts.CancelAsync();
        _searchCts.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(500, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await _grid.FirstPage(true);
    }

    private static string BuildSearchFilter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escaped = text.Replace("'", "''");
        string[] columns =
        [
            // Case / Member identification
            "CaseId", "MemberName", "MemberRank", "ServiceNumber", "Unit",
            "FromLine", "IncidentDescription", "PointOfContact",

            // Orders / Duty Period
            "MemberOrdersStartTime", "MemberOrdersEndTime",

            // Medical assessment
            "TreatmentFacilityName", "ClinicalDiagnosis", "MedicalFindings",
            "PsychiatricEvalResults", "OtherRelevantConditions", "OtherTestResults",
            "MedicalRecommendation",

            // Commander review
            "OtherSourcesDescription", "MisconductExplanation",
            "CommanderToLine", "CommanderFromLine",
            "AbsentWithoutLeaveDate1", "AbsentWithoutLeaveTime1",
            "AbsentWithoutLeaveDate2", "AbsentWithoutLeaveTime2",
            "WitnessNameAddress1", "WitnessNameAddress2", "WitnessNameAddress3",
            "WitnessNameAddress4", "WitnessNameAddress5",

            // Findings
            "ProximateCause", "PSCDocumentation",

            // Signatures / name-ranks
            "ProviderNameRank", "ProviderDate", "ProviderSignature",
            "CommanderNameRank", "CommanderDate", "CommanderSignature",
            "SjaNameRank", "SjaDate",
            "WingCcSignature",
            "AppointingAuthorityNameRank", "AppointingAuthorityDate", "AppointingAuthoritySignature",

            // Board review
            "MedicalReviewText", "MedicalReviewerNameRank", "MedicalReviewDate", "MedicalReviewerSignature",
            "LegalReviewText", "LegalReviewerNameRank", "LegalReviewDate", "LegalReviewerSignature",
            "LodBoardChairNameRank", "LodBoardChairDate", "LodBoardChairSignature",

            // Approving authority
            "ApprovingAuthorityNameRank", "ApprovingAuthorityDate", "ApprovingAuthoritySignature",

            // Special handling / evidence
            "SARCCoordination", "ToxicologyReport"
        ];
        return string.Join(" or ", columns.Select(c => $"contains({c},'{escaped}')"));
    }

    private static string CombineFilters(string columnFilter, string searchFilter)
    {
        var hasColumn = !string.IsNullOrEmpty(columnFilter);
        var hasSearch = !string.IsNullOrEmpty(searchFilter);

        return (hasColumn, hasSearch) switch
        {
            (true, true) => $"({columnFilter}) and ({searchFilter})",
            (true, false) => columnFilter,
            (false, true) => searchFilter,
            _ => null
        };
    }

    private void OnCreateCase()
    {
        Navigation.NavigateTo("/case/new?from=cases");
    }

    private void OnCellContextMenu(DataGridCellMouseEventArgs<LineOfDutyCase> args)
    {
        _ = ShowContextMenuAsync(args);
    }

    private async Task ShowContextMenuAsync(DataGridCellMouseEventArgs<LineOfDutyCase> args)
    {
        var lodCase = args.Data;
        var isBookmarked = await CaseService.IsBookmarkedAsync(lodCase.Id);

        ContextMenuService.Open(args,
            [
                new ContextMenuItem
                {
                    Text = "Open Case",
                    Icon = "open_in_new",
                    Value = "open"
                },
                new ContextMenuItem
                {
                    Text = isBookmarked ? "Remove Bookmark" : "Add Bookmark",
                    Icon = isBookmarked ? "bookmark_remove" : "bookmark_add",
                    Value = "bookmark"
                },
                new ContextMenuItem
                {
                    Text = "Copy Case ID",
                    Icon = "content_copy",
                    Value = "copy"
                }
            ],
            async menuItem =>
            {
                ContextMenuService.Close();

                switch (menuItem.Value?.ToString())
                {
                    case "open":
                        Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=cases");
                        break;

                    case "bookmark":
                        await ToggleBookmark(lodCase);
                        break;

                    case "copy":
                        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", lodCase.CaseId);
                        NotificationService.Notify(NotificationSeverity.Info, "Copied", $"Case ID {lodCase.CaseId} copied to clipboard.", closeOnClick: true);
                        break;
                }
            });
    }

    public void Dispose()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
    }
}
