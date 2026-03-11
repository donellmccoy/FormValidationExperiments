using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using ECTSystem.Web.Services;
using ECTSystem.Web.Shared;
using Microsoft.AspNetCore.Components;
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
        ("Case Dialogue",           "forum"),
        ("Notifications",           "notifications"),
        ("Documents",               "folder"),
        ("Air Force Form 348",      "description"),
        ("Case History",            "history"),
        ("Tracking",                "track_changes"),
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

    private static readonly Dictionary<string, (LineOfDutyTrigger Trigger, string DisplayName)> BoardTargets = new()
    {
        ["board-tech"] = (LineOfDutyTrigger.ForwardToBoardTechnicianReview, "Board Technician"),
        ["board-med"] = (LineOfDutyTrigger.ForwardToBoardMedicalReview, "Board Medical Officer"),
        ["board-legal"] = (LineOfDutyTrigger.ForwardToBoardLegalReview, "Board Legal Advisor"),
        ["board-admin"] = (LineOfDutyTrigger.ForwardToBoardAdministratorReview, "Board Administrator"),
    };

    private static readonly Dictionary<LineOfDutyTrigger, string> TriggerDisplayNames = new()
    {
        [LineOfDutyTrigger.ForwardToMedicalOfficerReview] = "Medical Officer",
        [LineOfDutyTrigger.ForwardToUnitCommanderReview] = "Unit Commander",
        [LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview] = "Wing Judge Advocate",
        [LineOfDutyTrigger.ForwardToWingCommanderReview] = "Wing Commander",
        [LineOfDutyTrigger.ForwardToAppointingAuthorityReview] = "Appointing Authority",
        [LineOfDutyTrigger.ForwardToBoardTechnicianReview] = "Board Technician",
        [LineOfDutyTrigger.ForwardToBoardMedicalReview] = "Board Medical Officer",
        [LineOfDutyTrigger.ForwardToBoardLegalReview] = "Board Legal Advisor",
        [LineOfDutyTrigger.ForwardToBoardAdministratorReview] = "Board Administrator",
    };

    #endregion

    #region Injected Services

    [Inject]
    private IDataService CaseService { get; set; }

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

    #endregion

    #region Parameters

    [Parameter]
    public string CaseId { get; set; }

    [SupplyParameterFromQuery(Name = "from")]
    public string FromPage { get; set; }

    #endregion

    #region Properties & Fields

    private bool IsNewCase => string.IsNullOrEmpty(CaseId);

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

    private readonly PageOperationState _page = new();

    private readonly BookmarkUiState _bookmark = new();

    private readonly DocumentUiState _documents = new();

    private RadzenDataGrid<LineOfDutyDocument> _documentsGrid;

    private string _documentsSearchText = string.Empty;
    private CancellationTokenSource _documentsSearchCts = new();

    private readonly CancellationTokenSource _cts = new();

    private LineOfDutyCase _lineOfDutyCase;

    private LineOfDutyStateMachine _stateMachine;

    private int _selectedTabIndex;

    private RadzenTabs _tabs;

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
    private ODataEnumerable<LineOfDutyCase> _previousCases;
    private int _previousCasesCount;
    private bool _previousCasesLoading;
    private string _previousCasesSearchText = string.Empty;
    private CancellationTokenSource _previousCasesSearchCts = new();
    private int _previousCasesLoadGeneration;
    private int _previousCasesMemberId;
    private readonly HashSet<int> _previousCasesBookmarkedIds = [];
    private readonly HashSet<int> _previousCasesAnimatingIds = [];
    private IList<LineOfDutyCase> _selectedPreviousCase;

    private RadzenDataGrid<WorkflowStateHistory> _trackingGrid;
    private string _trackingSearchText = string.Empty;
    private CancellationTokenSource _trackingSearchCts = new();
    private bool _trackingLoading;
    private IList<WorkflowStateHistory> _selectedTrackingEntry;

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
        _trackingLoading = true;
        StateHasChanged();

        try
        {
            var refreshedCase = await CaseService.GetCaseAsync(CaseId, _cts.Token);
            if (refreshedCase is not null)
            {
                _lineOfDutyCase.WorkflowStateHistories = refreshedCase.WorkflowStateHistories;
            }

            _selectedTrackingEntry = TrackingHistory.FirstOrDefault() is { } first
                ? new List<WorkflowStateHistory> { first }
                : null;
        }
        finally
        {
            _trackingLoading = false;
            StateHasChanged();
        }
    }

    private IEnumerable<WorkflowStateHistory> TrackingHistory
    {
        get
        {
            var items = _lineOfDutyCase?.WorkflowStateHistories;
            if (items is null)
                return Enumerable.Empty<WorkflowStateHistory>();

            IEnumerable<WorkflowStateHistory> result = items;

            if (!string.IsNullOrWhiteSpace(_trackingSearchText))
            {
                var search = _trackingSearchText.Trim();
                result = result.Where(h =>
                    h.WorkflowState.ToDisplayString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    h.Action.ToDisplayString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    h.Status.ToDisplayString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (h.PerformedBy is not null && h.PerformedBy.Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            return result
                .OrderByDescending(h => h.CreatedDate)
                .ThenByDescending(h => h.Id);
        }
    }

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        if (IsNewCase)
        {
            _selectedTabIndex = WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase?.WorkflowState ?? WorkflowState.Draft);

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
            _tabs?.Reload();

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
            _lineOfDutyCase = await CaseService.GetCaseAsync(CaseId, _cts.Token);

            _stateMachine = StateMachineFactory.Create(_lineOfDutyCase);

            _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

            _selectedTabIndex = WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase.WorkflowState);

            try
            {
                _bookmark.IsBookmarked = await CaseService.IsBookmarkedAsync(_lineOfDutyCase.Id, _cts.Token);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to check bookmark status for case {CaseId}", _lineOfDutyCase.Id);
            }

            await LoadPreviousCasesAsync(_lineOfDutyCase.MemberId);

            _selectedTrackingEntry = TrackingHistory.FirstOrDefault() is { } firstTracking
                ? new List<WorkflowStateHistory> { firstTracking }
                : null;

            _loadedCaseId = CaseId;

            // Load auth token for RadzenUpload Authorization header
            try
            {
                var token = await LocalStorage.GetItemAsStringAsync("accessToken");
                _documents.AuthToken = !string.IsNullOrEmpty(token) ? $"Bearer {token}" : string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load auth token for document uploads");
            }

            // No pre-population needed — the DataGrid binds directly to SortedDocuments.
        }
        catch (OperationCanceledException)
        {
            // Component disposed during load — silently ignore
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

            _selectedTabIndex = WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase.WorkflowState);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
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

        var generation = ++_previousCasesLoadGeneration;

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
                count: true,
                cancellationToken: _cts.Token);

            if (generation != _previousCasesLoadGeneration)
            {
                return;
            }

            _previousCases = result?.Value?.AsODataEnumerable();
            _previousCasesCount = result?.Count ?? 0;

            var firstCase = _previousCases?.FirstOrDefault();
            _selectedPreviousCase = firstCase is not null ? new List<LineOfDutyCase> { firstCase } : null;

            _previousCasesBookmarkedIds.Clear();

            if (_previousCases is not null)
            {
                foreach (var lodCase in _previousCases)
                {
                    if (generation != _previousCasesLoadGeneration)
                    {
                        return;
                    }

                    if (await CaseService.IsBookmarkedAsync(lodCase.Id, _cts.Token))
                    {
                        _previousCasesBookmarkedIds.Add(lodCase.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Component disposed — ignore
        }
        catch (Exception ex)
        {
            if (generation == _previousCasesLoadGeneration)
            {
                Logger.LogWarning(ex, "Failed to load previous cases for member {MemberId}", _previousCasesMemberId);
                _previousCases = null;
                _previousCasesCount = 0;
            }
        }
        finally
        {
            if (generation == _previousCasesLoadGeneration)
            {
                _previousCasesLoading = false;
            }
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
            await Task.Delay(500, token);
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
            await Task.Delay(500, token);
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

    private async Task TogglePreviousCaseBookmark(LineOfDutyCase lodCase)
    {
        var isBookmarked = _previousCasesBookmarkedIds.Contains(lodCase.Id);

        if (isBookmarked)
        {
            try
            {
                await CaseService.RemoveBookmarkAsync(lodCase.Id);
                _previousCasesBookmarkedIds.Remove(lodCase.Id);
                await BookmarkCountService.RefreshAsync();
                NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {lodCase.CaseId} removed from bookmarks.", closeOnClick: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to remove bookmark for case {CaseId}", lodCase.Id);
            }
        }
        else
        {
            _previousCasesAnimatingIds.Add(lodCase.Id);
            StateHasChanged();

            try
            {
                await CaseService.AddBookmarkAsync(lodCase.Id);
                _previousCasesBookmarkedIds.Add(lodCase.Id);
                await BookmarkCountService.RefreshAsync();
                NotificationService.Notify(NotificationSeverity.Success, "Bookmark Added", $"Case {lodCase.CaseId} added to bookmarks.", closeOnClick: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to add bookmark for case {CaseId}", lodCase.Id);
            }

            await Task.Delay(800);
            _previousCasesAnimatingIds.Remove(lodCase.Id);
        }
    }

    #endregion

    #region Workflow Actions

    private async Task OnMemberForwardClick(RadzenSplitButtonItem item)
    {
        bool? confirmed = null;

        if (item?.Value == "cancel")
        {
            confirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this line of duty case?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Cancel Case", CancelButtonText = "Don't Cancel Case" });

            if (confirmed != true)
            {
                return;
            }

            Navigation.NavigateTo(NavigatedFromPath, replace: true);

            return;
        }

        if (item?.Value == "revert")
        {
            await OnRevertChanges();

            return;
        }

        if (IsNewCase)
        {
            if (_selectedMemberId == 0)
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Member Required", "Please search for and select a member before starting a case.");
                return;
            }

            confirmed = await DialogService.Confirm(
                "Are you sure you want to start this line of duty case?",
                "Start Line of Duty Case",
                new ConfirmOptions
                {
                    OkButtonText = "Start",
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

                _stateMachine = StateMachineFactory.Create();

                var result = await _stateMachine.FireAsync(lineOfDutyCase, LineOfDutyTrigger.StartLineOfDutyCase);

                if (result.Success)
                {
                    result = await _stateMachine.FireAsync(result.Case, LineOfDutyTrigger.ForwardToMedicalTechnician);
                }

                if (result.Success)
                {
                    _lineOfDutyCase = result.Case;

                    CaseId = _lineOfDutyCase.CaseId;

                    _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                    TakeSnapshots();

                    _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

                    _selectedTabIndex = result.TabIndex;

                    _tabs?.Reload();

                    NotificationService.Notify(
                        NotificationSeverity.Success,
                        "Line of Duty Case Started",
                        $"Case: {_lineOfDutyCase.CaseId} created for: {_lineOfDutyCase.MemberName}.");
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

            return;
        }

        confirmed = await DialogService.Confirm(
            "Are you sure you want to forward the case to the Medical Technician?",
            "Forward to Medical Technician",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Medical Technician...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToMedicalTechnician);

            if (result.Success)
            {
                _lineOfDutyCase = result.Case;

                CaseId = _lineOfDutyCase.CaseId;

                _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                TakeSnapshots();

                _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

                _selectedTabIndex = result.TabIndex;

                NotificationService.Notify(
                    NotificationSeverity.Success,
                    "Line of Duty Case Updated",
                    $"Case: {_lineOfDutyCase.CaseId} updated for: {_lineOfDutyCase.MemberName}.");
            }
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task OnForwardClick(RadzenSplitButtonItem item, LineOfDutyTrigger trigger)
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
            LineOfDutyTrigger.Complete,
            okButtonText: "Complete",
            notifySummary: "Line of Duty Case Completed",
            notifyVerb: "completed");
    }

    private async Task FireWorkflowActionAsync(
        RadzenSplitButtonItem item,
        string confirmMessage,
        string confirmTitle,
        string busyMessage,
        LineOfDutyTrigger forwardTrigger,
        string okButtonText = "Start",
        string notifySummary = "Line of Duty Case Updated",
        string notifyVerb = "updated")
    {
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
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

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
                new ConfirmOptions { OkButtonText = "Start", CancelButtonText = "Cancel" });

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
        return WorkflowTabHelper.IsTabDisabled(tabIndex, _lineOfDutyCase?.WorkflowState ?? WorkflowState.Draft);
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
            5 => TabNames.WingCommander,
            6 => TabNames.AppointingAuthority,
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
        if (_lineOfDutyCase is null)
        {
            return;
        }

        SetIsSaving(true);

        await SetBusyAsync($"Saving {tabName}...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            _lineOfDutyCase = await CaseService.SaveCaseAsync(_lineOfDutyCase, _cts.Token);

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
            StateHasChanged();

            try
            {
                await CaseService.AddBookmarkAsync(_lineOfDutyCase.Id, _cts.Token);
                await BookmarkCountService.RefreshAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to add bookmark for case {CaseId}", _lineOfDutyCase.Id);
                _bookmark.IsBookmarked = false; // Revert on failure
            }

            await Task.Delay(800);
            _bookmark.IsAnimating = false;
        }
        else
        {
            try
            {
                await CaseService.RemoveBookmarkAsync(_lineOfDutyCase.Id, _cts.Token);
                await BookmarkCountService.RefreshAsync(_cts.Token);
                NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {_viewModel?.CaseNumber} removed from bookmarks.", closeOnClick: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to remove bookmark for case {CaseId}", _lineOfDutyCase.Id);
                _bookmark.IsBookmarked = true; // Revert on failure
            }
        }
    }

    private async Task OnAttachFileClick()
    {
        // TODO: Implement file attachment dialog/upload
        await Task.CompletedTask;
    }

    private void OnCreateNewCase()
    {
        Navigation.NavigateTo("/case/new?from=case");
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
        _trackingSearchCts.Cancel();
        _trackingSearchCts.Dispose();
        _documentsSearchCts.Cancel();
        _documentsSearchCts.Dispose();
    }

    #endregion
}
