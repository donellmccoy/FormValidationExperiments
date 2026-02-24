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

    private static readonly (string TabName, LineOfDutyWorkflowState State)[] WorkflowTabMap =
    [
        (TabNames.MemberInformation,     LineOfDutyWorkflowState.MemberInformationEntry),    // 0
        (TabNames.MedicalTechnician,     LineOfDutyWorkflowState.MedicalTechnicianReview),   // 1
        (TabNames.MedicalOfficer,        LineOfDutyWorkflowState.MedicalOfficerReview),      // 2
        (TabNames.UnitCommander,         LineOfDutyWorkflowState.UnitCommanderReview),       // 3
        (TabNames.WingJudgeAdvocate,     LineOfDutyWorkflowState.WingJudgeAdvocateReview),   // 4
        (TabNames.WingCommander,         LineOfDutyWorkflowState.WingCommanderReview),       // 5
        (TabNames.AppointingAuthority,   LineOfDutyWorkflowState.AppointingAuthorityReview), // 6
        (TabNames.BoardTechnicianReview, LineOfDutyWorkflowState.BoardTechnicianReview),     // 7
        (TabNames.BoardMedicalReview,    LineOfDutyWorkflowState.BoardMedicalReview),        // 8
        (TabNames.BoardLegalReview,      LineOfDutyWorkflowState.BoardLegalReview),          // 9
        (TabNames.BoardAdminReview,      LineOfDutyWorkflowState.BoardAdminReview),          // 10
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

    private MemberInfoFormModel _memberFormModel = new();

    private MedicalAssessmentFormModel _medicalFormModel = new();

    private RadzenTemplateForm<MedicalAssessmentFormModel> _medicalForm;

    private UnitCommanderFormModel _commanderFormModel = new();

    private WingJudgeAdvocateFormModel _wingJAFormModel = new();

    private WingCommanderFormModel _wingCommanderFormModel = new();

    private MedicalTechnicianFormModel _medTechFormModel = new();

    private AppointingAuthorityFormModel _appointingAuthorityFormModel = new();

    private BoardTechnicianFormModel _boardTechFormModel = new();

    private BoardMedicalFormModel _boardMedFormModel = new();

    private BoardLegalFormModel _boardLegalFormModel = new();

    private BoardAdminFormModel _boardAdminFormModel = new();

    private CaseDialogueFormModel _caseDialogueFormModel = new();

    private CaseNotificationsFormModel _caseNotificationsFormModel = new();

    private CaseDocumentsFormModel _caseDocumentsFormModel = new();

    private CaseInfoModel _caseInfo = new()
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

    private IReadOnlyList<TrackableModel> AllFormModels
    {
        get
        {
            return [_memberFormModel, _medicalFormModel, _commanderFormModel, _wingJAFormModel, _wingCommanderFormModel, _medTechFormModel, _appointingAuthorityFormModel, _boardTechFormModel, _boardMedFormModel, _boardLegalFormModel, _boardAdminFormModel, _caseDialogueFormModel, _caseNotificationsFormModel, _caseDocumentsFormModel];
        }
    }

    private bool HasAnyChanges
    {
        get
        {
            return AllFormModels.Any(m => m.IsDirty);
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

    private Task OnBoardTechForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.BoardTechnicianReview, item);

    private Task OnBoardMedForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.BoardMedicalReview, item);

    private Task OnBoardLegalForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.BoardLegalReview, item);

    private Task OnBoardAdminForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.BoardAdminReview, item);

    private Task OnBoardCompleteClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.Completed, item);

    private async Task HandleWorkflowActionAsync(LineOfDutyWorkflowState sourceState, RadzenSplitButtonItem item)
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

    private Task OnMemberForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.MemberInformationEntry, item);

    private Task OnMedTechForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.MedicalTechnicianReview, item);

    private Task OnMedicalForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.MedicalOfficerReview, item);

    private Task OnCommanderForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.UnitCommanderReview, item);

    private Task OnWingJAForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.WingJudgeAdvocateReview, item);

    private Task OnLegalForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.WingCommanderReview, item);

    private Task OnWingForwardClick(RadzenSplitButtonItem item) =>
        HandleWorkflowActionAsync(LineOfDutyWorkflowState.AppointingAuthorityReview, item);

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
    /// <paramref name="state"/>. Does NOT persist the state change to the database.
    /// </summary>
    private void ApplyWorkflowState(LineOfDutyWorkflowState state)
    {
        // Clamp to valid range — DB rows that predate the WorkflowState migration have int value 0
        var stateInt = (int)state < 1 ? 1 : (int)state > _workflowSteps.Count ? _workflowSteps.Count : (int)state;
        _currentStepIndex = stateInt - 1;
        _selectedTabIndex = GetTabIndexForState((LineOfDutyWorkflowState)stateInt);

        // Index TimelineSteps by position (1-based) for fast lookup
        var timelineByIndex = _lodCase?.TimelineSteps
            .Select((ts, i) => (Index: i + 1, Step: ts))
            .ToDictionary(x => x.Index, x => x.Step);

        foreach (var step in _workflowSteps)
        {
            // Pull matching timeline data when available
            var timeline = timelineByIndex?.GetValueOrDefault(step.Number);

            if (step.Number < stateInt)
            {
                step.Status = WorkflowStepStatus.Completed;
                if (string.IsNullOrEmpty(step.StatusText))
                    step.StatusText = "Completed";

                step.StartDate = timeline?.StartDate;
                step.CompletedDate = timeline?.CompletionDate;
                step.CompletedBy = timeline?.ModifiedBy ?? string.Empty;

                if (string.IsNullOrEmpty(step.CompletionDate))
                    step.CompletionDate = step.CompletedDate?.ToString("MM/dd/yyyy h:mm tt")
                        ?? DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
            }
            else if (step.Number == stateInt)
            {
                step.Status = WorkflowStepStatus.InProgress;
                step.StartDate = timeline?.StartDate;
                step.CompletedDate = null;
                step.CompletedBy = string.Empty;
            }
            else
            {
                step.Status = WorkflowStepStatus.Pending;
                step.StatusText = string.Empty;
                step.CompletionDate = string.Empty;
                step.StartDate = null;
                step.CompletedDate = null;
                step.CompletedBy = string.Empty;
            }
        }
    }

    /// <summary>
    /// Maps a <see cref="LineOfDutyWorkflowState"/> to its 0-based tab index in the RadzenTabs control.
    /// Tab order: Member(0) · Med Tech(1) · Med Officer(2) · Unit CC(3) · Wing JA(4) · Wing CC(5) · Appointing Auth(6) · Board Tech(7) · Board Med(8) · Board Legal(9) · Board Admin(10)
    /// </summary>
    private static int GetTabIndexForState(LineOfDutyWorkflowState state)
    {
        for (var i = 0; i < WorkflowTabMap.Length; i++)
        {
            if (WorkflowTabMap[i].State == state)
                return i;
        }

        // Completed maps to the last workflow tab; unknown states default to first
        return state == LineOfDutyWorkflowState.Completed
            ? WorkflowTabMap.Length - 1
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
            return false;

        var state = _lodCase?.WorkflowState ?? LineOfDutyWorkflowState.MemberInformationEntry;
        return tabIndex > GetTabIndexForState(state);
    }

    /// <summary>
    /// Confirms with the user, advances the LOD <see cref="LineOfDutyCase.WorkflowState"/> to
    /// <paramref name="targetState"/>, persists the change, updates the sidebar, and notifies.
    /// </summary>
    private async Task ChangeWorkflowStateAsync(
        LineOfDutyWorkflowState targetState,
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
            return;

        var confirmed = await DialogService.Confirm(confirmMessage, confirmTitle,
            new ConfirmOptions { OkButtonText = okButtonText, CancelButtonText = cancelButtonText });

        if (confirmed != true)
            return;

        await SetBusyAsync(busyMessage);

        try
        {
            _lodCase.WorkflowState = targetState;
            _lodCase = await CaseService.SaveCaseAsync(_lodCase, _cts.Token);
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
        var confirmed = await DialogService.Confirm(
            "Are you sure you want to digitally sign this section?",
            "Confirm Digital Signature",
            new ConfirmOptions { OkButtonText = "Sign", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Applying digital signature...");

        try
        {
            // TODO: persist digital signature to database

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Digitally Signed",
                Detail = "Section has been digitally signed.",
                Duration = 3000
            });
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private (Action Apply, TrackableModel Model)? GetTabOperations(string tabName) => tabName switch
    {
        TabNames.MemberInformation     => (() => LineOfDutyCaseMapper.ApplyMemberInfo(_memberFormModel, _lodCase),           _memberFormModel),
        TabNames.MedicalTechnician     => (() => LineOfDutyCaseMapper.ApplyMedicalAssessment(_medicalFormModel, _lodCase),    _medicalFormModel),
        TabNames.UnitCommander         => (() => LineOfDutyCaseMapper.ApplyUnitCommander(_commanderFormModel, _lodCase),      _commanderFormModel),
        TabNames.WingJudgeAdvocate     => (() => LineOfDutyCaseMapper.ApplyWingJudgeAdvocate(_wingJAFormModel, _lodCase),     _wingJAFormModel),
        TabNames.WingCommander         => (() => LineOfDutyCaseMapper.ApplyWingCommander(_wingCommanderFormModel, _lodCase),  _wingCommanderFormModel),
        TabNames.AppointingAuthority   => (() => LineOfDutyCaseMapper.ApplyAppointingAuthority(_appointingAuthorityFormModel, _lodCase), _appointingAuthorityFormModel),
        TabNames.BoardTechnicianReview => (() => LineOfDutyCaseMapper.ApplyBoardTechnician(_boardTechFormModel, _lodCase),    _boardTechFormModel),
        _                              => null,
    };

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
            var ops = GetTabOperations(source);

            if (ops is { } tabOps)
            {
                tabOps.Apply();
            }
            else
            {
                LineOfDutyCaseMapper.ApplyAll(
                    new CaseViewModelsDto
                    {
                        CaseInfo = _caseInfo,
                        MemberInfo = _memberFormModel,
                        MedicalAssessment = _medicalFormModel,
                        UnitCommander = _commanderFormModel,
                        MedicalTechnician = _medTechFormModel,
                        WingJudgeAdvocate = _wingJAFormModel,
                        WingCommander = _wingCommanderFormModel,
                        AppointingAuthority = _appointingAuthorityFormModel,
                        BoardTechnician = _boardTechFormModel,
                        BoardMedical = _boardMedFormModel,
                        BoardLegal = _boardLegalFormModel,
                        BoardAdmin = _boardAdminFormModel
                    },
                    _lodCase);
            }

            // Save the entity
            _lodCase = await CaseService.SaveCaseAsync(_lodCase, _cts.Token);

            // Refresh the read-only case info from the saved entity
            _caseInfo = LineOfDutyCaseMapper.ToCaseInfoModel(_lodCase);

            // Re-snapshot only the saved model so other tabs retain their dirty state
            if (ops is { } saved)
            {
                saved.Model.TakeSnapshot(JsonOptions);
            }
            else
            {
                TakeSnapshots();
            }

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
        if (_medicalFormModel.IsMilitaryFacility != true)
        {
            _medicalFormModel.TreatmentFacilityName = null;
        }
    }

    private void OnWasUnderInfluenceChanged()
    {
        if (_medicalFormModel.WasUnderInfluence != true)
        {
            _medicalFormModel.SubstanceType = null;
        }
    }

    private void OnToxicologyTestDoneChanged()
    {
        if (_medicalFormModel.ToxicologyTestDone != true)
        {
            _medicalFormModel.ToxicologyTestResults = null;
        }
    }

    private void OnPsychiatricEvalCompletedChanged()
    {
        if (_medicalFormModel.PsychiatricEvalCompleted != true)
        {
            _medicalFormModel.PsychiatricEvalDate = null;
            _medicalFormModel.PsychiatricEvalResults = null;
        }
    }

    private void OnOtherTestsDoneChanged()
    {
        if (_medicalFormModel.OtherTestsDone != true)
        {
            _medicalFormModel.OtherTestDate = null;
            _medicalFormModel.OtherTestResults = null;
        }
    }

    private void OnIsEptsNsaChanged()
    {
        if (_medicalFormModel.IsEptsNsa != true)
        {
            _medicalFormModel.IsServiceAggravated = null;
        }
    }

    private void OnIsAtDeployedLocationChanged()
    {
        if (_medicalFormModel.IsAtDeployedLocation != false)
        {
            _medicalFormModel.RequiresArcBoard = null;
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

            var dto = LineOfDutyCaseMapper.ToCaseViewModelsDto(_lodCase);

            _caseInfo = dto.CaseInfo;
            _memberFormModel = dto.MemberInfo;
            _medicalFormModel = dto.MedicalAssessment;
            _commanderFormModel = dto.UnitCommander;
            _medTechFormModel = dto.MedicalTechnician;
            _wingJAFormModel = dto.WingJudgeAdvocate;
            _wingCommanderFormModel = dto.WingCommander;
            _appointingAuthorityFormModel = dto.AppointingAuthority;
            _boardTechFormModel = dto.BoardTechnician;
            _boardMedFormModel = dto.BoardMedical;
            _boardLegalFormModel = dto.BoardLegal;
            _boardAdminFormModel = dto.BoardAdmin;

            _memberFormModel.Grade = _lodCase.MemberRank ?? string.Empty;

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
            new() { Number = 1,  Name = "Enter Member Information",  Icon = "flag",                 Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.MemberInformationEntry,    Description = "Enter member identification and incident details to initiate the LOD case." },
            new() { Number = 2,  Name = "Medical Technician Review", Icon = "person",               Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.MedicalTechnicianReview,   Description = "Medical technician reviews the injury/illness and documents clinical findings." },
            new() { Number = 3,  Name = "Medical Officer Review",    Icon = "medical_services",     Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.MedicalOfficerReview,      Description = "Medical officer reviews the technician's findings and provides a clinical assessment." },
            new() { Number = 4,  Name = "Unit CC Review",            Icon = "edit_document",        Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.UnitCommanderReview,       Description = "Unit commander reviews the case and submits a recommendation for the LOD determination." },
            new() { Number = 5,  Name = "Wing JA Review",            Icon = "gavel",                Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.WingJudgeAdvocateReview,   Description = "Wing Judge Advocate reviews the case for legal sufficiency and compliance." },
            new() { Number = 6,  Name = "Appointing Authority",      Icon = "verified_user",        Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.AppointingAuthorityReview, Description = "Appointing authority reviews the case and issues a formal LOD determination." },
            new() { Number = 7,  Name = "Wing CC Review",            Icon = "stars",                Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.WingCommanderReview,       Description = "Wing commander reviews the case and renders a preliminary LOD determination." },
            new() { Number = 8,  Name = "Board Technician Review",   Icon = "rate_review",          Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.BoardTechnicianReview,     Description = "Board medical technician reviews the case file for completeness and accuracy." },
            new() { Number = 9,  Name = "Board Medical Review",      Icon = "medical_services",     Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.BoardMedicalReview,        Description = "Board medical officer reviews all medical evidence and provides a formal assessment." },
            new() { Number = 10, Name = "Board Legal Review",        Icon = "gavel",                Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.BoardLegalReview,          Description = "Board legal counsel reviews the case for legal sufficiency before final decision." },
            new() { Number = 11, Name = "Board Admin Review",        Icon = "admin_panel_settings", Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.BoardAdminReview,          Description = "Board administrative officer finalizes the case package and prepares the formal determination." },
            new() { Number = 12, Name = "Completed",                 Icon = "check_circle",         Status = WorkflowStepStatus.Pending, WorkflowState = LineOfDutyWorkflowState.Completed,                Description = "LOD determination has been finalized and the case is closed." }
        ];

        ApplyWorkflowState(_lodCase?.WorkflowState ?? LineOfDutyWorkflowState.MemberInformationEntry);
    }

    private async Task OnFormSubmit(MedicalAssessmentFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.MedicalTechnician);
    }

    private async Task OnMemberFormSubmit(MemberInfoFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.MemberInformation);
    }

    private async Task OnCommanderFormSubmit(UnitCommanderFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.UnitCommander);
    }

    private async Task OnWingJAFormSubmit(WingJudgeAdvocateFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.WingJudgeAdvocate);
    }

    private async Task OnWingCommanderFormSubmit(WingCommanderFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.WingCommander);
    }

    private async Task OnMedTechFormSubmit(MedicalTechnicianFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.MedicalTechnician);
    }

    private async Task OnAppointingAuthorityFormSubmit(AppointingAuthorityFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.AppointingAuthority);
    }

    private async Task OnBoardFormSubmit(BoardTechnicianFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.BoardTechnicianReview);
    }

    private async Task OnBookmarkClick()
    {
        if (_lodCase?.Id is null or 0)
            return;

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
                NotificationService.Notify(NotificationSeverity.Info, "Bookmark Removed", $"Case {_caseInfo?.CaseNumber} removed from bookmarks.", closeOnClick: true);
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
                InitiationDate = DateTime.UtcNow,
                IncidentDate = DateTime.UtcNow
            };

            LineOfDutyCaseMapper.ApplyMemberInfo(_memberFormModel, newCase);

            var saved = await CaseService.SaveCaseAsync(newCase, _cts.Token);

            // Advance to the first review state and persist
            saved.WorkflowState = LineOfDutyWorkflowState.MedicalTechnicianReview;
            saved = await CaseService.SaveCaseAsync(saved, _cts.Token);

            _lodCase = saved;
            CaseId = saved.CaseId;
            _caseInfo = LineOfDutyCaseMapper.ToCaseInfoModel(saved);
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