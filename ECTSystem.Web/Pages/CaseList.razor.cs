using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
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

    [Inject]
    private TooltipService _tooltipService { get; set; }

    #endregion

    #region Fields & Constants

    private RadzenDataGrid<CaseListItemViewModel> _grid;
    private RadzenTextBox _searchBox;
    private IEnumerable<CaseListItemViewModel> cases = [];
    private IList<CaseListItemViewModel> _selectedCases = [];
    private HashSet<int> animatingBookmarkIds = [];
    private int count;
    private bool isLoading;
    private bool _initialLoadComplete;
    private string searchText = string.Empty;
    private LoadDataArgs _lastArgs;
    private CancellationTokenSource _loadCts = new();
    private CancellationTokenSource _searchCts = new();
    private bool _searchBoxFocused;

    private const string ListSelect = "Id,CaseId,ServiceNumber,MemberName,MemberRank,Unit,IncidentType,IncidentDate,ProcessType,IsCheckedOut,CheckedOutBy,CheckedOutByName";
    private const string ListExpand = "WorkflowStateHistories($select=Id,WorkflowState)";
    private string _currentUserId;

    private WorkflowState? _workflowStateFilter;
    private IncidentType? _incidentTypeFilter;
    private ProcessType? _processTypeFilter;

    private static readonly object[] _workflowStateFilters =
        Enum.GetValues<WorkflowState>()
            .Select(e => (object)new { Value = (WorkflowState?)e, Text = e.ToDisplayString() })
            .ToArray();

    private static readonly object[] _incidentTypeFilters =
        Enum.GetValues<IncidentType>()
            .Select(e => (object)new { Value = (IncidentType?)e, Text = e.ToDisplayString() })
            .ToArray();

    private static readonly object[] _processTypeFilters =
        Enum.GetValues<ProcessType>()
            .Select(e => (object)new { Value = (ProcessType?)e, Text = e.ToDisplayString() })
            .ToArray();

    #endregion
    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        _currentUserId = await CurrentUserService.GetUserIdAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _grid is not null)
        {
            await _grid.Reload();
        }

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
            var filter = BuildFilter(args.Filter);

            Logger.LogDebug("Loading cases — Top: {Top}, Skip: {Skip}, OrderBy: {OrderBy}, Filter: {Filter}", args.Top, args.Skip, args.OrderBy, filter);

            var result = await CaseService.GetCasesAsync(
                filter: filter,
                top: args.Top,
                skip: args.Skip,
                orderby: args.OrderBy,
                select: ListSelect,
                count: true,
                expand: ListExpand,
                cancellationToken: ct);

            cases = [.. result.Value.Select(item => LineOfDutyCaseMapper.ToCaseListItem(item, _currentUserId))];
            count = result.Count;

            var caseIds = cases.Select(c => c.Id).ToArray();
            if (caseIds.Length > 0)
            {
                var bookmarkMap = await BookmarkService.GetBookmarkedCaseIdsAsync(caseIds, ct);
                foreach (var c in cases)
                {
                    if (bookmarkMap.TryGetValue(c.Id, out var bookmarkId))
                    {
                        c.IsBookmarked = true;
                        c.BookmarkId = bookmarkId;
                    }
                }
            }

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

            _initialLoadComplete = true;
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
            _initialLoadComplete = true;
        }
        finally
        {
            isLoading = false;
        }
    }

    #endregion

    #region Bookmarks

    private async Task ToggleBookmark(CaseListItemViewModel lodCase)
    {
        if (lodCase.IsBookmarked)
        {
            var confirmed = await DialogService.Confirm(
                $"Remove bookmark for case {lodCase.CaseId}?",
                "Remove Bookmark",
                new ConfirmOptions { OkButtonText = "Remove", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await BookmarkService.DeleteBookmarkAsync(lodCase.Id, lodCase.BookmarkId!.Value);

            lodCase.IsBookmarked = false;
            lodCase.BookmarkId = null;

            Logger.LogInformation("Bookmark removed for case {CaseId}", lodCase.CaseId);

            NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);

            BookmarkCountService.Decrement();
        }
        else
        {
            animatingBookmarkIds.Add(lodCase.Id);

            StateHasChanged();

            lodCase.BookmarkId = await BookmarkService.AddBookmarkAsync(lodCase.Id);

            lodCase.IsBookmarked = true;

            Logger.LogInformation("Bookmark added for case {CaseId}", lodCase.CaseId);

            NotificationService.Notify(NotificationSeverity.Success, "Bookmark Added", $"Case {lodCase.CaseId} added to bookmarks.", closeOnClick: true);

            BookmarkCountService.Increment();

            await Task.Delay(800);

            animatingBookmarkIds.Remove(lodCase.Id);
        }
    }

    #endregion

    #region Search & Filtering

    private void ShowSearchTooltip(ElementReference args)
    {
        _tooltipService.Open(args,
            "Search across case number, member name, rank, SSN, unit, incident description, " +
            "clinical diagnosis, medical findings, commander review details, witness information, " +
            "SJA and board review fields, and signature blocks. Results match any field containing " +
            "your search text.",
            new TooltipOptions 
            { 
                Duration = null, 
                Position = TooltipPosition.Right, 
                Style = "max-width: 480px; white-space: normal; padding: 12px 16px; background: var(--rz-panel-background-color); color: var(--rz-text-color); border: 1px solid var(--rz-border-color); box-shadow: var(--rz-shadow-2);" 
             });
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
            await Task.Delay(900, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await _grid.FirstPage(true);
    }

    private string BuildFilter(string argsFilter)
    {
        var filters = new List<string>();

        if (_workflowStateFilter.HasValue)
        {
            filters.Add($"WorkflowStateHistories/any(h: h/WorkflowState eq '{_workflowStateFilter.Value}')");
        }

        if (_incidentTypeFilter.HasValue)
        {
            filters.Add($"IncidentType eq '{_incidentTypeFilter.Value}'");
        }

        if (_processTypeFilter.HasValue)
        {
            filters.Add($"ProcessType eq '{_processTypeFilter.Value}'");
        }

        var enumFilter = filters.Count > 0 ? string.Join(" and ", filters) : null;

        return CombineFilters(CombineFilters(argsFilter, enumFilter),BuildSearchFilter(searchText));
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

    private async Task OnCaseClick(CaseListItemViewModel lodCase)
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
            var success = await CaseService.CheckOutCaseAsync(lodCase.Id, lodCase.RowVersion);

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

    private void OnCellContextMenu(DataGridCellMouseEventArgs<CaseListItemViewModel> args)
    {
        _ = ShowContextMenuAsync(args);
    }

    private async Task ShowContextMenuAsync(DataGridCellMouseEventArgs<CaseListItemViewModel> args)
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
                        var success = await CaseService.CheckInCaseAsync(lodCase.Id, lodCase.RowVersion);
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
