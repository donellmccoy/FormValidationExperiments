using System.Text.Json;
using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
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
        (TabNames.BoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview),     // 7
        (TabNames.BoardMedicalReview,    WorkflowState.BoardMedicalOfficerReview),        // 8
        (TabNames.BoardLegalReview,      WorkflowState.BoardLegalReview),          // 9
        (TabNames.BoardAdminReview,      WorkflowState.BoardAdministratorReview),          // 10
    ];

    private static readonly object[] _dutyStatusOptions = Enum.GetValues<DutyStatus>()
        .Select(s => new { Text = s.ToDisplayString(), Value = (DutyStatus?)s })
        .ToArray();

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

    private LodStateMachine _stateMachine;

    private IEnumerable<LodTrigger> _permittedTriggers = [];

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

    private async Task RefreshPermittedTriggersAsync()
    {
        _permittedTriggers = _stateMachine is not null
            ? await _stateMachine.GetPermittedTriggersAsync()
            : [];
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
        return HandleWorkflowActionAsync(WorkflowState.BoardMedicalTechnicianReview, item);
    }

    private Task OnBoardMedForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.BoardMedicalOfficerReview, item);
    }

    private Task OnBoardLegalForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.BoardLegalReview, item);
    }

    private Task OnBoardAdminForwardClick(RadzenSplitButtonItem item)
    {
        return HandleWorkflowActionAsync(WorkflowState.BoardAdministratorReview, item);
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
            transition.Trigger,
            transition.TargetState,
            transition.ConfirmMessage,
            transition.ConfirmTitle,
            transition.OkButtonText,
            transition.BusyMessage,
            transition.Severity,
            transition.NotifySummary,
            transition.NotifyDetail);
    }

    private async Task OnMemberForwardClick(LodTrigger trigger, RadzenSplitButtonItem item)
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

        await ChangeWorkflowStateAsync(
            trigger,
            WorkflowState.MedicalTechnicianReview,
            "Are you sure you want to forward this case to the Medical Technician?",
            "Confirm Forward", "Forward",
            "Forwarding to Medical Technician...",
            NotificationSeverity.Success, "Forwarded to Medical Technician",
            "Case has been forwarded to the Medical Technician for review.");
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
    /// <summary>
    /// UI shell: confirms with user, fires the state machine trigger (which handles all DB
    /// persistence via callbacks), re-fetches the case, rebuilds the SM, and updates the UI.
    /// </summary>
    private async Task ChangeWorkflowStateAsync(
        LodTrigger trigger,
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
            await _stateMachine.FireAsync(trigger);

            // Re-fetch the full case including WorkflowStateHistories
            _lodCase = await CaseService.GetCaseAsync(CaseId, _cts.Token);

            if (_lodCase is not null)
            {
                _stateMachine.UpdateCase(_lodCase);
                await RefreshPermittedTriggersAsync();
            }

            _currentStepIndex = WorkflowSidebar.ApplyWorkflowState(_workflowSteps, targetState, _lodCase);
            _selectedTabIndex = GetTabIndexForState(targetState);
            NotificationService.Notify(severity, notifySummary, notifyDetail);
        }
        catch (Exception ex)
        {
            NotificationService.Notify(NotificationSeverity.Error, "State Change Failed", ex.Message);

            // Re-sync SM with persisted state to avoid SM/DB drift
            _lodCase = await CaseService.GetCaseAsync(CaseId, _cts.Token);
            if (_lodCase is not null)
            {
                _stateMachine = LodStateMachineFactory.Create(_lodCase, CaseService);
                await RefreshPermittedTriggersAsync();
            }
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private Task CancelInvestigationAsync()
    {
        return ChangeWorkflowStateAsync(
            LodTrigger.Cancel,
            WorkflowState.Cancelled,
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
                var newCase = LineOfDutyCaseFactory.Create(_selectedMemberId);

                LineOfDutyCaseMapper.ApplyToCase(_viewModel, newCase);

                newCase.AddHistoryEntry(WorkflowStateHistoryFactory.CreateInitialHistory(0, WorkflowState.MemberInformationEntry));

                var saved = await CaseService.SaveCaseAsync(newCase, _cts.Token);

                // Record workflow history entry
                //await CaseService.AddHistoryEntryAsync(WorkflowStateHistoryFactory.CreateInitialHistory(saved.Id, WorkflowState.MemberInformationEntry, saved.CreatedDate), _cts.Token);

                CaseId = saved.CaseId;

                // Reload the full case — the POST response omits navigation properties
                // (TimelineSteps, WorkflowStateHistories, etc.)
                _lodCase = await CaseService.GetCaseAsync(saved.CaseId, _cts.Token) ?? saved;

                _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lodCase);

                _currentStepIndex = WorkflowSidebar.ApplyWorkflowState(_workflowSteps, _lodCase.WorkflowState, _lodCase);
                _selectedTabIndex = GetTabIndexForState(_lodCase.WorkflowState);

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

        var timelineStep = timelineSteps.ElementAt(_currentStepIndex);

        await SetBusyAsync("Applying digital signature...");

        try
        {
            var signed = await CaseService.SignTimelineStepAsync(timelineStep.Id, _cts.Token);

            timelineStep.SignedDate = signed.SignedDate;
            timelineStep.SignedBy = signed.SignedBy;

            // Record signed history entry so the sidebar shows the SignedDate immediately
            var historyEntry = await CaseService.AddHistoryEntryAsync(
                WorkflowStateHistoryFactory.CreateSigned(_lodCase.Id, _lodCase.WorkflowState, CurrentStep?.StartDate, signed.SignedDate, signed.SignedBy),
                _cts.Token);

            _lodCase.AddHistoryEntry(historyEntry);

            _currentStepIndex = WorkflowSidebar.ApplyWorkflowState(_workflowSteps, _lodCase.WorkflowState, _lodCase);
            _selectedTabIndex = GetTabIndexForState(_lodCase.WorkflowState);

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

        SetIsSaving(true);

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
            SetIsSaving(false);

            await SetBusyAsync(isBusy: false);
        }
    }

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
            (_workflowSteps, _currentStepIndex) = WorkflowSidebar.InitializeSteps(_lodCase);
            _selectedTabIndex = GetTabIndexForState(_lodCase?.WorkflowState ?? WorkflowState.MemberInformationEntry);
            TakeSnapshots();
        }
        else
        {
            await LoadCaseAsync();

            if (_lodCase is not null)
            {
                _stateMachine = LodStateMachineFactory.Create(_lodCase, CaseService);
                await RefreshPermittedTriggersAsync();
            }
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
                (_workflowSteps, _currentStepIndex) = WorkflowSidebar.InitializeSteps(_lodCase);
                _selectedTabIndex = GetTabIndexForState(WorkflowState.MemberInformationEntry);

                return;
            }

            _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lodCase);

            (_workflowSteps, _currentStepIndex) = WorkflowSidebar.InitializeSteps(_lodCase);
            _selectedTabIndex = GetTabIndexForState(_lodCase.WorkflowState);

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

            (_workflowSteps, _currentStepIndex) = WorkflowSidebar.InitializeSteps(_lodCase);
            _selectedTabIndex = GetTabIndexForState(_lodCase?.WorkflowState ?? WorkflowState.MemberInformationEntry);
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
        _currentStepIndex = WorkflowSidebar.ApplyWorkflowState(_workflowSteps, step.WorkflowState, _lodCase);
        _selectedTabIndex = GetTabIndexForState(step.WorkflowState);
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
            var newCase = LineOfDutyCaseFactory.Create(_selectedMemberId);

            LineOfDutyCaseMapper.ApplyToCase(_viewModel, newCase);

            var saved = await CaseService.SaveCaseAsync(newCase, _cts.Token);

            saved.WorkflowState = WorkflowState.MedicalTechnicianReview;

            saved.AddHistoryEntry(WorkflowStateHistoryFactory.CreateCompleted(saved.Id, WorkflowState.MemberInformationEntry, saved.CreatedDate));
            saved.AddHistoryEntry(WorkflowStateHistoryFactory.CreateInitialHistory(saved.Id, WorkflowState.MedicalTechnicianReview));

            await CaseService.SaveCaseAsync(saved, _cts.Token);

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
