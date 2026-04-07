using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace ECTSystem.Web.Pages;

public partial class CaseList : ComponentBase, IDisposable
{
    #region Injected Services

    [Inject]
    private ICaseService CaseService { get; set; }

    [Inject]
    private IBookmarkService BookmarkService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    [Inject]
    private ContextMenuService ContextMenuService { get; set; }

    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Inject]
    private DialogService DialogService { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; }

    [Inject]
    private ILogger<CaseList> Logger { get; set; }

    [Inject]
    private CurrentUserService CurrentUserService { get; set; }

    #endregion

    #region Fields & Constants

    private RadzenDataGrid<LineOfDutyCase> _grid;
    private RadzenTextBox _searchBox;
    private ODataEnumerable<LineOfDutyCase> cases;
    private IList<LineOfDutyCase> _selectedCases = [];
    private HashSet<int> bookmarkedCaseIds = [];
    private HashSet<int> animatingBookmarkIds = [];
    private int count;
    private bool isLoading;
    private string searchText = string.Empty;
    private LoadDataArgs _lastArgs;
    private CancellationTokenSource _loadCts = new();
    private CancellationTokenSource _searchCts = new();
    private bool _searchBoxFocused;

    private const string ListSelect = "Id,CaseId,ServiceNumber,MemberName,MemberRank,Unit,IncidentType,IncidentDate,ProcessType,IsCheckedOut,CheckedOutBy,CheckedOutByName";
    private string _currentUserId;

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        _currentUserId = await CurrentUserService.GetUserIdAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_searchBoxFocused && _searchBox is not null && cases is not null)
        {
            _searchBoxFocused = true;
            await _searchBox.Element.FocusAsync();
        }
    }

    #endregion

    #region Data Loading

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

            Logger.LogDebug("Loading cases — Top: {Top}, Skip: {Skip}, OrderBy: {OrderBy}, Filter: {Filter}",
                args.Top, args.Skip, args.OrderBy, filter);

            var result = await CaseService.GetCasesAsync(
                filter: filter,
                top: args.Top,
                skip: args.Skip,
                orderby: args.OrderBy,
                select: ListSelect,
                count: true,
                cancellationToken: ct);

            cases = result.Value.AsODataEnumerable();
            count = result.Count;

            Logger.LogDebug("Loaded {Count} cases (total: {Total})", cases.Count(), count);

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
            Logger.LogDebug("LoadData cancelled — superseded by newer request");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load cases");
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

        var caseIds = cases.Select(c => c.Id).ToArray();
        bookmarkedCaseIds = await BookmarkService.GetBookmarkedCaseIdsAsync(caseIds, ct);

        Logger.LogDebug("Loaded {BookmarkCount} bookmarks for {CaseCount} cases", bookmarkedCaseIds.Count, caseIds.Length);
    }

    #endregion

    #region Bookmarks

    private async Task ToggleBookmark(LineOfDutyCase lodCase)
    {
        var isBookmarked = bookmarkedCaseIds.Contains(lodCase.Id);

        if (isBookmarked)
        {
            var confirmed = await DialogService.Confirm(
                $"Remove bookmark for case {lodCase.CaseId}?",
                "Remove Bookmark",
                new ConfirmOptions { OkButtonText = "Remove", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await BookmarkService.RemoveBookmarkAsync(lodCase.Id);
            bookmarkedCaseIds.Remove(lodCase.Id);
            Logger.LogInformation("Bookmark removed for case {CaseId}", lodCase.CaseId);
            NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);
            BookmarkCountService.Decrement();
        }
        else
        {
            animatingBookmarkIds.Add(lodCase.Id);
            StateHasChanged();

            await BookmarkService.AddBookmarkAsync(lodCase.Id);
            bookmarkedCaseIds.Add(lodCase.Id);
            Logger.LogInformation("Bookmark added for case {CaseId}", lodCase.CaseId);
            NotificationService.Notify(NotificationSeverity.Success, "Bookmark Added", $"Case {lodCase.CaseId} added to bookmarks.", closeOnClick: true);
            BookmarkCountService.Increment();

            await Task.Delay(800);
            animatingBookmarkIds.Remove(lodCase.Id);
        }
    }

    #endregion

    #region Search & Filtering

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

    #endregion

    #region Navigation & Actions

    private void OnCreateCase()
    {
        Logger.LogInformation("Navigating to create new case");
        Navigation.NavigateTo("/case/new?from=cases");
    }

    private async Task OnCaseClick(LineOfDutyCase lodCase)
    {
        if (lodCase.IsCheckedOut)
        {
            if (string.Equals(lodCase.CheckedOutBy, _currentUserId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Case {CaseId} already checked out by current user — opening in edit mode", lodCase.CaseId);
                Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=cases&mode=edit");
            }
            else
            {
                Logger.LogInformation("Case {CaseId} checked out by {CheckedOutBy} — opening read-only", lodCase.CaseId, lodCase.CheckedOutByName);
                Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=cases&mode=readonly");
            }
            return;
        }

        // Ask user whether to check out for editing, view read-only, or cancel
        var result = await DialogService.OpenAsync<Shared.CheckOutCaseDialog>(
            "Check Out Case",
            new Dictionary<string, object> { { "CaseId", lodCase.CaseId } },
            new DialogOptions { ShowClose = false, Width = "auto" });

        if (result is "checkout")
        {
            var success = await CaseService.CheckOutCaseAsync(lodCase.Id);

            if (success)
            {
                Logger.LogInformation("Checked out case {CaseId} for editing", lodCase.CaseId);
                Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=cases&mode=edit");
            }
            else
            {
                Logger.LogWarning("Checkout failed for case {CaseId} (Id: {Id}) — may be checked out by another user", lodCase.CaseId, lodCase.Id);
                NotificationService.Notify(NotificationSeverity.Error, 
                    "Checkout Failed", 
                    $"Could not check out Case {lodCase.CaseId}. It may have been checked out by another user.", 
                    closeOnClick: true);

                // Refresh the grid to get latest checkout state
                if (_lastArgs is not null)
                {
                    await LoadData(_lastArgs);
                }
            }
        }
        else if (result is "readonly")
        {
            Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=cases&mode=readonly");
        }
    }

    #endregion

    #region Context Menu

    private void OnCellContextMenu(DataGridCellMouseEventArgs<LineOfDutyCase> args)
    {
        _ = ShowContextMenuAsync(args);
    }

    private async Task ShowContextMenuAsync(DataGridCellMouseEventArgs<LineOfDutyCase> args)
    {
        var lodCase = args.Data;
        var isBookmarked = await BookmarkService.IsBookmarkedAsync(lodCase.Id);
        var isCheckedOutByMe = lodCase.IsCheckedOut
            && string.Equals(lodCase.CheckedOutBy, _currentUserId, StringComparison.OrdinalIgnoreCase);

        var items = new List<ContextMenuItem>
        {
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
        };

        if (isCheckedOutByMe)
        {
            items.Add(new ContextMenuItem
            {
                Text = "Check In",
                Icon = "lock_open",
                Value = "checkin"
            });
        }

        ContextMenuService.Open(args, items,
            async menuItem =>
            {
                ContextMenuService.Close();

                switch (menuItem.Value?.ToString())
                {
                    case "open":
                        await OnCaseClick(lodCase);
                        break;

                    case "bookmark":
                        await ToggleBookmark(lodCase);
                        break;

                    case "copy":
                        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", lodCase.CaseId);
                        NotificationService.Notify(NotificationSeverity.Info, "Copied", $"Case ID {lodCase.CaseId} copied to clipboard.", closeOnClick: true);
                        break;

                    case "checkin":
                        var success = await CaseService.CheckInCaseAsync(lodCase.Id);
                        if (success)
                        {
                            Logger.LogInformation("Checked in case {CaseId}", lodCase.CaseId);
                            NotificationService.Notify(NotificationSeverity.Success, "Checked In", $"Case {lodCase.CaseId} has been checked in.", closeOnClick: true);
                            if (_lastArgs is not null)
                            {
                                await LoadData(_lastArgs);
                            }
                        }
                        else
                        {
                            NotificationService.Notify(NotificationSeverity.Error, "Check In Failed", $"Could not check in Case {lodCase.CaseId}.", closeOnClick: true);
                        }
                        break;
                }
            });
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
    }

    #endregion
}
