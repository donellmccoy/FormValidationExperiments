using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Factories;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using ECTSystem.Web.Factories;
using ECTSystem.Web.Helpers;
using ECTSystem.Web.Services;
using ECTSystem.Web.Shared;
using ECTSystem.Web.StateMachines;
using ECTSystem.Web.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;
using Blazored.LocalStorage;
using Stateless;
using System.Text.Json;

namespace ECTSystem.Web.Pages;

public partial class EditCase : ComponentBase, IDisposable
{
    #region Constants & Lookups

    private static class TabNames
    {
        public const string MemberInformation = "Member Information";
        public const string MedicalTechnician = "Medical Technician";
        public const string MedicalOfficer = "Medical Officer";
        public const string UnitCommander = "Unit CC Review";
        public const string WingJudgeAdvocate = "Wing JA Review";
        public const string WingCommander = "Wing CC Review";
        public const string AppointingAuthority = "Appointing Authority";
        public const string BoardTechnicianReview = "Board Technician Review";
        public const string BoardMedicalReview = "Board Medical Review";
        public const string BoardLegalReview = "Board Legal Review";
        public const string BoardAdminReview = "Board Admin Review";
        public const string Draft = "Draft";
    }

    private static readonly (string Name, string Icon)[] TabMetadata =
    [
        ("Member Information",      "person"),
        ("Medical Technician",      "biotech"),
        ("Medical Officer",         "medical_services"),
        ("Unit Commander Review",   "edit_document"),
        ("Wing JA Review",          "gavel"),
        ("Appointing Authority",    "verified_user"),
        ("Wing Commander Review",   "stars"),
        ("Board Technician Review", "rate_review"),
        ("Board Medical Review",    "medical_services"),
        ("Board Legal Review",      "balance"),
        ("Board Admin Review",      "admin_panel_settings"),
    ];

    private static readonly Dictionary<string, (WorkflowState State, string DisplayName)> ReturnTargets = new()
    {
        ["return-med-tech"] = (WorkflowState.MedicalTechnicianReview, "Medical Technician"),
        ["return-med-officer"] = (WorkflowState.MedicalOfficerReview, "Medical Officer"),
        ["return-unit-cc"] = (WorkflowState.UnitCommanderReview, "Unit Commander"),
        ["return-wing-ja"] = (WorkflowState.WingJudgeAdvocateReview, "Wing Judge Advocate"),
        ["return-wing-cc"] = (WorkflowState.WingCommanderReview, "Wing Commander"),
        ["return-appointing-authority"] = (WorkflowState.AppointingAuthorityReview, "Appointing Authority"),
    };

    private static readonly Dictionary<string, (WorkflowTrigger Trigger, string DisplayName)> BoardTargets = new()
    {
        ["board-tech"] = (WorkflowTrigger.ForwardToBoardTechnicianReview, "Board Technician"),
        ["board-med"] = (WorkflowTrigger.ForwardToBoardMedicalReview, "Board Medical Officer"),
        ["board-legal"] = (WorkflowTrigger.ForwardToBoardLegalReview, "Board Legal Advisor"),
        ["board-admin"] = (WorkflowTrigger.ForwardToBoardAdministratorReview, "Board Administrator"),
    };

    private static readonly Dictionary<WorkflowTrigger, string> TriggerDisplayNames = new()
    {
        [WorkflowTrigger.ForwardToMedicalTechnician] = "Medical Technician",
        [WorkflowTrigger.ForwardToMedicalOfficerReview] = "Medical Officer",
        [WorkflowTrigger.ForwardToUnitCommanderReview] = "Unit Commander",
        [WorkflowTrigger.ForwardToWingJudgeAdvocateReview] = "Wing Judge Advocate",
        [WorkflowTrigger.ForwardToWingCommanderReview] = "Wing Commander",
        [WorkflowTrigger.ForwardToAppointingAuthorityReview] = "Appointing Authority",
        [WorkflowTrigger.ForwardToBoardTechnicianReview] = "Board Technician",
        [WorkflowTrigger.ForwardToBoardMedicalReview] = "Board Medical Officer",
        [WorkflowTrigger.ForwardToBoardLegalReview] = "Board Legal Advisor",
        [WorkflowTrigger.ForwardToBoardAdministratorReview] = "Board Administrator",
    };

    #endregion

    #region Injected Services

    [Inject]
    private ICaseService CaseService { get; set; }

    [Inject]
    private IBookmarkService BookmarkService { get; set; }

    [Inject]
    private IAuthorityService AuthorityService { get; set; }

    [Inject]
    private IDocumentService DocumentService { get; set; }

    [Inject]
    private IMemberService MemberService { get; set; }

    [Inject]
    private IWorkflowHistoryService WorkflowHistoryService { get; set; }

    [Inject]
    private LineOfDutyStateMachineFactory StateMachineFactory { get; set; }

