using System.Text.RegularExpressions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;

namespace ECTSystem.Web.Pages;

public partial class MyBookmarks : ComponentBase, IDisposable
{
    [Inject]
    private IBookmarkService CaseService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    [Inject]
    private DialogService DialogService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    private RadzenDataGrid<LineOfDutyCase> _grid;
    private RadzenTextBox _searchBox;
    private ODataEnumerable<LineOfDutyCase> _bookmarks;
    private IList<LineOfDutyCase> _selectedBookmarks = [];
    private int _count;
    private bool _isLoading;
    private string _searchText = string.Empty;
    private LoadDataArgs _lastArgs;
    private CancellationTokenSource _loadCts = new();
    private CancellationTokenSource _searchCts = new();
    private bool _searchBoxFocused;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_searchBoxFocused && _searchBox is not null && _bookmarks is not null)
        {
            _searchBoxFocused = true;
            await _searchBox.Element.FocusAsync();
        }
    }

    private async Task LoadData(LoadDataArgs args)
    {
        // Cancel any previous in-flight request
        await _loadCts.CancelAsync();
        _loadCts.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        _lastArgs = args;
        _isLoading = true;

        try
        {
            var filter = CombineFilters(args.Filter, BuildSearchFilter(_searchText));

            var result = await CaseService.GetBookmarkedCasesAsync(
                filter: filter,
                top: args.Top,
                skip: args.Skip,
                orderby: args.OrderBy,
                count: true,
                cancellationToken: ct);

            _bookmarks = result.Value.AsODataEnumerable();
            _count = result.Count;

            var firstItem = _bookmarks.FirstOrDefault();
            if (firstItem != null && !_selectedBookmarks.Any(b => b.Id == firstItem.Id))
            {
                _selectedBookmarks = [firstItem];
            }
            else if (firstItem == null)
            {
                _selectedBookmarks = [];
            }
        }
        catch (OperationCanceledException)
        {
            // Request was superseded by a newer one or component disposed — ignore
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading bookmarks: {ex}");
            _bookmarks = null;
            _count = 0;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task OnRemoveBookmark(LineOfDutyCase lodCase)
    {
        var confirmed = await DialogService.Confirm(
            $"Remove bookmark for case {lodCase.CaseId}?",
            "Remove Bookmark",
            new ConfirmOptions { OkButtonText = "Remove", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        try
        {
            await CaseService.RemoveBookmarkAsync(lodCase.Id);
            BookmarkCountService.Decrement();
            await LoadData(_lastArgs ?? new LoadDataArgs { Skip = 0, Top = 10 });

            StateHasChanged();
            NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing bookmark: {ex}");
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to remove bookmark. Please try again.");
        }
    }

    private void OnCreateCase()
    {
        Navigation.NavigateTo("/case/new?from=bookmarks");
    }

    private async Task OnSearchInput(ChangeEventArgs args)
    {
        _searchText = args.Value?.ToString() ?? string.Empty;

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

    public void Dispose()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
    }
}
