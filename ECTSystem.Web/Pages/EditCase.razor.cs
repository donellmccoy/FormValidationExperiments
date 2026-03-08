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

/// <summary>
/// Code-behind for the Edit Case page — a multi-step wizard that drives an LOD case
/// through the AF Form 348 workflow using <see cref="LineOfDutyStateMachine"/>.
/// </summary>
public partial class EditCase : ComponentBase, IDisposable
{
    /// <summary>Constants for the tab display names shown in the <see cref="RadzenTabs"/> control.</summary>
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

    /// <summary>Route parameter identifying the case to edit; <c>null</c> when creating a new case.</summary>
    [Parameter]
    public string CaseId { get; set; }

    /// <summary>Optional query-string parameter (<c>?from=</c>) indicating the originating page for breadcrumb navigation.</summary>
    [SupplyParameterFromQuery(Name = "from")]
    public string FromPage { get; set; }

    /// <summary>Whether a new case is being created (no <see cref="CaseId"/> supplied).</summary>
    private bool IsNewCase => string.IsNullOrEmpty(CaseId);

    /// <summary>Resolves the <see cref="FromPage"/> query parameter to a navigation URI.</summary>
    private string NavigatedFromPath => FromPage?.ToLowerInvariant() switch
    {
        "cases" => "/cases",
        "bookmarks" => "/bookmarks",
        _ => "/"
    };

    /// <summary>Display text for the breadcrumb link back to the originating page.</summary>
    private string BreadcrumbText => FromPage?.ToLowerInvariant() switch
    {
        "cases" => "Search Cases",
        "bookmarks" => "Bookmarks",
        _ => "Dashboard"
    };

    /// <summary>Cached dropdown items for <see cref="DutyStatus"/>.</summary>
    private static readonly object[] _dutyStatusOptions = [.. Enum.GetValues<DutyStatus>().Select(s => new { Text = s.ToDisplayString(), Value = (DutyStatus?)s })];

    /// <summary>Loading / busy / saving UI state.</summary>
    private readonly PageOperationState _page = new();

    /// <summary>Bookmark toggle UI state.</summary>
    private readonly BookmarkUiState _bookmark = new();

    /// <summary>Document upload / paging UI state.</summary>
    private readonly DocumentUiState _documents = new();

    /// <summary>Cancellation source linked to the component lifetime; cancelled on <see cref="Dispose"/>.</summary>
    private readonly CancellationTokenSource _cts = new();

    /// <summary>The domain entity loaded from the API, or <c>null</c> for a new case.</summary>
    private LineOfDutyCase _lineOfDutyCase;

    /// <summary>State machine controlling workflow transitions for the current case.</summary>
    private LineOfDutyStateMachine _stateMachine;

    /// <summary>Index of the currently selected <see cref="RadzenTabs"/> tab.</summary>
    private int _selectedTabIndex;

    /// <summary>Reference to the <see cref="RadzenTabs"/> component.</summary>
    private RadzenTabs _tabs;

    /// <summary>Reference to the workflow sidebar component.</summary>
    private WorkflowSidebar _workflowSidebar;

    /// <summary>Primary key of the member selected via the member-search popup.</summary>
    private int _selectedMemberId;

    /// <summary>Two-way data-bound view model for all form tabs.</summary>
    private LineOfDutyViewModel _viewModel = new();

    /// <summary>Reference to the Medical Technician <see cref="RadzenTemplateForm{TItem}"/> for manual validation.</summary>
    private RadzenTemplateForm<LineOfDutyViewModel> _medicalForm;

    /// <summary>Aggregate list of all trackable models used for dirty-checking.</summary>
    private IReadOnlyList<TrackableModel> AllFormModels => [_viewModel];

    /// <summary>Whether any form model has unsaved changes.</summary>
    private bool HasAnyChanges => _viewModel.IsDirty;

    /// <summary>Number of unread notifications attached to the current case.</summary>
    private int NotificationCount => _lineOfDutyCase?.Notifications?.Count ?? 0;

    /// <summary>The currently active <see cref="WorkflowStep"/> from the sidebar.</summary>
    private WorkflowStep CurrentStep => _workflowSidebar?.CurrentStep;


    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <summary>
    /// Loads the case identified by <see cref="CaseId"/> from the API, initialises the
    /// <see cref="_stateMachine"/>, maps the entity to <see cref="_viewModel"/>, and
    /// checks the bookmark status.
    /// </summary>
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

    /// <summary>
    /// Handles the "Forward" split-button action on the Member Information step.
    /// For new cases, creates the case via the state machine; for existing cases,
    /// transitions to <see cref="WorkflowState.MedicalTechnicianReview"/>.
    /// </summary>
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

                _stateMachine = StateMachineFactory.Create();