    [Inject]
    private BookmarkCountService BookmarkCountService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Inject]
    private DialogService DialogService { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; }

    [Inject]
    private JsonSerializerOptions JsonOptions { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; }

    [Inject]
    private ILogger<EditCase> Logger { get; set; }

    [Inject]
    private HttpClient Http { get; set; }

    [Inject]
    private ILocalStorageService LocalStorage { get; set; }

    [Inject]
    private IUserService UserService { get; set; }

    [Inject]
    private ContextMenuService ContextMenuService { get; set; }

    [Inject]
    private CurrentUserService CurrentUserService { get; set; }

    #endregion

    #region Parameters

    [Parameter]
    public string CaseId { get; set; }

    [SupplyParameterFromQuery(Name = "from")]
    public string FromPage { get; set; }

    [SupplyParameterFromQuery(Name = "mode")]
    public string Mode { get; set; }

    #endregion

    #region Properties & Fields

    private bool IsNewCase => string.IsNullOrEmpty(CaseId);

    private bool IsReadOnly => !IsNewCase && !string.Equals(Mode, "edit", StringComparison.OrdinalIgnoreCase);

    private bool IsCheckedOutByMe => string.Equals(Mode, "edit", StringComparison.OrdinalIgnoreCase);

    private string NavigatedFromPath => FromPage?.ToLowerInvariant() switch
    {
        "cases" => "/cases",
        "bookmarks" => "/bookmarks",
        _ => "/"
    };

    private string BreadcrumbText => FromPage?.ToLowerInvariant() switch
    {
        "cases" => "Search Cases",
        "bookmarks" => "Bookmarks",
        _ => "Dashboard"
    };

    private static readonly object[] _dutyStatusOptions = [.. Enum.GetValues<DutyStatus>().Select(s => new { Text = s.ToDisplayString(), Value = (DutyStatus?)s })];

    private static readonly object[] _findingOptions = [.. Enum.GetValues<FindingType>().Select(f => new { Text = Regex.Replace(f.ToString(), "(\\B[A-Z])", " $1"), Value = (FindingType?)f })];

    private readonly PageOperationState _page = new();

    private readonly BookmarkUiState _bookmark = new();

    private readonly DocumentUiState _documents = new();

    private RadzenDataGrid<LineOfDutyDocument> _documentsGrid;


    private RadzenTextBox _documentsSearchBox;

    private string _documentsSearchText = string.Empty;
    private CancellationTokenSource _documentsSearchCts = new();
    private CancellationTokenSource _documentsLoadCts = new();
    private ODataEnumerable<LineOfDutyDocument> _documentsData;
    private int _documentsCount;

    private readonly CancellationTokenSource _cts = new();

    private LineOfDutyCase _lineOfDutyCase;

    private LineOfDutyStateMachine _stateMachine;

    private int _selectedTabIndex;

    private WorkflowSidebar _workflowSidebar;

    private int _selectedMemberId;

    private LineOfDutyViewModel _viewModel = new();

    private RadzenTemplateForm<LineOfDutyViewModel> _medicalForm;

    private IReadOnlyList<TrackableModel> AllFormModels => [_viewModel];

    private bool HasAnyChanges => _viewModel.IsDirty;

    private int NotificationCount => _lineOfDutyCase?.Notifications?.Count ?? 0;

    private WorkflowStep CurrentStep => _workflowSidebar?.CurrentStep;

    private string CurrentTabName => _selectedTabIndex >= 0 && _selectedTabIndex < TabMetadata.Length
        ? TabMetadata[_selectedTabIndex].Name
        : "Assessment";

    private string CurrentTabIcon => _selectedTabIndex >= 0 && _selectedTabIndex < TabMetadata.Length
        ? TabMetadata[_selectedTabIndex].Icon
        : "assignment";

    private RadzenDataGrid<LineOfDutyCase> _previousCasesGrid;
    private RadzenTextBox _previousCasesSearchBox;
    private ODataEnumerable<LineOfDutyCase> _previousCases;
    private int _previousCasesCount;
    private bool _previousCasesLoading;
    private string _previousCasesSearchText = string.Empty;
    private CancellationTokenSource _previousCasesSearchCts = new();
    private CancellationTokenSource _previousCasesLoadCts = new();
    private int _previousCasesMemberId;
    private Dictionary<int, int> _previousCasesBookmarkedIds = [];
    private readonly HashSet<int> _previousCasesAnimatingIds = [];
    private IList<LineOfDutyCase> _selectedPreviousCase;
    private string _currentUserId;

    private RadzenDataGrid<WorkflowStateHistory> _trackingGrid;
    private RadzenTextBox _trackingSearchBox;
    private string _trackingSearchText = string.Empty;
    private CancellationTokenSource _trackingSearchCts = new();
    private CancellationTokenSource _trackingLoadCts = new();
    private bool _trackingLoading;
    private IList<WorkflowStateHistory> _selectedTrackingEntry;
    private ODataEnumerable<WorkflowStateHistory> _trackingData;
    private int _trackingCount;
    private bool _trackingPreloaded;

    private async Task RefreshCaseHistoryGrid()
    {
        if (_previousCasesGrid is not null)
        {
            await _previousCasesGrid.FirstPage(true);
        }

        StateHasChanged();
    }

    private async Task RefreshTrackingGrid()
    {
        if (_trackingGrid is not null)
        {
            await _trackingGrid.FirstPage(true);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _currentUserId = await CurrentUserService.GetUserIdAsync();

        if (IsNewCase)
        {
            _selectedTabIndex = WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase?.GetCurrentWorkflowState() ?? WorkflowState.Draft);

            TakeSnapshots();
        }
        else
        {
            await LoadCaseAsync();
        }

        _page.IsLoading = false;
    }

    private string _loadedCaseId;

    protected override async Task OnParametersSetAsync()
    {
        if (!IsNewCase && CaseId != _loadedCaseId)
        {
            _page.IsLoading = true;
            StateHasChanged();

            await LoadCaseAsync();

            _page.IsLoading = false;
            StateHasChanged();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (IsNewCase && _memberSearchTextBox is not null)
            {
                await _memberSearchTextBox.Element.FocusAsync();
            }
        }
    }

    #endregion

    #region Data Loading

    private async Task LoadCaseAsync()
    {
        await SetBusyAsync("Loading case...");

        try
        {
            var (lodCase, isBookmarked) = await CaseService.GetCaseAsync(CaseId, _cts.Token);

            _lineOfDutyCase = lodCase;

            _stateMachine = StateMachineFactory.Create(_lineOfDutyCase);

            _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

            TakeSnapshots();

            _selectedTabIndex = WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase.GetCurrentWorkflowState());

            // Pre-populate tracking data from $expand to avoid a duplicate HTTP request
            // when the tracking tab is first opened
            if (_lineOfDutyCase.WorkflowStateHistories is { Count: > 0 })
            {
                _trackingPreloaded = true;
            }

            // Use bookmark status from response header; fall back to separate call if absent
            if (isBookmarked.HasValue)
            {
                _bookmark.IsBookmarked = isBookmarked.Value;
                if (isBookmarked.Value)
                {
                    // Header confirms bookmarked — fetch the bookmark ID for deletion
                    var map = await BookmarkService.GetBookmarkedCaseIdsAsync([_lineOfDutyCase.Id], _cts.Token);
                    map.TryGetValue(_lineOfDutyCase.Id, out var bid);
                    _bookmark.BookmarkId = bid;
                }
            }
            else
            {
                await CheckBookmarkAsync();
            }

            // Load previous cases and document count concurrently
            var previousCasesTask = LoadPreviousCasesAsync(_lineOfDutyCase.MemberId);
            var documentCountTask = LoadDocumentCountAsync(_lineOfDutyCase.Id);

            await Task.WhenAll(previousCasesTask, documentCountTask);

            _loadedCaseId = CaseId;

            // Load auth token for RadzenUpload Authorization header
            await RefreshUploadAuthTokenAsync();
        }
        catch (OperationCanceledException)
        {
            // Component disposed during load — silently ignore
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource disposed during load — silently ignore
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load case {CaseId}", CaseId);

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Load Failed",
                Detail = $"Failed to load case: {ex.Message}",
                Duration = 5000
            });

            _selectedTabIndex = WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase?.GetCurrentWorkflowState() ?? WorkflowState.Draft);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task CheckBookmarkAsync()
    {
        try
        {
            var map = await BookmarkService.GetBookmarkedCaseIdsAsync([_lineOfDutyCase.Id], _cts.Token);
            if (map.TryGetValue(_lineOfDutyCase.Id, out var bookmarkId))
            {
                _bookmark.IsBookmarked = true;
                _bookmark.BookmarkId = bookmarkId;
            }
            else
            {
                _bookmark.IsBookmarked = false;
                _bookmark.BookmarkId = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to check bookmark status for case {CaseId}", _lineOfDutyCase.Id);
        }
    }

    private async Task LoadPreviousCasesAsync(int memberId)
    {
        _previousCasesMemberId = memberId;
        _previousCasesSearchText = string.Empty;

        if (_previousCasesGrid is not null)
        {
            await _previousCasesGrid.FirstPage(true);
        }
    }

    private async Task LoadPreviousCasesData(LoadDataArgs args)
    {
        if (_previousCasesMemberId == 0)
        {
            return;
        }

        // Cancel any previous in-flight request
        await _previousCasesLoadCts.CancelAsync();
        _previousCasesLoadCts.Dispose();
        _previousCasesLoadCts = new CancellationTokenSource();
        var ct = _previousCasesLoadCts.Token;

        _previousCasesLoading = true;

        try
        {
            var currentId = _lineOfDutyCase?.Id ?? 0;
            var memberFilter = currentId > 0
                ? $"MemberId eq {_previousCasesMemberId} and Id ne {currentId}"
                : $"MemberId eq {_previousCasesMemberId}";

            var filter = CombinePreviousCasesFilters(memberFilter, args.Filter, BuildPreviousCasesSearchFilter(_previousCasesSearchText));

            var result = await CaseService.GetCasesAsync(
                filter: filter,
                top: args.Top,
                skip: args.Skip,
                orderby: !string.IsNullOrEmpty(args.OrderBy) ? args.OrderBy : "InitiationDate desc",
                select: "Id,CaseId,Unit,InitiationDate,CompletionDate,MemberId,IsCheckedOut,CheckedOutBy,CheckedOutByName",
                count: true,
                expand: $"WorkflowStateHistories($select=Id,WorkflowState),Bookmarks($filter=UserId eq '{_currentUserId}';$select=Id,UserId)",
                cancellationToken: ct);

            _previousCases = result?.Value?.AsODataEnumerable();
            _previousCasesCount = result?.Count ?? 0;

            var firstCase = _previousCases?.FirstOrDefault();
            _selectedPreviousCase = firstCase is not null ? new List<LineOfDutyCase> { firstCase } : null;

            _previousCasesBookmarkedIds = (_previousCases ?? Enumerable.Empty<LineOfDutyCase>())
                .Where(c => c.Bookmarks is not null)
                .SelectMany(c => c.Bookmarks.Select(b => new { CaseId = c.Id, BookmarkId = b.Id }))
                .ToDictionary(x => x.CaseId, x => x.BookmarkId);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("LoadPreviousCasesData cancelled — superseded by newer request");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load previous cases for member {MemberId}", _previousCasesMemberId);
            _previousCases = null;
            _previousCasesCount = 0;
        }
        finally
        {
            _previousCasesLoading = false;
        }
    }

    private async Task OnPreviousCasesSearchInput(ChangeEventArgs args)
    {
        _previousCasesSearchText = args.Value?.ToString() ?? string.Empty;

        await _previousCasesSearchCts.CancelAsync();
        _previousCasesSearchCts.Dispose();
        _previousCasesSearchCts = new CancellationTokenSource();
        var token = _previousCasesSearchCts.Token;

        try
        {
            await Task.Delay(700, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_previousCasesGrid is not null)
        {
            await _previousCasesGrid.FirstPage(true);
        }
    }

    private async Task OnTrackingSearchInput(ChangeEventArgs args)
    {
        _trackingSearchText = args.Value?.ToString() ?? string.Empty;

        await _trackingSearchCts.CancelAsync();
        _trackingSearchCts.Dispose();
        _trackingSearchCts = new CancellationTokenSource();
        var token = _trackingSearchCts.Token;

        try
        {
            await Task.Delay(700, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_trackingGrid is not null)
        {
            await _trackingGrid.FirstPage(true);
        }
    }

    /// <summary>
    /// Server-side data load handler for the Tracking (Workflow History) grid.
    /// </summary>
    private async Task LoadTrackingData(LoadDataArgs args)
    {
        if (_lineOfDutyCase?.Id is null or 0)
        {
            return;
        }

        // On the first grid load, use the histories already fetched via $expand
        // to avoid a duplicate HTTP request
        if (_trackingPreloaded && string.IsNullOrEmpty(_trackingSearchText))
        {
            _trackingPreloaded = false;
            var allHistories = _lineOfDutyCase.WorkflowStateHistories
                .OrderByDescending(h => h.CreatedDate)
                .ThenByDescending(h => h.Id)
                .ToList();

            _trackingCount = allHistories.Count;

            IEnumerable<WorkflowStateHistory> paged = allHistories;
            if (args.Skip.HasValue) paged = paged.Skip(args.Skip.Value);
            if (args.Top.HasValue) paged = paged.Take(args.Top.Value);

            _trackingData = paged.ToList().AsODataEnumerable();
            _selectedTrackingEntry = _trackingData?.FirstOrDefault() is { } first
                ? new List<WorkflowStateHistory> { first }
                : null;
            return;
        }

        // Cancel any previous in-flight request
        await _trackingLoadCts.CancelAsync();
        _trackingLoadCts.Dispose();
        _trackingLoadCts = new CancellationTokenSource();
        var ct = _trackingLoadCts.Token;

        _trackingLoading = true;

        try
        {
            var filter = CombineTrackingFilters(args.Filter, BuildTrackingSearchFilter(_trackingSearchText));

            var result = await WorkflowHistoryService.GetWorkflowStateHistoriesAsync(
                caseId: _lineOfDutyCase.Id,
                filter: filter,
                top: args.Top,
                skip: args.Skip,
                orderby: !string.IsNullOrEmpty(args.OrderBy) ? args.OrderBy : "CreatedDate desc,Id desc",
                count: true,
                cancellationToken: ct);

            _trackingData = result?.Value?.AsODataEnumerable();
            _trackingCount = result?.Count ?? 0;

            _selectedTrackingEntry = _trackingData?.FirstOrDefault() is { } first
                ? new List<WorkflowStateHistory> { first }
                : null;
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug("LoadTrackingData cancelled — superseded by newer request");
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load tracking history for case {CaseId}", _lineOfDutyCase?.Id);
            _trackingData = null;
            _trackingCount = 0;
        }
        finally
        {
            _trackingLoading = false;
        }
    }

    private static string BuildTrackingSearchFilter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escaped = text.Replace("'", "''");

        var parts = new List<string>
        {
            $"contains(CreatedBy,'{escaped}')",
            $"contains(ModifiedBy,'{escaped}')"
        };

        foreach (var state in Enum.GetValues<WorkflowState>())
        {
            if (state.ToDisplayString().Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add($"WorkflowState eq '{state}'");
            }
        }

        return string.Join(" or ", parts);
    }

    private static string CombineTrackingFilters(string columnFilter, string searchFilter)
    {
        var parts = new[] { columnFilter, searchFilter }
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => $"({p})")
            .ToList();

        return parts.Count > 0 ? string.Join(" and ", parts) : null;
    }

    private static string BuildPreviousCasesSearchFilter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var escaped = text.Replace("'", "''");
        return $"contains(CaseId,'{escaped}') or contains(Unit,'{escaped}')";
    }

    private static string CombinePreviousCasesFilters(string memberFilter, string columnFilter, string searchFilter)
    {
        var parts = new[] { memberFilter, columnFilter, searchFilter }
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => $"({p})")
            .ToList();

        return parts.Count > 0 ? string.Join(" and ", parts) : null;
    }

    private async Task TogglePreviousBookmark(LineOfDutyCase lodCase)
    {
        var isBookmarked = _previousCasesBookmarkedIds.ContainsKey(lodCase.Id);

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

            await SetBusyAsync($"Removing bookmark for case {lodCase.CaseId}...");
            try
            {
                var bookmarkId = _previousCasesBookmarkedIds[lodCase.Id];
                await BookmarkService.DeleteBookmarkAsync(lodCase.Id, bookmarkId);
                _previousCasesBookmarkedIds.Remove(lodCase.Id);
                BookmarkCountService.Decrement();
                NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to remove bookmark for case {CaseId}", lodCase.Id);
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else
        {
            _previousCasesAnimatingIds.Add(lodCase.Id);
            await SetBusyAsync($"Adding bookmark for case {lodCase.CaseId}...");

            try
            {
                var newBookmarkId = await BookmarkService.AddBookmarkAsync(lodCase.Id);
                _previousCasesBookmarkedIds[lodCase.Id] = newBookmarkId;
                BookmarkCountService.Increment();
                NotificationService.Notify(NotificationSeverity.Success, "Bookmark Added", $"Case {lodCase.CaseId} added to bookmarks.", closeOnClick: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to add bookmark for case {CaseId}", lodCase.Id);
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }

            await Task.Delay(800);
            _previousCasesAnimatingIds.Remove(lodCase.Id);
        }
    }

    private async Task OnPreviousCaseClick(LineOfDutyCase lodCase)
    {
        if (lodCase.IsCheckedOut)
        {
            if (string.Equals(lodCase.CheckedOutBy, _currentUserId, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Case {CaseId} already checked out by current user — opening in edit mode", lodCase.CaseId);
                Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=case&mode=edit");
            }
            else
            {
                Logger.LogInformation("Case {CaseId} checked out by {CheckedOutBy} — opening read-only", lodCase.CaseId, lodCase.CheckedOutByName);
                Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=case&mode=readonly");
            }
            return;
        }

        var result = await DialogService.OpenAsync<Shared.CheckOutCaseDialog>(
            "Check Out Case",
            new Dictionary<string, object> { { "CaseId", lodCase.CaseId } },
            new DialogOptions { ShowClose = false, Width = "auto" });

        if (result is "checkout")
        {
            await SetBusyAsync($"Checking out case {lodCase.CaseId}...");
            LineOfDutyCase updated;
            try
            {
                updated = await CaseService.CheckOutCaseViaODataAsync(lodCase.Id, lodCase.RowVersion);
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }

            if (updated is not null)
            {
                // Merge fresh concurrency token + checkout state onto the grid row so a
                // subsequent action against this row uses the up-to-date RowVersion.
                lodCase.RowVersion = updated.RowVersion;
                lodCase.IsCheckedOut = updated.IsCheckedOut;
                lodCase.CheckedOutBy = updated.CheckedOutBy ?? string.Empty;
                lodCase.CheckedOutByName = updated.CheckedOutByName ?? string.Empty;
                lodCase.CheckedOutDate = updated.CheckedOutDate;

                Logger.LogInformation("Checked out case {CaseId} for editing", lodCase.CaseId);
                Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=case&mode=edit");
            }
            else
            {
                Logger.LogWarning("Checkout failed for case {CaseId} (Id: {Id})", lodCase.CaseId, lodCase.Id);
                NotificationService.Notify(NotificationSeverity.Error,
                    "Checkout Failed",
                    $"Could not check out Case {lodCase.CaseId}. It may have been checked out by another user.",
                    closeOnClick: true);
                await _previousCasesGrid.Reload();
            }
        }
        else if (result is "readonly")
        {
            Navigation.NavigateTo($"/case/{lodCase.CaseId}?from=case&mode=readonly");
        }
    }

    private void OnPreviousCaseCellContextMenu(DataGridCellMouseEventArgs<LineOfDutyCase> args)
    {
        _ = ShowPreviousCaseContextMenuAsync(args);
    }

    private async Task ShowPreviousCaseContextMenuAsync(DataGridCellMouseEventArgs<LineOfDutyCase> args)
    {
        var lodCase = args.Data;
        var isCheckedOutByMe = lodCase.IsCheckedOut
            && string.Equals(lodCase.CheckedOutBy, _currentUserId, StringComparison.OrdinalIgnoreCase);

        var items = new List<ContextMenuItem>
        {
            new ContextMenuItem { Text = "Open Case", Icon = "open_in_new", Value = "open" },
            new ContextMenuItem
            {
                Text = _previousCasesBookmarkedIds.ContainsKey(lodCase.Id) ? "Remove Bookmark" : "Add Bookmark",
                Icon = _previousCasesBookmarkedIds.ContainsKey(lodCase.Id) ? "bookmark_remove" : "bookmark_add",
                Value = "bookmark"
            },
            new ContextMenuItem { Text = "Copy Case ID", Icon = "content_copy", Value = "copy" }
        };

        if (isCheckedOutByMe)
        {
            items.Add(new ContextMenuItem { Text = "Check In", Icon = "lock_open", Value = "checkin" });
        }

        ContextMenuService.Open(args, items,
            async menuItem =>
            {
                ContextMenuService.Close();

                switch (menuItem.Value?.ToString())
                {
                    case "open":
                        await OnPreviousCaseClick(lodCase);
                        break;

                    case "bookmark":
                        await TogglePreviousBookmark(lodCase);
                        break;

                    case "copy":
                        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText", lodCase.CaseId);
                        NotificationService.Notify(NotificationSeverity.Info, "Copied", $"Case ID {lodCase.CaseId} copied to clipboard.", closeOnClick: true);
                        break;

                    case "checkin":
                        await SetBusyAsync($"Checking in case {lodCase.CaseId}...");
                        LineOfDutyCase checkedIn;
                        try
                        {
                            checkedIn = await CaseService.CheckInCaseViaODataAsync(lodCase.Id, lodCase.RowVersion);
                        }
                        finally
                        {
                            await SetBusyAsync(isBusy: false);
                        }

                        if (checkedIn is not null)
                        {
                            // Merge fresh concurrency token + checkout state onto the grid row
                            // so any further action on this reference uses the up-to-date RowVersion.
                            lodCase.RowVersion = checkedIn.RowVersion;
                            lodCase.IsCheckedOut = checkedIn.IsCheckedOut;
                            lodCase.CheckedOutBy = checkedIn.CheckedOutBy ?? string.Empty;
                            lodCase.CheckedOutByName = checkedIn.CheckedOutByName ?? string.Empty;
                            lodCase.CheckedOutDate = checkedIn.CheckedOutDate;

                            Logger.LogInformation("Checked in case {CaseId}", lodCase.CaseId);
                            NotificationService.Notify(NotificationSeverity.Success, "Checked In", $"Case {lodCase.CaseId} has been checked in.", closeOnClick: true);
                            await _previousCasesGrid.Reload();
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

    #region Workflow Actions

    private async Task OnMemberForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "cancel")
        {
            var cancelConfirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this line of duty case?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Cancel Case", CancelButtonText = "Don't Cancel Case" });

            if (cancelConfirmed != true)
            {
                return;
            }

            Navigation.NavigateTo(NavigatedFromPath, replace: true);

            return;
        }

        // For existing cases, delegate to the shared workflow action handler
        // which handles IsReadOnly, error handling, result application, and grid reload.
        if (!IsNewCase)
        {
            await OnForwardClick(item, WorkflowTrigger.ForwardToMedicalTechnician);
            return;
        }

        // New case creation — unique to the Member Information tab.
        if (_selectedMemberId == 0)
        {
            NotificationService.Notify(NotificationSeverity.Warning, "Member Required", "Please search for and select a member before starting a case.");
            return;
        }

        var confirmed = await DialogService.Confirm(
            "Are you sure you want to start this line of duty case?",
            "Start Line of Duty Case",
            new ConfirmOptions
            {
                OkButtonText = "Yes",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Creating line of duty case...");

        try
        {
            var lineOfDutyCase = LineOfDutyCaseFactory.Create(_selectedMemberId);

            LineOfDutyCaseMapper.ApplyToCase(_viewModel, lineOfDutyCase);

            // Persist the case first so it gets a real DB Id — the API creates the
            // case with an initial MemberInformationEntry workflow state history
            // entry, so the case is never in Draft state on the server.
            lineOfDutyCase = await CaseService.SaveCaseAsync(lineOfDutyCase, _cts.Token);

            // The OData client doesn't merge navigation collections from the POST
            // response, so WorkflowStateHistories is empty. Fetch the initial
            // history entries so the state machine can properly close out the
            // MemberInformationEntry entry when advancing to MedicalTechnicianReview.
            var initialHistories = await WorkflowHistoryService.GetWorkflowStateHistoriesAsync(
                lineOfDutyCase.Id, cancellationToken: _cts.Token);

            if (initialHistories.Value is not null)
            {
                lineOfDutyCase.WorkflowStateHistories = new List<WorkflowStateHistory>(initialHistories.Value);
            }

            // The API already created the MemberInformationEntry state, so start
            // the client-side state machine at MemberInformationEntry and advance
            // to MedicalTechnicianReview.
            _stateMachine = StateMachineFactory.CreateAtState(WorkflowState.MemberInformationEntry);

            var result = await _stateMachine.FireAsync(lineOfDutyCase, WorkflowTrigger.ForwardToMedicalTechnician);

            if (result.Success)
            {
                _lineOfDutyCase = result.Case;

                CaseId = _lineOfDutyCase.CaseId;

                // Auto-checkout the newly created case so the creator can edit immediately.
                // Merge the returned scalar fields (fresh RowVersion + checkout state) onto the
                // in-memory case so subsequent saves use the current concurrency token without
                // a re-fetch. The server response is sparse — do not replace the whole object
                // or loaded navigation collections (Member, MEDCON, WorkflowStateHistories, ...)
                // would be wiped.
                var checkedOut = await CaseService.CheckOutCaseViaODataAsync(_lineOfDutyCase.Id, _lineOfDutyCase.RowVersion, _cts.Token);
                if (checkedOut is not null)
                {
                    _lineOfDutyCase.RowVersion = checkedOut.RowVersion;
                    _lineOfDutyCase.IsCheckedOut = checkedOut.IsCheckedOut;
                    _lineOfDutyCase.CheckedOutBy = checkedOut.CheckedOutBy;
                    _lineOfDutyCase.CheckedOutByName = checkedOut.CheckedOutByName;
                    _lineOfDutyCase.CheckedOutDate = checkedOut.CheckedOutDate;
                }
                Mode = "edit";

                _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                TakeSnapshots();

                _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

                _selectedTabIndex = result.TabIndex;

                // Force an initial render so the tab and sidebar bindings commit
                // before the navigation triggers a parameter rebind cycle.
                StateHasChanged();

                // The tracking grid's LoadData fired during initial render (create
                // mode) but returned early because Id was 0. Reload it now so it
                // picks up the workflow history entries created during case setup.
                if (_trackingGrid is not null)
                {
                    await _trackingGrid.Reload();
                }

                NotificationService.Notify(
                    NotificationSeverity.Success,
                    "Line of Duty Case Started",
                    $"Case: {_lineOfDutyCase.CaseId} created for: {_lineOfDutyCase.MemberName}.");

                // Update the URL from /case/new to /case/{id} so the page is
                // in proper edit mode. Set _loadedCaseId first so that
                // OnParametersSetAsync does not re-fetch the case we just set up.
                _loadedCaseId = CaseId;
                Navigation.NavigateTo($"/case/{CaseId}?from=case&mode=edit", replace: true);

                // Re-render after the navigation-driven parameter rebind so the
                // sidebar and tabs reflect the in-memory state we just set up.
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to create case: {CaseId}", CaseId);

            NotificationService.Notify(
                NotificationSeverity.Error,
                "Create Line of Duty Case Failed",
                ex.Message);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task OnForwardClick(RadzenSplitButtonItem item, WorkflowTrigger trigger)
    {
        var name = TriggerDisplayNames[trigger];

        await FireWorkflowActionAsync(
            item,
            $"Are you sure you want to forward the case to the {name}?",
            $"Forward to {name}",
            $"Forwarding case to {name}...",
            trigger);
    }

    private async Task OnCompleteClick(RadzenSplitButtonItem item)
    {
        await FireWorkflowActionAsync(
            item,
            "Are you sure you want to complete this line of duty case?",
            "Complete Case",
            "Completing line of duty case...",
            WorkflowTrigger.Complete,
            okButtonText: "Complete",
            notifySummary: "Line of Duty Case Completed",
            notifyVerb: "completed");
    }

    private async Task FireWorkflowActionAsync(
        RadzenSplitButtonItem item,
        string confirmMessage,
        string confirmTitle,
        string busyMessage,
        WorkflowTrigger forwardTrigger,
        string okButtonText = "Yes",
        string notifySummary = "Line of Duty Case Updated",
        string notifyVerb = "updated")
    {
        if (IsReadOnly)
        {
            return;
        }

        var value = item?.Value;

        // Revert — restore snapshot without any state transition.
        if (value == "revert")
        {
            await OnRevertChanges();
            return;
        }

        // Cancel — confirm, then navigate away without saving.
        if (value == "cancel")
        {
            var cancelConfirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this line of duty case?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Cancel Case", CancelButtonText = "Don't Cancel Case" });

            if (cancelConfirmed != true)
            {
                return;
            }

            Navigation.NavigateTo(NavigatedFromPath, replace: true);

            return;
        }

        // Return — send the case back to an earlier workflow step.
        if (value is not null && ReturnTargets.TryGetValue(value, out var returnTarget))
        {
            var returnConfirmed = await DialogService.Confirm(
                $"Are you sure you want to return the case to {returnTarget.DisplayName}?",
                $"Return to {returnTarget.DisplayName}",
                new ConfirmOptions { OkButtonText = "Yes", CancelButtonText = "Cancel" });

            if (returnConfirmed != true)
            {
                return;
            }

            await SetBusyAsync($"Returning case to {returnTarget.DisplayName}...");

            try
            {
                LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

                var result = await _stateMachine.FireReturnAsync(_lineOfDutyCase, returnTarget.State);

                ApplyTransitionResult(result, notifySummary, notifyVerb);
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }

            return;
        }

        // Board — lateral transfer to another board reviewer.
        if (value is not null && BoardTargets.TryGetValue(value, out var boardTarget))
        {
            var boardConfirmed = await DialogService.Confirm(
                $"Are you sure you want to forward the case to {boardTarget.DisplayName}?",
                $"Forward to {boardTarget.DisplayName}",
                new ConfirmOptions { OkButtonText = "Yes", CancelButtonText = "Cancel" });

            if (boardConfirmed != true)
            {
                return;
            }

            await SetBusyAsync($"Forwarding case to {boardTarget.DisplayName}...");

            try
            {
                LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

                var result = await _stateMachine.FireAsync(_lineOfDutyCase, boardTarget.Trigger);

                ApplyTransitionResult(result, notifySummary, notifyVerb);
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }

            return;
        }

        // Default — forward to the next workflow step.
        var confirmed = await DialogService.Confirm(
            confirmMessage,
            confirmTitle,
            new ConfirmOptions
            {
                OkButtonText = okButtonText,
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync(busyMessage);

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, forwardTrigger);

            ApplyTransitionResult(result, notifySummary, notifyVerb);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private void ApplyTransitionResult(StateMachineResult result, string notifySummary, string notifyVerb)
    {
        if (!result.Success)
        {
            return;
        }

        _lineOfDutyCase = result.Case;

        CaseId = _lineOfDutyCase.CaseId;

        _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

        TakeSnapshots();

        _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

        _selectedTabIndex = result.TabIndex;

        NotificationService.Notify(
            NotificationSeverity.Success,
            notifySummary,
            $"Case: {_lineOfDutyCase.CaseId} {notifyVerb} for: {_lineOfDutyCase.MemberName}.");
    }

    #endregion

    #region Tab & Form Helpers

    private bool IsTabDisabled(int tabIndex)
    {
        return WorkflowTabHelper.IsTabDisabled(tabIndex, _lineOfDutyCase?.GetCurrentWorkflowState() ?? WorkflowState.Draft);
    }

    #endregion

    #region Form Field Change Handlers

    private void OnIsMilitaryFacilityChanged()
    {
        if (_viewModel.IsMilitaryFacility != true)
        {
            _viewModel.TreatmentFacilityName = null;
        }
    }

    private void OnWasUnderInfluenceChanged()
    {
        if (_viewModel.WasUnderInfluence != true)
        {
            _viewModel.SubstanceType = null;
        }
    }

    private void OnToxicologyTestDoneChanged()
    {
        if (_viewModel.ToxicologyTestDone != true)
        {
            _viewModel.ToxicologyTestResults = null;
        }
    }

    private void OnPsychiatricEvalCompletedChanged()
    {
        if (_viewModel.PsychiatricEvalCompleted != true)
        {
            _viewModel.PsychiatricEvalDate = null;
            _viewModel.PsychiatricEvalResults = null;
        }
    }

    private void OnOtherTestsDoneChanged()
    {
        if (_viewModel.OtherTestsDone != true)
        {
            _viewModel.OtherTestDate = null;
            _viewModel.OtherTestResults = null;
        }
    }

    private void OnIsEptsNsaChanged()
    {
        if (_viewModel.IsEptsNsa != true)
        {
            _viewModel.IsServiceAggravated = null;
        }
    }

    private void OnIsAtDeployedLocationChanged()
    {
        if (_viewModel.IsAtDeployedLocation != false)
        {
            _viewModel.RequiresArcBoard = null;
        }
    }

    #endregion

    #region Save & Revert

    private async Task OnRevertChanges()
    {
        var confirmed = await DialogService.Confirm(
            "Revert all unsaved changes? This cannot be undone.",
            "Confirm Revert",
            new ConfirmOptions { OkButtonText = "Revert", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        foreach (var model in AllFormModels)
        {
            model.Revert();
        }

        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Info,
            Summary = "Reverted",
            Detail = "All unsaved changes have been reverted.",
            Duration = 3000
        });

        StateHasChanged();
    }

    public void TakeSnapshots()
    {
        foreach (var model in AllFormModels)
        {
            model.TakeSnapshot(JsonOptions);
        }
    }

    private async Task OnApplyChangesClick(RadzenSplitButtonItem item)
    {
        if (IsReadOnly)
        {
            return;
        }

        if (item?.Value == "revert")
        {
            await OnRevertChanges();

            return;
        }

        // Validate the medical tab before saving
        if (_selectedTabIndex == 1 && _medicalForm?.EditContext?.Validate() == false)
        {
            return;
        }

        // Determine which tab to save based on the currently selected tab index
        var source = _selectedTabIndex switch
        {
            0 => TabNames.MemberInformation,
            1 => TabNames.MedicalTechnician,
            2 => TabNames.MedicalOfficer,
            3 => TabNames.UnitCommander,
            4 => TabNames.WingJudgeAdvocate,
            5 => TabNames.AppointingAuthority,
            6 => TabNames.WingCommander,
            7 => TabNames.BoardTechnicianReview,
            8 => TabNames.BoardMedicalReview,
            9 => TabNames.BoardLegalReview,
            10 => TabNames.BoardAdminReview,
            _ => TabNames.Draft
        };

        var confirmed = await DialogService.Confirm(
            "Are you sure you want to save?",
            "Confirm Save",
            new ConfirmOptions { OkButtonText = "Save", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SaveTabFormDataAsync(source);
    }

    private async Task SaveTabFormDataAsync(string tabName)
    {
        if (_lineOfDutyCase is null || IsReadOnly)
        {
            return;
        }

        SetIsSaving(true);

        await SetBusyAsync($"Saving {tabName}...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            _lineOfDutyCase = await CaseService.SaveCaseAsync(_lineOfDutyCase, _cts.Token);

            // Persist authority entries (commander, SJA, medical provider) via
            // the dedicated batch-upsert endpoint — the scalar PATCH body cannot
            // include navigation collections. SaveCaseAsync preserves in-memory
            // navigation data via capture/restore, so Authorities still holds
            // the ApplyToCase changes.
            if (_lineOfDutyCase.Id > 0 && _lineOfDutyCase.Authorities.Count > 0)
            {
                var savedAuthorities = await AuthorityService.SaveAuthoritiesAsync(
                    _lineOfDutyCase.Id, _lineOfDutyCase.Authorities, _cts.Token);

                _lineOfDutyCase.Authorities = savedAuthorities;
            }

            _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

            TakeSnapshots();

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Saved",
                Detail = $"{tabName} saved successfully.",
                Duration = 3000
            });
        }
        catch (OperationCanceledException)
        {
            // Component disposed during save — silently ignore
        }
        catch (ObjectDisposedException)
        {
            // CancellationTokenSource disposed during save — silently ignore
        }
        catch (Exception ex)
        {
            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Error,
                Summary = "Save Failed",
                Detail = ex.Message,
                Duration = 5000
            });
        }
        finally
        {
            SetIsSaving(false);

            await SetBusyAsync(isBusy: false);
        }
    }

    #endregion

    #region Bookmarks & Documents

    private async Task OnHistoryClick()
    {
        await OnOuterTabIndexChanged(OuterCaseHistoryTabIndex);
    }

    private async Task OnBookmarkClick()
    {
        if (_lineOfDutyCase?.Id is null or 0)
        {
            return;
        }

        _bookmark.IsBookmarked = !_bookmark.IsBookmarked;

        if (_bookmark.IsBookmarked)
        {
            _bookmark.IsAnimating = true;
            await SetBusyAsync($"Adding bookmark for case {_viewModel?.CaseNumber}...");

            try
            {
                _bookmark.BookmarkId = await BookmarkService.AddBookmarkAsync(_lineOfDutyCase.Id, _cts.Token);
                BookmarkCountService.Increment();
                NotificationService.Notify(NotificationSeverity.Success, "Bookmark Added", $"Case {_viewModel?.CaseNumber} added to bookmarks.", closeOnClick: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to add bookmark for case {CaseId}", _lineOfDutyCase.Id);
                _bookmark.IsBookmarked = false; // Revert on failure
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }

            await Task.Delay(800);
            _bookmark.IsAnimating = false;
        }
        else
        {
            // Revert optimistic toggle — wait for confirmation before removing
            _bookmark.IsBookmarked = true;

            var confirmed = await DialogService.Confirm(
                $"Remove bookmark for case {_viewModel?.CaseNumber}?",
                "Remove Bookmark",
                new ConfirmOptions { OkButtonText = "Remove", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            _bookmark.IsBookmarked = false;
            await SetBusyAsync($"Removing bookmark for case {_viewModel?.CaseNumber}...");

            try
            {
                await BookmarkService.DeleteBookmarkAsync(_lineOfDutyCase.Id, _bookmark.BookmarkId!.Value, _cts.Token);
                _bookmark.BookmarkId = null;
                BookmarkCountService.Decrement();
                NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {_viewModel?.CaseNumber} removed from bookmarks.", closeOnClick: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to remove bookmark for case {CaseId}", _lineOfDutyCase.Id);
                _bookmark.IsBookmarked = true; // Revert on failure
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
    }

    private async Task OnCheckInClick()
    {
        if (_lineOfDutyCase is null || _lineOfDutyCase.Id == 0)
        {
            return;
        }

        var confirm = await DialogService.Confirm(
            "Are you sure you want to check in this case? Other users will be able to edit it.",
            "Check In Case",
            new ConfirmOptions
            {
                OkButtonText = "Check In",
                CancelButtonText = "Cancel"
            });

        if (confirm != true)
        {
            return;
        }

        await SetBusyAsync($"Checking in case {_lineOfDutyCase.CaseId}...");

        try
        {
            var checkedIn = await CaseService.CheckInCaseViaODataAsync(_lineOfDutyCase.Id, _lineOfDutyCase.RowVersion);

            if (checkedIn is not null)
            {
                // Merge the refreshed scalar fields (fresh RowVersion + cleared checkout state) onto the
                // in-memory case so any subsequent operations on this page use the current concurrency
                // token. The server response is sparse — do not replace the whole object or loaded
                // navigation collections (Member, MEDCON, WorkflowStateHistories, ...) would be wiped.
                _lineOfDutyCase.RowVersion = checkedIn.RowVersion;
                _lineOfDutyCase.IsCheckedOut = checkedIn.IsCheckedOut;
                _lineOfDutyCase.CheckedOutBy = checkedIn.CheckedOutBy ?? string.Empty;
                _lineOfDutyCase.CheckedOutByName = checkedIn.CheckedOutByName ?? string.Empty;
                _lineOfDutyCase.CheckedOutDate = checkedIn.CheckedOutDate;

                NotificationService.Notify(NotificationSeverity.Success, "Checked In", $"Case {_lineOfDutyCase.CaseId} has been checked in.", closeOnClick: true);

                Navigation.NavigateTo($"/case/{CaseId}?from={FromPage}&mode=readonly");
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Check-In Failed", "Could not check in the case. Please try again.", closeOnClick: true);
            }
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task OnCheckOutClick()
    {
        if (_lineOfDutyCase is null || _lineOfDutyCase.Id == 0)
        {
            return;
        }

        var result = await DialogService.OpenAsync<Shared.CheckOutCaseDialog>(
            "Check Out Case",
            new Dictionary<string, object> { { "CaseId", _lineOfDutyCase.CaseId } },
            new DialogOptions { ShowClose = false, Width = "auto" });

        if (result is "checkout")
        {
            await SetBusyAsync($"Checking out case {_lineOfDutyCase.CaseId}...");

            try
            {
                var updated = await CaseService.CheckOutCaseViaODataAsync(_lineOfDutyCase.Id, _lineOfDutyCase.RowVersion);

                if (updated is not null)
                {
                    // Merge fresh concurrency token + checkout state onto bound model so any
                    // subsequent operation on this page (or revisit before navigation completes)
                    // sends the up-to-date RowVersion instead of triggering a 409.
                    _lineOfDutyCase.RowVersion = updated.RowVersion;
                    _lineOfDutyCase.IsCheckedOut = updated.IsCheckedOut;
                    _lineOfDutyCase.CheckedOutBy = updated.CheckedOutBy ?? string.Empty;
                    _lineOfDutyCase.CheckedOutByName = updated.CheckedOutByName ?? string.Empty;
                    _lineOfDutyCase.CheckedOutDate = updated.CheckedOutDate;

                    NotificationService.Notify(NotificationSeverity.Success, "Checked Out", $"Case {_lineOfDutyCase.CaseId} has been checked out for editing.", closeOnClick: true);
                    Navigation.NavigateTo($"/case/{CaseId}?from={FromPage}&mode=edit");
                }
                else
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Checkout Failed", $"Could not check out Case {_lineOfDutyCase.CaseId}. It may have been checked out by another user.", closeOnClick: true);
                }
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
    }

    #endregion

    #region UI Helpers

    private void SetIsSaving(bool isSaving)
    {
        _page.IsSaving = isSaving;
    }

    private async Task SetBusyAsync(string message = "Working...", bool? isBusy = true)
    {
        _page.BusyMessage = message;
        _page.IsBusy = isBusy.GetValueOrDefault(true);
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
        _previousCasesSearchCts.Cancel();
        _previousCasesSearchCts.Dispose();
        _previousCasesLoadCts.Cancel();
        _previousCasesLoadCts.Dispose();
        _trackingSearchCts.Cancel();
        _trackingSearchCts.Dispose();
        _trackingLoadCts.Cancel();
        _trackingLoadCts.Dispose();
        _documentsSearchCts.Cancel();
        _documentsSearchCts.Dispose();
        _documentsLoadCts.Cancel();
        _documentsLoadCts.Dispose();
    }

    #endregion
}
