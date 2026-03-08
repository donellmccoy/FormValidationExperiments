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
using Stateless;
using System.Text.Json;

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

    private readonly CancellationTokenSource _cts = new();

    private LineOfDutyCase _lineOfDutyCase;

    private LodStateMachine _stateMachine;

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


    protected override async Task OnInitializedAsync()
    {
        if (IsNewCase)
        {
            _selectedTabIndex = LodStateMachine.GetTabIndexForState(_lineOfDutyCase?.WorkflowState ?? WorkflowState.Draft);

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
        if (firstRender)
        {
            _tabs?.Reload();

            if (IsNewCase && _memberSearchTextBox is not null)
            {
                await _memberSearchTextBox.Element.FocusAsync();
            }
        }
    }

    private async Task LoadCaseAsync()
    {
        await SetBusyAsync("Loading case...");

        try
        {
            _lineOfDutyCase = await CaseService.GetCaseAsync(CaseId, _cts.Token);

            _stateMachine = LineOfDutyStateMachineFactory.Create(_lineOfDutyCase, CaseService);

            _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

            _selectedTabIndex = LodStateMachine.GetTabIndexForState(_lineOfDutyCase.WorkflowState);

            try
            {
                _bookmark.IsBookmarked = await CaseService.IsBookmarkedAsync(_lineOfDutyCase.Id, _cts.Token);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to check bookmark status for case {CaseId}", _lineOfDutyCase.Id);
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

            _selectedTabIndex = LodStateMachine.GetTabIndexForState(_lineOfDutyCase.WorkflowState);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

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

        if (IsNewCase)
        {
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

                _stateMachine = LineOfDutyStateMachineFactory.Create(CaseService);

                _stateMachine.OnMemberInformationEntered = lod =>
                {
                    _lineOfDutyCase = lod;

                    CaseId = _lineOfDutyCase.CaseId;

                    _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                    TakeSnapshots();

                    _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

                    _selectedTabIndex = LodStateMachine.GetTabIndexForState(_lineOfDutyCase.WorkflowState);

                    _tabs?.Reload();

                    NotificationService.Notify(
                        NotificationSeverity.Success,
                        "Line of Duty Case Started",
                        $"Case: {_lineOfDutyCase.CaseId} created for: {_lineOfDutyCase.MemberName}.");

                    return Task.CompletedTask;
                };

                await _stateMachine.FireAsync(lineOfDutyCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);
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
        else
        {
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

                _stateMachine.OnMedicalTechnicianReviewEntered = lod =>
                {
                    _lineOfDutyCase = lod;

                    CaseId = _lineOfDutyCase.CaseId;

                    _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                    TakeSnapshots();

                    _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

                    _selectedTabIndex = LodStateMachine.GetTabIndexForState(_lineOfDutyCase.WorkflowState);

                    NotificationService.Notify(
                        NotificationSeverity.Success,
                        "Line of Duty Case Updated",
                        $"Case: {_lineOfDutyCase.CaseId} updated for: {_lineOfDutyCase.MemberName}.");

                    return Task.CompletedTask;
                };

                await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToMedicalTechnician);
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
    }

    private async Task OnMedicalTechnicianForwardClick(RadzenSplitButtonItem item)
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

        confirmed = await DialogService.Confirm(
            "Are you sure you want to forward the case to the Medical Officer?",
            "Forward to Medical Officer",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Medical Officer...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            _stateMachine.OnMedicalOfficerReviewEntered = lod =>
            {
                _lineOfDutyCase = lod;

                CaseId = _lineOfDutyCase.CaseId;

                _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                TakeSnapshots();

                _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

                _selectedTabIndex = LodStateMachine.GetTabIndexForState(_lineOfDutyCase.WorkflowState);

                NotificationService.Notify(
                    NotificationSeverity.Success,
                    "Line of Duty Case Updated",
                    $"Case: {_lineOfDutyCase.CaseId} updated for: {_lineOfDutyCase.MemberName}.");

                return Task.CompletedTask;
            };

            await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToMedicalOfficerReview);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task OnForwardToUnitCommanderClick(RadzenSplitButtonItem item)
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

        confirmed = await DialogService.Confirm(
            "Are you sure you want to forward the case to the Medical Officer?",
            "Forward to Medical Officer",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Unit Commander...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            _stateMachine.OnUnitCommanderReviewEntered = lod =>
            {
                _lineOfDutyCase = lod;

                CaseId = _lineOfDutyCase.CaseId;

                _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                TakeSnapshots();

                _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

                _selectedTabIndex = LodStateMachine.GetTabIndexForState(_lineOfDutyCase.WorkflowState);

                NotificationService.Notify(
                    NotificationSeverity.Success,
                    "Line of Duty Case Updated",
                    $"Case: {_lineOfDutyCase.CaseId} updated for: {_lineOfDutyCase.MemberName}.");

                return Task.CompletedTask;
            };

            await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToUnitCommanderReview);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task OnForwardToWingJudgeAdvocateClick(RadzenSplitButtonItem item)
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

        confirmed = await DialogService.Confirm(
            "Are you sure you want to forward the case to the Wing Judge Advocate?",
            "Forward to Wing Judge Advocate",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Wing Judge Advocate...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            _stateMachine.OnWingJudgeAdvocateReviewEntered = lod =>
            {
                _lineOfDutyCase = lod;

                CaseId = _lineOfDutyCase.CaseId;

                _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                TakeSnapshots();

                _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

                _selectedTabIndex = LodStateMachine.GetTabIndexForState(_lineOfDutyCase.WorkflowState);

                NotificationService.Notify(
                    NotificationSeverity.Success,
                    "Line of Duty Case Updated",
                    $"Case: {_lineOfDutyCase.CaseId} updated for: {_lineOfDutyCase.MemberName}.");

                return Task.CompletedTask;
            };

            await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task OnForwardToWingCommanderClick(RadzenSplitButtonItem item)
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

        confirmed = await DialogService.Confirm(
            "Are you sure you want to forward the case to the Wing Commander?",
            "Forward to Wing Commander",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Wing Commander...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            _stateMachine.OnWingCommanderReviewEntered = lod =>
            {
                _lineOfDutyCase = lod;

                CaseId = _lineOfDutyCase.CaseId;

                _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                TakeSnapshots();

                _workflowSidebar.ApplyWorkflowState(_lineOfDutyCase);

                _selectedTabIndex = LodStateMachine.GetTabIndexForState(_lineOfDutyCase.WorkflowState);

                NotificationService.Notify(
                    NotificationSeverity.Success,
                    "Line of Duty Case Updated",
                    $"Case: {_lineOfDutyCase.CaseId} updated for: {_lineOfDutyCase.MemberName}.");

                return Task.CompletedTask;
            };

            await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToWingCommanderReview);
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private Task OnForwardToAppointingAuthorityClick(RadzenSplitButtonItem item)
    {
        return Task.CompletedTask;
    }

    private Task OnForwardToBoardTechnicianClick(RadzenSplitButtonItem item)
    {
        return Task.CompletedTask;
    }

    private Task OnForwardToBoardMedicalClick(RadzenSplitButtonItem item)
    {
        return Task.CompletedTask;
    }

    private Task OnForwardToBoardLegalClick(RadzenSplitButtonItem item)
    {
        return Task.CompletedTask;
    }

    private Task OnForwardToBoardAdminClick(RadzenSplitButtonItem item)
    {
        return Task.CompletedTask;
    }

    private Task OnCompleteClick(RadzenSplitButtonItem item)
    {
        return Task.CompletedTask;
    }



    private async Task HandleWorkflowActionAsync(WorkflowState sourceState, RadzenSplitButtonItem item)
    {
        if (item?.Value == "cancel")
        {
            await CancelInvestigationAsync();
            return;
        }

        var action = item?.Value ?? "default";

        var transition = LodStateMachine.ResolveTransition(sourceState, action);
        if (transition is null)
        {
            return;
        }

        await ChangeWorkflowStateAsync(transition);
    }

    private async Task ChangeWorkflowStateAsync(WorkflowTransition transition)
    {
        if (_lineOfDutyCase is null)
        {
            return;
        }

        var confirmed = await DialogService.Confirm(
            transition.ConfirmMessage,
            transition.ConfirmTitle,
            new ConfirmOptions { OkButtonText = transition.OkButtonText, CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync(transition.BusyMessage);

        try
        {
            var result = await _stateMachine.TransitionAsync(transition.Trigger, transition.TargetState, _cts.Token);

            if (result.Success)
            {
                _lineOfDutyCase = result.Case;

                _viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lineOfDutyCase);

                _workflowSidebar?.ApplyWorkflowState(_lineOfDutyCase);
                _selectedTabIndex = result.TabIndex;
                _tabs?.Reload();

                TakeSnapshots();

                NotificationService.Notify(transition.Severity, transition.NotifySummary, transition.NotifyDetail);
            }
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "State Change Failed", result.ErrorMessage);

                // On failure, the SM already reset from persisted state — re-sync local references
                _lineOfDutyCase = _stateMachine.Case;
                _stateMachine = LineOfDutyStateMachineFactory.Create(_lineOfDutyCase, CaseService);
            }
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    private bool IsTabDisabled(int tabIndex)
    {
        return _stateMachine?.IsTabDisabled(tabIndex) ?? tabIndex > LodStateMachine.GetTabIndexForState(WorkflowState.Draft);
    }

    private Task CancelInvestigationAsync()
    {
        return ChangeWorkflowStateAsync(
            new WorkflowTransition(
                LineOfDutyTrigger.Cancel,
                WorkflowState.Cancelled,
                "Are you sure you want to cancel this investigation?",
                "Confirm Cancellation",
                "Yes, Cancel",
                "Cancelling investigation...",
                NotificationSeverity.Warning,
                "Investigation Cancelled",
                "The LOD investigation has been cancelled."));
    }

    private void SetIsSaving(bool isSaving)
    {
        _page.IsSaving = isSaving;
    }

    private async Task SaveCurrentTabAsync(string tabName)
    {
        if (_lineOfDutyCase is null)
        {
            return;
        }

        SetIsSaving(true);
        await SetBusyAsync($"Saving {tabName}...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _stateMachine.Case);

            var result = await _stateMachine.SaveCaseAsync(_cts.Token);

            if (result.Success)
            {
                _lineOfDutyCase = result.Case;
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
            else
            {
                NotificationService.Notify(NotificationSeverity.Error, "Save Failed", result.ErrorMessage);
            }
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

    public void TakeSnapshots()
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

    private async Task OnWingCommanderFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.WingCommander);
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
