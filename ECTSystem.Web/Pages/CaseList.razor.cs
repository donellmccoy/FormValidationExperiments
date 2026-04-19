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

/// <summary>
/// Code-behind for the <c>CaseList</c> page. Hosts a Radzen <see cref="RadzenDataGrid{TItem}"/>
/// of <see cref="LineOfDutyCase"/> records with server-side paging, sorting, filtering,
/// free-text search across many fields, bookmark toggling, context-menu actions, and
/// case check-out / check-in.
/// </summary>
/// <remarks>
/// Records are projected to <see cref="CaseListItemViewModel"/> via
/// <see cref="LineOfDutyCaseMapper"/>, with <c>Bookmarks</c> expanded and filtered to the
/// current user. Cancellation tokens guard against stale OData responses while the user
/// types or paginates rapidly.
/// </remarks>
public partial class CaseList : ComponentBase, IDisposable
{
    #region Injected Services

    /// <summary>
    /// OData client for case queries and check-in / check-out operations.
    /// </summary>
    [Inject]
    private ICaseService CaseService { get; set; }

    /// <summary>
    /// OData client for bookmark CRUD operations against the API.
    /// </summary>
    [Inject]
    private IBookmarkService BookmarkService { get; set; }

    /// <summary>
    /// Blazor navigation service used to route to case detail and creation pages.
    /// </summary>
    [Inject]
    private NavigationManager Navigation { get; set; }

    /// <summary>
    /// Radzen context-menu service used for the right-click row menu.
    /// </summary>
    [Inject]
    private ContextMenuService ContextMenuService { get; set; }

    /// <summary>
    /// Client-side service tracking the current user's bookmark count for the sidebar badge.
    /// </summary>
    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    /// <summary>
    /// Radzen notification service for transient toast messages.
    /// </summary>
    [Inject]
    private NotificationService NotificationService { get; set; }

    /// <summary>
    /// Radzen dialog service used for confirmations and the check-out dialog.
    /// </summary>
    [Inject]
    private DialogService DialogService { get; set; }

    /// <summary>
    /// JavaScript interop runtime used for clipboard and focus operations.
    /// </summary>
    [Inject]
    private IJSRuntime JSRuntime { get; set; }

    /// <summary>
    /// Logger scoped to this page.
    /// </summary>
    [Inject]
    private ILogger<CaseList> Logger { get; set; }

    /// <summary>
    /// Resolves the current authenticated user's identifier from the auth state.
    /// </summary>
    [Inject]
    private CurrentUserService CurrentUserService { get; set; }

    /// <summary>
    /// Radzen tooltip service used to show the search field tooltip.
    /// </summary>
    [Inject]
    private TooltipService _tooltipService { get; set; }

    #endregion

    #region Fields & Constants

    /// <summary>
    /// Reference to the Radzen data grid component, used to trigger reloads and paging.
    /// </summary>
    private RadzenDataGrid<CaseListItemViewModel> _grid;

    /// <summary>
    /// Reference to the search text box, used to focus the field after the initial render.
    /// </summary>
    private RadzenTextBox _searchBox;

    /// <summary>
    /// Current page of cases bound to the grid.
    /// </summary>
    private IEnumerable<CaseListItemViewModel> _cases = [];

    /// <summary>
    /// Currently selected rows in the grid (single-select; first row is auto-selected).
    /// </summary>
    private IList<CaseListItemViewModel> _selectedCases = [];

    /// <summary>
    /// Total record count returned by the server for the current filter; drives pager.
    /// </summary>
    private int _count;

    /// <summary>
    /// True while an OData request is in flight; bound to the grid's loading indicator.
    /// </summary>
    private bool _isLoading;

    /// <summary>
    /// Set to true after the first successful (or failed) load so the empty-state UI can render.
    /// </summary>
    private bool _initialLoadComplete;

    /// <summary>
    /// Current free-text search value; debounced before triggering a reload.
    /// </summary>
    private string searchText = string.Empty;

    /// <summary>
    /// Most recent <see cref="LoadDataArgs"/> received from the grid; used for manual reloads.
    /// </summary>
    private LoadDataArgs _lastArgs;

    /// <summary>
    /// Cancels superseded LoadData requests so older responses cannot overwrite newer ones.
    /// </summary>
    private CancellationTokenSource _loadCts = new();

    /// <summary>
    /// Cancels the search debounce timer when the user keeps typing.
    /// </summary>
    private CancellationTokenSource _searchCts = new();

    /// <summary>
    /// Tracks whether the search box has been focused once so we don't steal focus on every render.
    /// </summary>
    private bool _searchBoxFocused;

    /// <summary>
    /// Indicates a grid reload should be triggered on the next <see cref="OnAfterRenderAsync"/>.
    /// </summary>
    private bool _pendingReload;

    /// <summary>
    /// OData <c>$select</c> projection for the case list (only fields needed by the grid).
    /// </summary>
    private const string ListSelect = "Id,CaseId,ServiceNumber,MemberName,MemberRank,Unit,IncidentType,IncidentDate,ProcessType,IsCheckedOut,CheckedOutBy,CheckedOutByName,RowVersion";

