using System.Text.Json;
using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using ECTSystem.Web.Services;
using ECTSystem.Shared.ViewModels;
using ECTSystem.Web.Shared;
using Radzen;
using Radzen.Blazor;
using Radzen.Blazor.Rendering;

namespace ECTSystem.Web.Pages;

public partial class EditCase : ComponentBase, IDisposable
{
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

    private static readonly (string TabName, WorkflowState State)[] _workflowTabMap =
    [
        (TabNames.MemberInformation,     WorkflowState.MemberInformationEntry),    // 0
        (TabNames.MedicalTechnician,     WorkflowState.MedicalTechnicianReview),   // 1
        (TabNames.MedicalOfficer,        WorkflowState.MedicalOfficerReview),      // 2
        (TabNames.UnitCommander,         WorkflowState.UnitCommanderReview),       // 3
        (TabNames.WingJudgeAdvocate,     WorkflowState.WingJudgeAdvocateReview),   // 4
        (TabNames.WingCommander,         WorkflowState.WingCommanderReview),       // 5
        (TabNames.AppointingAuthority,   WorkflowState.AppointingAuthorityReview), // 6
        (TabNames.BoardTechnicianReview, WorkflowState.BoardTechnicianReview),     // 7
        (TabNames.BoardMedicalReview,    WorkflowState.BoardMedicalReview),        // 8
        (TabNames.BoardLegalReview,      WorkflowState.BoardLegalReview),          // 9
        (TabNames.BoardAdminReview,      WorkflowState.BoardAdminReview),          // 10
    ];

    [Inject]
    private IDataService CaseService { get; set; }

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

    [Parameter]
    public string CaseId { get; set; }

    [SupplyParameterFromQuery(Name = "from")]
    public string FromPage { get; set; }

    private bool IsNewCase
    {
        get
        {
            return string.IsNullOrEmpty(CaseId);
        }
    }

    private bool IsFromBookmarks
    {
        get
        {
            return string.Equals(FromPage, "bookmarks", StringComparison.OrdinalIgnoreCase);
        }
    }

    private readonly PageOperationState _page = new();

    private readonly BookmarkUiState _bookmark = new();

    private readonly DocumentUiState _documents = new();

    private readonly CancellationTokenSource _cts = new();

    private LineOfDutyCase _lodCase;

    private int _selectedTabIndex;

    private int _currentStepIndex;

    private int _selectedMemberId;

    private LineOfDutyViewModel _viewModel = new()
    {
        CaseNumber = "Pending...",
        MemberName = "Pending...",
        Component = "Pending...",
        Rank = "Pending...",
        Grade = "Pending...",
        Unit = "Pending...",
        DateOfInjury = "Pending...",
        Status = "New"
    };


    private RadzenTemplateForm<LineOfDutyViewModel> _medicalForm;

    private IReadOnlyList<TrackableModel> AllFormModels
    {
        get
        {
            return [_viewModel];
        }
    }

    private bool HasAnyChanges
    {
        get
        {
            return _viewModel.IsDirty;
        }
    }

    private int NotificationCount
    {
        get
        {
            return _lodCase?.Notifications?.Count ?? 0;
        }
    }

    private List<WorkflowStep> _workflowSteps = [];

    private WorkflowStep CurrentStep
    {
        get
        {
            return _workflowSteps.Count > 0 && _currentStepIndex >= 0 && _currentStepIndex < _workflowSteps.Count ? _workflowSteps[_currentStepIndex] : null;
        }
    }