                var result = await _stateMachine.FireAsync(lineOfDutyCase, LineOfDutyTrigger.StartLineOfDutyCase);

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
    }

    /// <summary>
    /// Handles the "Forward" action on the Medical Technician step, transitioning
    /// to <see cref="WorkflowState.MedicalOfficerReview"/>.
    /// </summary>
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

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToMedicalOfficerReview);

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

    /// <summary>
    /// Handles the "Forward" action on the Medical Officer step, transitioning
    /// to <see cref="WorkflowState.UnitCommanderReview"/>.
    /// </summary>
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

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToUnitCommanderReview);

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

    /// <summary>
    /// Handles the "Forward" action on the Unit Commander step, transitioning
    /// to <see cref="WorkflowState.WingJudgeAdvocateReview"/>.
    /// </summary>
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

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview);

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

    /// <summary>
    /// Handles the "Forward" action on the Wing Judge Advocate step, transitioning
    /// to <see cref="WorkflowState.WingCommanderReview"/>.
    /// </summary>
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

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToWingCommanderReview);

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

    /// <summary>
    /// Handles the "Forward" action on the Wing Commander step, transitioning
    /// to <see cref="WorkflowState.AppointingAuthorityReview"/>.
    /// </summary>
    private async Task OnForwardToAppointingAuthorityClick(RadzenSplitButtonItem item)
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
            "Are you sure you want to forward the case to the Appointing Authority?",
            "Forward to Appointing Authority",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Appointing Authority...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToAppointingAuthorityReview);

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

    /// <summary>
    /// Handles the "Forward" action on the Appointing Authority step, transitioning
    /// to <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// </summary>
    private async Task OnForwardToBoardTechnicianClick(RadzenSplitButtonItem item)
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
            "Are you sure you want to forward the case to the Board Technician?",
            "Forward to Board Technician",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Board Technician...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToBoardTechnicianReview);

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

    /// <summary>
    /// Handles the "Forward" action on the Board Technician step, transitioning
    /// to <see cref="WorkflowState.BoardMedicalOfficerReview"/>.
    /// </summary>
    private async Task OnForwardToBoardMedicalClick(RadzenSplitButtonItem item)
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
            "Are you sure you want to forward the case to the Board Medical Officer?",
            "Forward to Board Medical Officer",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Board Medical Officer...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToBoardMedicalReview);

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

    /// <summary>
    /// Handles the "Forward" action on the Board Medical Officer step, transitioning
    /// to <see cref="WorkflowState.BoardLegalReview"/>.
    /// </summary>
    private async Task OnForwardToBoardLegalClick(RadzenSplitButtonItem item)
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
            "Are you sure you want to forward the case to the Board Legal Advisor?",
            "Forward to Board Legal Advisor",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Board Legal Advisor...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToBoardLegalReview);

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

    /// <summary>
    /// Handles the "Forward" action on the Board Legal Review step, transitioning
    /// to <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// </summary>
    private async Task OnForwardToBoardAdminClick(RadzenSplitButtonItem item)
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
            "Are you sure you want to forward the case to the Board Administrator?",
            "Forward to Board Administrator",
            new ConfirmOptions
            {
                OkButtonText = "Start",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding case to Board Administrator...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.ForwardToBoardAdministratorReview);

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

    /// <summary>
    /// Handles the "Complete" action on the Board Administrator step, transitioning
    /// to <see cref="WorkflowState.Completed"/>.
    /// </summary>
    private async Task OnCompleteClick(RadzenSplitButtonItem item)
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
            "Are you sure you want to complete this line of duty case?",
            "Complete Case",
            new ConfirmOptions
            {
                OkButtonText = "Complete",
                CancelButtonText = "Cancel"
            });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Completing line of duty case...");

        try
        {
            LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lineOfDutyCase);

            var result = await _stateMachine.FireAsync(_lineOfDutyCase, LineOfDutyTrigger.Complete);

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
                    "Line of Duty Case Completed",
                    $"Case: {_lineOfDutyCase.CaseId} completed for: {_lineOfDutyCase.MemberName}.");
            }
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
    }

    /// <summary>Returns whether the tab at <paramref name="tabIndex"/> should be disabled given the current workflow state.</summary>
    private bool IsTabDisabled(int tabIndex)
    {
        var currentState = _lineOfDutyCase?.WorkflowState ?? WorkflowState.Draft;
        return WorkflowTabHelper.IsTabDisabled(tabIndex, currentState);
    }

    /// <summary>Sets the <see cref="PageOperationState.IsSaving"/> flag on <see cref="_page"/>.</summary>
    private void SetIsSaving(bool isSaving)
    {
        _page.IsSaving = isSaving;
    }

    /// <summary>
    /// Persists the current tab's form data via the state machine and refreshes the view model.
    /// </summary>
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

    /// <summary>Sets the busy overlay message and triggers a UI re-render.</summary>
    private async Task SetBusyAsync(string message = "Working...", bool? isBusy = true)
    {
        _page.BusyMessage = message;
        _page.IsBusy = isBusy.GetValueOrDefault(true);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Prompts the user to confirm and then reverts all form models to their last snapshot.</summary>
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

    /// <summary>Clears dependent facility-name field when the military-facility flag is toggled off.</summary>
    /// <summary>Clears dependent facility-name field when the military-facility flag is toggled off.</summary>
    /// <summary>Clears dependent facility-name field when the military-facility flag is toggled off.</summary>
    private void OnIsMilitaryFacilityChanged()
    {
        if (_viewModel.IsMilitaryFacility != true)
        {
            _viewModel.TreatmentFacilityName = null;
        }
    /// <summary>Clears the substance-type field when the under-influence flag is toggled off.</summary>
    }
/// <summary>Clears the substance-type field when the under-influence flag is toggled off.</summary>
    
    /// <summary>Clears the substance-type field when the under-influence flag is toggled off.</summary>
    private void OnWasUnderInfluenceChanged()
    {
        if (_viewModel.WasUnderInfluence != true)
        {
    /// <summary>Clears toxicology results when the toxicology-test-done flag is toggled off.</summary>
            _viewModel.SubstanceType = null;
        }
    /// <summary>Clears toxicology results when the toxicology-test-done flag is toggled off.</summary>
    }

    /// <summary>Clears toxicology results when the toxicology-test-done flag is toggled off.</summary>
    private void OnToxicologyTestDoneChanged()
    {
    /// <summary>Clears psychiatric evaluation date and results when the eval flag is toggled off.</summary>
        if (_viewModel.ToxicologyTestDone != true)
        {
            _viewModel.ToxicologyTestResults = null;
    /// <summary>Clears psychiatric evaluation date and results when the eval flag is toggled off.</summary>
        }
    }

    /// <summary>Clears psychiatric evaluation date and results when the eval flag is toggled off.</summary>
    private void OnPsychiatricEvalCompletedChanged()
    /// <summary>Clears other-test date and results when the other-tests-done flag is toggled off.</summary>
    {
        if (_viewModel.PsychiatricEvalCompleted != true)
        {
            _viewModel.PsychiatricEvalDate = null;
    /// <summary>Clears other-test date and results when the other-tests-done flag is toggled off.</summary>
            _viewModel.PsychiatricEvalResults = null;
        }
    }

    /// <summary>Clears the service-aggravated flag when the EPTS/NSA flag is toggled off.</summary>
    /// <summary>Clears other-test date and results when the other-tests-done flag is toggled off.</summary>
    private void OnOtherTestsDoneChanged()
    {
        if (_viewModel.OtherTestsDone != true)
        {
    /// <summary>Clears the service-aggravated flag when the EPTS/NSA flag is toggled off.</summary>
            _viewModel.OtherTestDate = null;
            _viewModel.OtherTestResults = null;
    /// <summary>Clears the ARC-board-required flag when the deployed-location flag changes.</summary>
        }
    }

    /// <summary>Clears the service-aggravated flag when the EPTS/NSA flag is toggled off.</summary>
    private void OnIsEptsNsaChanged()
    {
    /// <summary>Clears the ARC-board-required flag when the deployed-location flag changes.</summary>
        if (_viewModel.IsEptsNsa != true)
        {
            _viewModel.IsServiceAggravated = null;
        }
    }

    /// <summary>Clears the ARC-board-required flag when the deployed-location flag changes.</summary>
    private void OnIsAtDeployedLocationChanged()
    {
        if (_viewModel.IsAtDeployedLocation != false)
        {
            _viewModel.RequiresArcBoard = null;
        }
    }

    /// <summary>Captures a JSON snapshot of every form model for dirty-tracking.</summary>
    public void TakeSnapshots()
    {
        foreach (var model in AllFormModels)
        {
            model.TakeSnapshot(JsonOptions);
        }
    }

    /// <summary>Handles the Medical Technician form submit by saving the current tab.</summary>
    private async Task OnFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.MedicalTechnician);
    }

    /// <summary>Handles the Member Information form submit by saving the current tab.</summary>
    private async Task OnMemberFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.MemberInformation);
    }

    /// <summary>Handles the Unit Commander form submit by saving the current tab.</summary>
    private async Task OnCommanderFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.UnitCommander);
    }

    /// <summary>Handles the Wing Commander form submit by saving the current tab.</summary>
    private async Task OnWingCommanderFormSubmit(LineOfDutyViewModel model)
    {
        await SaveCurrentTabAsync(TabNames.WingCommander);
    }

    /// <summary>Toggles the bookmark for the current case with animation and API persistence.</summary>
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

    /// <summary>Opens the file attachment dialog. Not yet implemented.</summary>
    private async Task OnAttachFileClick()
    {
        // TODO: Implement file attachment dialog/upload
        await Task.CompletedTask;
    }

    /// <summary>
    /// Top-level save dispatcher: validates the active form, confirms with the user,
    /// and delegates to <see cref="SaveCurrentTabAsync"/>.
    /// </summary>
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

    /// <summary>Cancels and disposes the <see cref="CancellationTokenSource"/> instances.</summary>
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
    }
}