    /// <summary>
    /// Identifier of the current authenticated user; populated in <see cref="OnInitializedAsync"/>.
    /// </summary>
    private string _currentUserId;

    /// <summary>
    /// OData <c>$expand</c> clause that pulls bookmark and workflow-state data scoped to the current user.
    /// Workflow histories are narrowed to the latest entry (matching <c>GetCurrentWorkflowState</c>).
    /// <c>UserId</c> is included in the Bookmarks projection because the client-side mapper
    /// (<c>LineOfDutyCaseMapper.ToCaseListItem</c>) filters by <c>b.UserId == userId</c>.
    /// </summary>
    private string ListExpand => $"WorkflowStateHistories($select=WorkflowState;$orderby=Id desc;$top=1),Bookmarks($filter=UserId eq '{_currentUserId}';$select=Id,UserId;$top=1)";

    /// <summary>
    /// Optional workflow-state filter applied via the toolbar dropdown.
    /// </summary>
    private WorkflowState? _workflowStateFilter;

    /// <summary>
    /// Optional incident-type filter applied via the toolbar dropdown.
    /// </summary>
    private IncidentType? _incidentTypeFilter;

    /// <summary>
    /// Optional process-type filter applied via the toolbar dropdown.
    /// </summary>
    private ProcessType? _processTypeFilter;

    /// <summary>
    /// Cached <c>{ Value, Text }</c> dropdown items for the workflow-state filter.
    /// </summary>
    private static readonly object[] _workflowStateFilters =
        Enum.GetValues<WorkflowState>()
            .Select(e => (object)new { Value = (WorkflowState?)e, Text = e.ToDisplayString() })
            .ToArray();

    /// <summary>
    /// Cached <c>{ Value, Text }</c> dropdown items for the incident-type filter.
    /// </summary>
    private static readonly object[] _incidentTypeFilters =
        Enum.GetValues<IncidentType>()
            .Select(e => (object)new { Value = (IncidentType?)e, Text = e.ToDisplayString() })
            .ToArray();

    /// <summary>
    /// Cached <c>{ Value, Text }</c> dropdown items for the process-type filter.
    /// </summary>
    private static readonly object[] _processTypeFilters =
        Enum.GetValues<ProcessType>()
            .Select(e => (object)new { Value = (ProcessType?)e, Text = e.ToDisplayString() })
            .ToArray();

    #endregion

    #region Lifecycle

