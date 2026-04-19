using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace ECTSystem.Web.Pages;

public partial class MyBookmarks : ComponentBase, IDisposable
{
    [Inject]
    private IBookmarkService BookmarkService { get; set; }

    [Inject]
    private ICaseService CaseService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    [Inject]
    private DialogService DialogService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    [Inject]
    private CurrentUserService CurrentUserService { get; set; }

    [Inject]
    private ContextMenuService ContextMenuService { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; }

    [Inject]
    private TooltipService TooltipService { get; set; }

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
    private bool _initialLoadComplete;

    private const string ListSelect = "Id,CaseId,ServiceNumber,MemberName,MemberRank,Unit,IncidentType,IncidentDate,ProcessType,IsCheckedOut,CheckedOutBy,CheckedOutByName";
    private const string ListExpand = "WorkflowStateHistories($select=Id,WorkflowState)";
    private string _currentUserId;
    private Dictionary<int, int> _bookmarkIdMap = [];

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

    protected override async Task OnInitializedAsync()
    {
        _currentUserId = await CurrentUserService.GetUserIdAsync();
    }

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
            // Build enum filters manually — these columns use FilterTemplate with separate state variables
            var enumFilters = new List<string>();
            if (_incidentTypeFilter.HasValue)
                enumFilters.Add($"IncidentType eq '{_incidentTypeFilter.Value}'");
            if (_processTypeFilter.HasValue)
                enumFilters.Add($"ProcessType eq '{_processTypeFilter.Value}'");
            var enumFilter = enumFilters.Count > 0 ? string.Join(" and ", enumFilters) : null;

            var filter = CombineFilters(
                CombineFilters(args.Filter, enumFilter),
                BuildSearchFilter(_searchText));

            ODataServiceResult<LineOfDutyCase> result;

            if (_workflowStateFilter.HasValue)
            {
                result = await BookmarkService.GetBookmarkedCasesByCurrentStateAsync(
                    includeStates: [_workflowStateFilter.Value],
                    filter: filter,
                    top: args.Top,
                    skip: args.Skip,
                    orderby: args.OrderBy,
                    select: ListSelect,
                    count: true,
                    expand: ListExpand,
                    cancellationToken: ct);
            }
            else
            {
                result = await BookmarkService.GetBookmarkedCasesAsync(
                    filter: filter,
                    top: args.Top,
                    skip: args.Skip,
                    orderby: args.OrderBy,
                    select: ListSelect,
                    count: true,
                    expand: ListExpand,
                    cancellationToken: ct);
            }

            _bookmarks = result.Value.AsODataEnumerable();
            _count = result.Count;
            _initialLoadComplete = true;

            var caseIds = _bookmarks.Select(b => b.Id).ToArray();
            if (caseIds.Length > 0)
            {
                _bookmarkIdMap = await BookmarkService.GetBookmarkedCaseIdsAsync(caseIds, ct);
            }

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
        catch (Exception)
        {
            _bookmarks = Array.Empty<LineOfDutyCase>().AsODataEnumerable();
            _count = 0;
            _initialLoadComplete = true;
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
            var bookmarkId = _bookmarkIdMap[lodCase.Id];
            await BookmarkService.DeleteBookmarkAsync(lodCase.Id, bookmarkId);
            _bookmarkIdMap.Remove(lodCase.Id);
            BookmarkCountService.Decrement();
            await _grid.Reload();

            NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);
        }
        catch (Exception)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Error", "Failed to remove bookmark. Please try again.");
        }
    }

    private async Task OnCaseClick(LineOfDutyCase lodCase)
    {
        if (lodCase.IsCheckedOut)
        {
            if (string.Equals(lodCase.CheckedOutBy, _currentUserId, StringComparison.OrdinalIgnoreCase))
            {
                Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=bookmarks&mode=edit");
            }
            else
            {
                Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=bookmarks&mode=readonly");
            }
            return;
        }

        var result = await DialogService.OpenAsync<Shared.CheckOutCaseDialog>(
            "Check Out Case",
            new Dictionary<string, object> { { "CaseId", lodCase.CaseId } },
            new DialogOptions { ShowClose = false, Width = "auto" });

        if (result is "checkout")
        {
            var success = await CaseService.CheckOutCaseAsync(lodCase.Id, lodCase.RowVersion);

            if (success)
            {
                Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=bookmarks&mode=edit");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Checkout Failed", $"Could not check out Case {lodCase.CaseId}. It may have been checked out by another user.", closeOnClick: true);
                if (_lastArgs is not null)
                {
                    await LoadData(_lastArgs);
                }
            }
        }
        else if (result is "readonly")
        {
            Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=bookmarks&mode=readonly");
        }
    }

    private void OnCreateCase()
    {
        Navigation.NavigateTo("/case/new?from=bookmarks");
    }

    private void ShowSearchTooltip(ElementReference args)
    {
        TooltipService.Open(args,
            "Search across case number, member name, rank, SSN, unit, incident description, " +
            "clinical diagnosis, medical findings, commander review details, witness information, " +
            "SJA and board review fields, and signature blocks. Results match any field containing " +
            "your search text.",
            new TooltipOptions { Duration = null, Position = TooltipPosition.Right, Style = "max-width: 480px; white-space: normal; padding: 12px 16px; background: var(--rz-panel-background-color); color: var(--rz-text-color); border: 1px solid var(--rz-border-color); box-shadow: var(--rz-shadow-2);" });
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
            await Task.Delay(700, token);
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

    private void OnCellContextMenu(DataGridCellMouseEventArgs<LineOfDutyCase> args)
    {
        _ = ShowContextMenuAsync(args);
    }

    private async Task ShowContextMenuAsync(DataGridCellMouseEventArgs<LineOfDutyCase> args)
    {
        var lodCase = args.Data;
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
                Text = "Remove Bookmark",
                Icon = "bookmark_remove",
                Value = "remove-bookmark"
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

                    case "remove-bookmark":
                        await OnRemoveBookmark(lodCase);
                        break;

                    case "copy":
                        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", lodCase.CaseId);
                        NotificationService.Notify(NotificationSeverity.Info, "Copied", $"Case ID {lodCase.CaseId} copied to clipboard.", closeOnClick: true);
                        break;

                    case "checkin":
                        var success = await CaseService.CheckInCaseAsync(lodCase.Id, lodCase.RowVersion);
                        if (success)
                        {
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
}