    private Task OnBoardTechForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.BoardTechnicianReview, item);
    }

    private Task OnBoardMedForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.BoardMedicalReview, item);
    }

    private Task OnBoardLegalForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.BoardLegalReview, item);
    }

    private Task OnBoardAdminForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.BoardAdminReview, item);
    }

    private Task OnBoardCompleteClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.Completed, item);
    }

    private async Task HandleWorkflowActionAsync(WorkflowState sourceState, RadzenSplitButtonItem item)
    {
        if (item?.Value == "cancel")
        {
            await CancelInvestigationAsync();
            return;
        }

        var action = item?.Value ?? "default";

        if (!SourceTransitions.TryGetValue((sourceState, action), out var transition)
            && !SharedTransitions.TryGetValue(action, out transition)
            && !SourceTransitions.TryGetValue((sourceState, "default"), out transition))
        {
            return;
        }

        await ChangeWorkflowStateAsync(
            transition.TargetState,
            transition.ConfirmMessage,
            transition.ConfirmTitle,
            transition.OkButtonText,
            transition.BusyMessage,
            transition.Severity,
            transition.NotifySummary,
            transition.NotifyDetail);
    }

    private async Task OnMemberForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "cancel")
        {
            await CancelInvestigationAsync();
            return;
        }

        if (_lodCase is null)
        {
            await OnStartLod();
            return;
        }

        await HandleWorkflowActionAsync(WorkflowState.MemberInformationEntry, item);
    }

    private Task OnMedTechForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.MedicalTechnicianReview, item);
    }

    private Task OnMedicalForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.MedicalOfficerReview, item);
    }

    private Task OnCommanderForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.UnitCommanderReview, item);
    }

    private Task OnWingJAForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.WingJudgeAdvocateReview, item);
    }

    private Task OnLegalForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.WingCommanderReview, item);
    }

    private Task OnWingForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.AppointingAuthorityReview, item);
    }

    private async Task ConfirmAndNotifyAsync(
        string confirmMessage, 
        string confirmTitle, 
        string okButtonText,
        string busyMessage, 
        NotificationSeverity severity,
        string notifySummary, 
        string notifyDetail,
        string cancelButtonText = "Cancel")
    {
        var confirmed = await DialogService.Confirm(confirmMessage, confirmTitle,
            new ConfirmOptions { OkButtonText = okButtonText, CancelButtonText = cancelButtonText });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync(busyMessage);

        try
        {
            NotificationService.Notify(severity, notifySummary, notifyDetail);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    /// <summary>
    /// Syncs the workflow sidebar step statuses and <see cref="_currentStepIndex"/> to match
    /// <paramref name="state"/>. Uses WorkflowStateHistory entries as the primary source;
    /// falls back to positional TimelineStep data for cases predating the history feature.
    /// Does NOT persist the state change to the database.
    /// </summary>
    private void ApplyWorkflowState(WorkflowState state)
    {
        // Clamp to valid range — DB rows that predate the WorkflowState migration have int value 0
        var stateInt = (int)state < 1 ? 1 : (int)state > _workflowSteps.Count ? _workflowSteps.Count : (int)state;
        _currentStepIndex = stateInt - 1;
        _selectedTabIndex = GetTabIndexForState((WorkflowState)stateInt);

        // Primary source: latest history entry per WorkflowState (highest Id = most recent)
        var historyByState = _lodCase?.WorkflowStateHistories?
            .GroupBy(h => h.WorkflowState)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Id).First())
            ?? [];

        // Fallback: positional TimelineStep data (backward compatibility with seeded cases)
        var timelineByIndex = _lodCase?.TimelineSteps
            .Select((ts, i) => (Index: i + 1, Step: ts))
            .ToDictionary(x => x.Index, x => x.Step)
            ?? [];

        foreach (var step in _workflowSteps)
        {
            if (historyByState.TryGetValue(step.WorkflowState, out var history))
            {
                step.Status         = history.Status;
                step.StartDate      = history.StartDate;
                step.SignedDate     = history.SignedDate;
                step.SignedBy       = history.SignedBy ?? string.Empty;
                step.CompletedBy    = history.PerformedBy;
                step.StatusText     = history.Status == WorkflowStepStatus.Completed ? "Completed" : string.Empty;
                step.CompletedDate  = history.Status == WorkflowStepStatus.Completed ? history.OccurredAt : null;
                step.CompletionDate = history.Status == WorkflowStepStatus.Completed ? history.OccurredAt.ToString("MM/dd/yyyy h:mm tt") : string.Empty;
            }
            else
            {
                // No history — fall back to positional timeline data
                var timeline = timelineByIndex.GetValueOrDefault(step.Number);

                if (step.Number < stateInt)
                {
                    step.Status = WorkflowStepStatus.Completed;
                    step.StatusText = "Completed";
                    if (string.IsNullOrEmpty(step.CompletionDate))
                    {
                        step.CompletionDate = timeline?.CompletionDate?.ToString("MM/dd/yyyy h:mm tt") ?? DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
                    }
                }
                else if (step.Number == stateInt)
                {
                    step.Status = WorkflowStepStatus.InProgress;
                    step.StatusText = string.Empty;
                    step.CompletionDate = string.Empty;
                }
                else
                {
                    step.Status = WorkflowStepStatus.Pending;
                    step.StatusText = string.Empty;
                    step.CompletionDate = string.Empty;
                }

                step.StartDate      = timeline?.StartDate;
                step.SignedDate     = timeline?.SignedDate;
                step.SignedBy       = timeline?.SignedBy ?? string.Empty;
                step.CompletedDate  = timeline?.CompletionDate;
                step.CompletedBy    = timeline?.ModifiedBy ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Maps a <see cref="WorkflowState"/> to its 0-based tab index in the RadzenTabs control.
    /// Tab order: Member(0) · Med Tech(1) · Med Officer(2) · Unit CC(3) · Wing JA(4) · Wing CC(5) · Appointing Auth(6) · Board Tech(7) · Board Med(8) · Board Legal(9) · Board Admin(10)
    /// </summary>
    private static int GetTabIndexForState(WorkflowState state)
    {
        for (var i = 0; i < _workflowTabMap.Length; i++)
        {
            if (_workflowTabMap[i].State == state)
            {
                return i;
            }
        }

        // Completed maps to the last workflow tab; unknown states default to first
        return state == WorkflowState.Completed
            ? _workflowTabMap.Length - 1
            : 0;
    }

    /// <summary>
    /// Returns true if the tab at <paramref name="tabIndex"/> should be disabled.
    /// Workflow tabs (0–10): enabled for the current state and all prior states; disabled for future states.
    /// Always-on tabs (11+): Case Dialogue, Notifications, and Documents — never disabled.
    /// </summary>
    private bool IsTabDisabled(int tabIndex)
    {
        if (tabIndex >= 11)
        {
            return false;
        }

        var state = _lodCase?.WorkflowState ?? WorkflowState.MemberInformationEntry;
        return tabIndex > GetTabIndexForState(state);
    }

    /// <summary>
    /// Confirms with the user, advances the LOD <see cref="LineOfDutyCase.WorkflowState"/> to
    /// <paramref name="targetState"/>, persists the change, updates the sidebar, and notifies.
    /// </summary>
    private async Task ChangeWorkflowStateAsync(
        WorkflowState targetState,
        string confirmMessage,
        string confirmTitle,
        string okButtonText,
        string busyMessage,
        NotificationSeverity severity,
        string notifySummary,
        string notifyDetail,
        string cancelButtonText = "Cancel")
    {
        if (_lodCase is null)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(confirmMessage, confirmTitle,
            new ConfirmOptions { OkButtonText = okButtonText, CancelButtonText = cancelButtonText });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync(busyMessage);

        try
        {
            var sourceState  = _lodCase.WorkflowState;
            var isForward    = (int)targetState > (int)sourceState;
            var now          = DateTime.UtcNow;
            var outgoingStep = _workflowSteps.FirstOrDefault(s => s.WorkflowState == sourceState);

            _lodCase.WorkflowState = targetState;
            await CaseService.SaveCaseAsync(_lodCase, _cts.Token);

            // Record outgoing step history snapshot
            if (outgoingStep is not null)
            {
                await CaseService.AddHistoryEntryAsync(new WorkflowStateHistory
                {
                    LineOfDutyCaseId = _lodCase.Id,
                    WorkflowState    = sourceState,
                    Action           = isForward ? TransitionAction.Completed : TransitionAction.Returned,
                    Status           = isForward ? WorkflowStepStatus.Completed : WorkflowStepStatus.Pending,
                    StartDate        = outgoingStep.StartDate,
                    SignedDate       = isForward ? now : outgoingStep.SignedDate,
                    SignedBy         = isForward ? string.Empty : (string.IsNullOrEmpty(outgoingStep.SignedBy) ? null : outgoingStep.SignedBy),
                    OccurredAt       = now,
                    PerformedBy      = string.Empty
                }, _cts.Token);
            }

            // Record incoming step history snapshot (fresh start)
            await CaseService.AddHistoryEntryAsync(new WorkflowStateHistory
            {
                LineOfDutyCaseId = _lodCase.Id,
                WorkflowState    = targetState,
                Action           = TransitionAction.Entered,
                Status           = WorkflowStepStatus.InProgress,
                StartDate        = now,
                OccurredAt       = now,
                PerformedBy      = string.Empty
            }, _cts.Token);

            // Start the incoming (new current) timeline step
            var targetIndex = (int)targetState - 1;
            var timelineSteps = _lodCase.TimelineSteps?.ToList();
            if (timelineSteps is not null && targetIndex >= 0 && targetIndex < timelineSteps.Count)
            {
                var incomingStep = timelineSteps[targetIndex];
                if (!incomingStep.StartDate.HasValue)
                {
                    await CaseService.StartTimelineStepAsync(incomingStep.Id, _cts.Token);
                }
            }

            // Re-fetch the full case including WorkflowStateHistories
            _lodCase = await CaseService.GetCaseAsync(CaseId, _cts.Token);
            ApplyWorkflowState(targetState);
            NotificationService.Notify(severity, notifySummary, notifyDetail);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "State Change Failed", ex.Message);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private Task CancelInvestigationAsync()
    {
        return ConfirmAndNotifyAsync(
            "Are you sure you want to cancel this investigation?",
            "Confirm Cancellation", 
            "Yes, Cancel",
            "Cancelling investigation...",
            NotificationSeverity.Warning, 
            "Investigation Cancelled",
            "The LOD investigation has been cancelled.", 
            "No");
    }

    private async Task OnDigitallySign()
    {
        // If LOD case doesn't exist yet, create it first (without advancing workflow)
        if (_lodCase is null)
        {
            if (_selectedMemberId <= 0)
            {
                NotificationService.Notify(NotificationSeverity.Warning, "No Member Selected",
                    "Please search for and select a member before signing.");
                return;
            }

            var confirmed = await DialogService.Confirm(
                "This will create the LOD case and digitally sign the Member Information section. Continue?",
                "Create and Sign",
                new ConfirmOptions { OkButtonText = "Create & Sign", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Creating LOD case...");

            try
            {
                var newCase = new LineOfDutyCase
                {
                    CaseId = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                    MemberId = _selectedMemberId,
                    InitiationDate = DateTime.UtcNow,
                    IncidentDate = DateTime.UtcNow,
                    WorkflowState = WorkflowState.MemberInformationEntry
                };

                LineOfDutyCaseMapper.ApplyToCase(_viewModel, newCase);

                var saved = await CaseService.SaveCaseAsync(newCase, _cts.Token);

                // Record workflow history entry
                var historyEntry = await CaseService.AddHistoryEntryAsync(new WorkflowStateHistory
                {
                    LineOfDutyCaseId = saved.Id,
                    WorkflowState    = WorkflowState.MemberInformationEntry,
                    Action           = TransitionAction.Entered,
                    Status           = WorkflowStepStatus.InProgress,
                    StartDate        = saved.CreatedDate,
                    OccurredAt       = saved.CreatedDate, 
                    CreatedBy        = saved.CreatedBy,
                    CreatedDate      = saved.CreatedDate,
                    ModifiedBy       = saved.ModifiedBy,
                    ModifiedDate     = saved.ModifiedDate,
                    PerformedBy      = string.Empty
                }, _cts.Token);

                _lodCase = saved;
                CaseId = saved.CaseId;

                _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(saved);

                ApplyWorkflowState(saved.WorkflowState);

                TakeSnapshots();

                Navigation.NavigateTo($"/case/{saved.CaseId}", replace: true);
            }
            catch (Exception ex)
            {
                NotificationService.Notify(NotificationSeverity.Error, "Creation Failed", ex.Message);
            }
            finally            
            {
                await SetBusyAsync(isBusy: false);
            }
            return;
        }

        var confirmed2 = await DialogService.Confirm(
            "Are you sure you want to digitally sign this section?",
            "Confirm Digital Signature",
            new ConfirmOptions { OkButtonText = "Sign", CancelButtonText = "Cancel" });

        if (confirmed2 != true)
        {
            return;
        }

        var timelineSteps = _lodCase?.TimelineSteps;

        if (_currentStepIndex >= timelineSteps.Count)
        {
            NotificationService.Notify(
                NotificationSeverity.Warning, 
                "No Timeline Step",
                "No timeline step found for the current workflow step.");

            return;
        }

        var timelineStep = timelineSteps[_currentStepIndex];

        await SetBusyAsync("Applying digital signature...");

        try
        {
            var signed = await CaseService.SignTimelineStepAsync(timelineStep.Id, _cts.Token);

            timelineStep.SignedDate = signed.SignedDate;
            timelineStep.SignedBy   = signed.SignedBy;

            // Record signed history entry so the sidebar shows the SignedDate immediately
            var historyEntry = await CaseService.AddHistoryEntryAsync(new WorkflowStateHistory
            {
                LineOfDutyCaseId = _lodCase.Id,
                WorkflowState    = _lodCase.WorkflowState,
                Action           = TransitionAction.Signed,
                Status           = WorkflowStepStatus.InProgress,
                StartDate        = CurrentStep?.StartDate,
                SignedDate       = signed.SignedDate,
                SignedBy         = signed.SignedBy,
                OccurredAt       = DateTime.UtcNow,
                PerformedBy      = string.Empty
            }, _cts.Token);

            _lodCase.WorkflowStateHistories ??= [];
            _lodCase.WorkflowStateHistories.Add(historyEntry);

            ApplyWorkflowState(_lodCase.WorkflowState);

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Digitally Signed",
                Detail = "Section has been digitally signed.",
                Duration = 3000
            });
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Signing Failed", ex.Message);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task SaveCurrentTabAsync(string source)
    {
        if (_page.IsSaving)
        {
            return;
        }

        _page.IsSaving = true;
        await SetBusyAsync("Saving...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lodCase);

            // Save the entity
            _lodCase = await CaseService.SaveCaseAsync(_lodCase, _cts.Token);

            // Refresh the view model from the saved entity and re-snapshot
            _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lodCase);
            TakeSnapshots();

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Saved",
                Detail = $"{source} data saved successfully.",
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
            _page.IsSaving = false;
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task SetBusyAsync(string message = "Working...", bool? isBusy = true)
    {
        _page.BusyMessage = message;
        _page.IsBusy = isBusy.GetValueOrDefault(true);
        await InvokeAsync(StateHasChanged);
    }

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

    protected override async Task OnInitializedAsync()
    {
        if (IsNewCase)
        {
            InitializeWorkflowSteps();
            TakeSnapshots();
        }
        else
        {
            await LoadCaseAsync();
        }

        _page.IsLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && IsNewCase && _memberSearchTextBox is not null)
        {
            await _memberSearchTextBox.Element.FocusAsync();
        }
    }

    private async Task LoadCaseAsync()
    {
        await SetBusyAsync("Loading case...");

        try
        {
            _lodCase = await CaseService.GetCaseAsync(CaseId, _cts.Token);

            if (_lodCase is null)
            {
                InitializeWorkflowSteps();

                return;
            }

            _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lodCase);

            InitializeWorkflowSteps();

            TakeSnapshots();

            // Check if this case is bookmarked
            if (_lodCase.Id > 0)
            {
                try
                {
                    _bookmark.IsBookmarked = await CaseService.IsBookmarkedAsync(_lodCase.Id, _cts.Token);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to check bookmark status for case {CaseId}", _lodCase.Id);
                }
            }
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

            InitializeWorkflowSteps();
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private void TakeSnapshots()
    {
        foreach (var model in AllFormModels)
        {
            model.TakeSnapshot(JsonOptions);
        }
    }

    private void InitializeWorkflowSteps()
    {
        _workflowSteps =
        [
            new() { Number = 1,  Name = "Enter Member Information",  Icon = "flag",                 Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.MemberInformationEntry,    Description = "Enter member identification and incident details to initiate the LOD case." },
            new() { Number = 2,  Name = "Medical Technician Review", Icon = "person",               Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.MedicalTechnicianReview,   Description = "Medical technician reviews the injury/illness and documents clinical findings." },
            new() { Number = 3,  Name = "Medical Officer Review",    Icon = "medical_services",     Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.MedicalOfficerReview,      Description = "Medical officer reviews the technician's findings and provides a clinical assessment." },
            new() { Number = 4,  Name = "Unit CC Review",            Icon = "edit_document",        Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.UnitCommanderReview,       Description = "Unit commander reviews the case and submits a recommendation for the LOD determination." },
            new() { Number = 5,  Name = "Wing JA Review",            Icon = "gavel",                Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.WingJudgeAdvocateReview,   Description = "Wing Judge Advocate reviews the case for legal sufficiency and compliance." },
            new() { Number = 6,  Name = "Appointing Authority",      Icon = "verified_user",        Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.AppointingAuthorityReview, Description = "Appointing authority reviews the case and issues a formal LOD determination." },
            new() { Number = 7,  Name = "Wing CC Review",            Icon = "stars",                Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.WingCommanderReview,       Description = "Wing commander reviews the case and renders a preliminary LOD determination." },
            new() { Number = 8,  Name = "Board Technician Review",   Icon = "rate_review",          Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.BoardTechnicianReview,     Description = "Board medical technician reviews the case file for completeness and accuracy." },
            new() { Number = 9,  Name = "Board Medical Review",      Icon = "medical_services",     Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.BoardMedicalReview,        Description = "Board medical officer reviews all medical evidence and provides a formal assessment." },
            new() { Number = 10, Name = "Board Legal Review",        Icon = "gavel",                Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.BoardLegalReview,          Description = "Board legal counsel reviews the case for legal sufficiency before final decision." },
            new() { Number = 11, Name = "Board Admin Review",        Icon = "admin_panel_settings", Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.BoardAdminReview,          Description = "Board administrative officer finalizes the case package and prepares the formal determination." },
            new() { Number = 12, Name = "Completed",                 Icon = "check_circle",         Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.Completed,                Description = "LOD determination has been finalized and the case is closed." }
        ];

        ApplyWorkflowState(_lodCase?.WorkflowState ?? WorkflowState.MemberInformationEntry);
    }

    private async Task OnFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.MedicalTechnician);
    }

    private async Task OnMemberFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.MemberInformation);
    }

    private async Task OnCommanderFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.UnitCommander);
    }

    private async Task OnWingJAFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.WingJudgeAdvocate);
    }

    private async Task OnWingCommanderFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.WingCommander);
    }

    private async Task OnMedTechFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.MedicalTechnician);
    }

    private async Task OnAppointingAuthorityFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.AppointingAuthority);
    }

    private async Task OnBoardFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.BoardTechnicianReview);
    }

    private async Task OnBookmarkClick()
    {
        if (_lodCase?.Id is null or 0)
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
                await CaseService.AddBookmarkAsync(_lodCase.Id, _cts.Token);
                await BookmarkCountService.RefreshAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to add bookmark for case {CaseId}", _lodCase.Id);
                _bookmark.IsBookmarked = false; // Revert on failure
            }

            await Task.Delay(800);
            _bookmark.IsAnimating = false;
        }
        else
        {
            try
            {
                await CaseService.RemoveBookmarkAsync(_lodCase.Id, _cts.Token);
                await BookmarkCountService.RefreshAsync(_cts.Token);
                NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {_viewModel?.CaseNumber} removed from bookmarks.", closeOnClick: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to remove bookmark for case {CaseId}", _lodCase.Id);
                _bookmark.IsBookmarked = true; // Revert on failure
            }
        }
    }

    private async Task OnAttachFileClick()
    {
        // TODO: Implement file attachment dialog/upload
        await Task.CompletedTask;
    }

    private void OnStepSelected(WorkflowStep step)
    {
        ApplyWorkflowState(step.WorkflowState);
    }

    private async Task OnStartLod()
    {
        if (_selectedMemberId <= 0)
        {
            NotificationService.Notify(NotificationSeverity.Warning, "No Member Selected",
                "Please search for and select a member before forwarding.");
            return;
        }

        var confirmed = await DialogService.Confirm(
            "Are you sure you want to start the LOD process?",
            "Start LOD",
            new ConfirmOptions { OkButtonText = "Start", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Creating LOD case...");

        try
        {
            var newCase = new LineOfDutyCase
            {
                CaseId = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                MemberId = _selectedMemberId,
                WorkflowState = WorkflowState.MemberInformationEntry,
                InitiationDate = DateTime.UtcNow,
                IncidentDate = DateTime.UtcNow
            };

            LineOfDutyCaseMapper.ApplyToCase(_viewModel, newCase);

            var saved = await CaseService.SaveCaseAsync(newCase, _cts.Token);

            // Advance to the first review state and persist
            saved.WorkflowState = WorkflowState.MedicalTechnicianReview;
            saved = await CaseService.SaveCaseAsync(saved, _cts.Token);

            // Record workflow history: MemberInformationEntry completed, MedicalTechnicianReview entered
            var startNow = DateTime.UtcNow;
            await CaseService.AddHistoryEntryAsync(new WorkflowStateHistory
            {
                LineOfDutyCaseId = saved.Id,
                WorkflowState    = WorkflowState.MemberInformationEntry,
                Action           = TransitionAction.Completed,
                Status           = WorkflowStepStatus.Completed,
                StartDate        = saved.CreatedDate,
                SignedDate       = startNow,
                OccurredAt       = startNow,
                PerformedBy      = string.Empty
            }, _cts.Token);

            await CaseService.AddHistoryEntryAsync(new WorkflowStateHistory
            {
                LineOfDutyCaseId = saved.Id,
                WorkflowState    = WorkflowState.MedicalTechnicianReview,
                Action           = TransitionAction.Entered,
                Status           = WorkflowStepStatus.InProgress,
                StartDate        = startNow,
                OccurredAt       = startNow,
                PerformedBy      = string.Empty
            }, _cts.Token);

            _lodCase = saved;
            CaseId = saved.CaseId;
            _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(saved);
            ApplyWorkflowState(saved.WorkflowState);

            TakeSnapshots();

            NotificationService.Notify(NotificationSeverity.Success, "LOD Started", $"Case {saved.CaseId} created for {saved.MemberName}.");

            Navigation.NavigateTo($"/case/{saved.CaseId}", replace: true);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "Create Failed", ex.Message);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task OnSplitButtonClick(RadzenSplitButtonItem item)
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

        await SaveCurrentTabAsync(source);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
    }
}