    /// <summary>
    /// Resolves the current user's identifier and queues an initial grid reload.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        _currentUserId = await CurrentUserService.GetUserIdAsync();
        _pendingReload = true;
    }

    /// <summary>
    /// Performs the deferred initial grid reload (the grid reference is not available
    /// until after first render) and focuses the search box once.
    /// </summary>
    /// <param name="firstRender">True on the very first render of the component.</param>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_pendingReload && _grid is not null)
        {
            _pendingReload = false;
            await _grid.Reload();
        }

        if (!_searchBoxFocused && _searchBox is not null && _cases is not null)
        {
            _searchBoxFocused = true;
            await _searchBox.Element.FocusAsync();
        }
    }

    #endregion

    #region Data Loading

    /// <summary>
    /// Loads a page of cases from the server in response to a Radzen grid <c>LoadData</c>
    /// event. Prior in-flight requests are cancelled before issuing the new one.
    /// </summary>
    /// <param name="args">Paging, sorting, and filter arguments supplied by the grid.</param>
    private async Task LoadData(LoadDataArgs args)
    {
        _lastArgs = args;

        // Cancel any previous in-flight request
        await _loadCts.CancelAsync();
        _loadCts.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        _isLoading = true;

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

            _cases = [.. result.Value.Select(item => LineOfDutyCaseMapper.ToCaseListItem(item, _currentUserId))];
            _count = result.Count;

            Logger.LogDebug("Loaded {Count} cases (total: {Total})", _cases.Count(), _count);

            var firstItem = _cases.FirstOrDefault();

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
            _cases = null;
            _count = 0;
            _initialLoadComplete = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    #endregion

    #region Bookmarks

    /// <summary>
    /// Toggles the bookmark state of a case. Removing prompts for confirmation; adding
    /// triggers the bookmark-flash animation. Updates the sidebar bookmark badge count
    /// and notifies the user of success or failure.
    /// </summary>
    /// <param name="lodCase">The case row whose bookmark state should be toggled.</param>
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

            try
            {
                await BookmarkService.DeleteBookmarkAsync(lodCase.Id, lodCase.BookmarkId!.Value);

                lodCase.IsBookmarked = false;

                lodCase.BookmarkId = null;

                Logger.LogInformation("Bookmark removed for case {CaseId}", lodCase.CaseId);

                NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);

                BookmarkCountService.Decrement();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to remove bookmark for case {CaseId}", lodCase.CaseId);
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to remove bookmark. Please try again.", closeOnClick: true);
            }
        }
        else
        {
            lodCase.IsAnimating = true;

            StateHasChanged();

            try
            {
                lodCase.BookmarkId = await BookmarkService.AddBookmarkAsync(lodCase.Id);

                lodCase.IsBookmarked = true;

                Logger.LogInformation("Bookmark added for case {CaseId}", lodCase.CaseId);

                NotificationService.Notify(NotificationSeverity.Success, "Bookmark Added", $"Case {lodCase.CaseId} added to bookmarks.", closeOnClick: true);

                BookmarkCountService.Increment();

                await Task.Delay(800);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to add bookmark for case {CaseId}", lodCase.CaseId);
                NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to add bookmark. Please try again.", closeOnClick: true);
            }
            finally
            {
                lodCase.IsAnimating = false;
            }
        }
    }

    /// <summary>
    /// Returns the CSS class applied to the bookmark icon (the flash animation class
    /// while a bookmark is being added, otherwise empty).
    /// </summary>
    /// <param name="lodCase">The case row whose bookmark icon is being rendered.</param>
    /// <returns>The CSS class name for the icon element.</returns>
    private static string GetBookmarkIconClass(CaseListItemViewModel lodCase)
    {
        return lodCase.IsAnimating ? "bookmark-flash" : "";
    }

    /// <summary>
    /// Returns the Material Symbols icon name to display for the case's current
    /// bookmark state (animating, bookmarked, or not bookmarked).
    /// </summary>
    /// <param name="lodCase">The case row whose bookmark icon is being rendered.</param>
    /// <returns>The icon ligature name.</returns>
    private static string GetBookmarkIcon(CaseListItemViewModel lodCase)
    {
        return lodCase.IsAnimating ? "bookmark_added"
        : lodCase.IsBookmarked ? "bookmark_remove"
        : "bookmark_add";
    }

    #endregion

    #region Search & Filtering

    /// <summary>
    /// Opens the long-form tooltip describing the fields covered by the free-text search.
    /// </summary>
    /// <param name="args">The DOM element reference to anchor the tooltip on.</param>
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

    /// <summary>
    /// Handles search-box input with a 900&#160;ms debounce. The latest keystroke cancels
    /// the previous timer; once the timer elapses the grid is reset to the first page,
    /// which triggers <see cref="LoadData"/> with the new search term.
    /// </summary>
    /// <param name="args">The change event carrying the current text-box value.</param>
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

    /// <summary>
    /// Composes the OData <c>$filter</c> string from the grid filter, the toolbar
    /// dropdowns, and the free-text search across all relevant case fields.
    /// </summary>
    /// <param name="argsFilter">The filter clause supplied by the grid (column filters, etc.).</param>
    /// <returns>A combined OData filter expression, or <c>null</c> if no filters apply.</returns>
    private string BuildFilter(string argsFilter)
    {
        var filters = new List<string>();

        if (!string.IsNullOrEmpty(argsFilter))
        {
            filters.Add($"({argsFilter})");
        }

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

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var escaped = searchText.Replace("'", "''");
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
            filters.Add($"({string.Join(" or ", columns.Select(c => $"contains({c},'{escaped}')"))})");
        }

        return filters.Count > 0 ? string.Join(" and ", filters) : null;
    }

    #endregion

    #region Navigation & Actions

    /// <summary>
    /// Navigates to the new-case wizard, tagging the origin as the cases list page.
    /// </summary>
    private void OnCreateCase()
    {
        Logger.LogInformation("Navigating to create new case");
        Navigation.NavigateTo("/case/new?from=cases");
    }

    /// <summary>
    /// Handles a click on a case row. If the case is already checked out by the current
    /// user, it opens in edit mode; if checked out by someone else, it opens read-only;
    /// otherwise the user is prompted to check out, view read-only, or cancel.
    /// </summary>
    /// <param name="lodCase">The case row that was clicked.</param>
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

    /// <summary>
    /// Synchronous wrapper invoked from the grid's <c>CellContextMenu</c> event that
    /// fires the asynchronous menu builder without awaiting it.
    /// </summary>
    /// <param name="args">The cell mouse event from the grid.</param>
    private void OnCellContextMenu(DataGridCellMouseEventArgs<CaseListItemViewModel> args)
    {
        _ = ShowContextMenuAsync(args);
    }

    /// <summary>
    /// Builds and shows the right-click context menu for a row, with actions to open
    /// the case, toggle the bookmark, copy the case ID, and (when applicable) check
    /// the case back in.
    /// </summary>
    /// <param name="args">The cell mouse event identifying the row that was right-clicked.</param>
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

    /// <summary>
    /// Cancels any in-flight load and search debounce tasks and disposes their
    /// <see cref="CancellationTokenSource"/> instances when the component is removed.
    /// </summary>
    public void Dispose()
    {
        _loadCts.Cancel();
        _loadCts.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
    }

    #endregion
}
