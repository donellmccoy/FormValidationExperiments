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
    // ──── Tab Name Constants ────
    private static class TabNames
    {
        public const string MemberInformation = "Member Information";
        public const string MedicalTechnician = "Medical Technician";
        public const string CommanderReview = "Commander Review";
        public const string WingJAReview = "Wing JA Review";
        public const string LegalSJAReview = "Legal SJA Review";
        public const string Draft = "Draft";
    }

    [Inject]
    private IDataService CaseService { get; set; }

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

    [Parameter]
    public string CaseId { get; set; }

    private bool IsNewCase => string.IsNullOrEmpty(CaseId);

    private readonly CancellationTokenSource _cts = new();

    private LineOfDutyCase _lodCase;

    private bool isLoading = true;

    private bool isSaving;

    private bool _isBusy;

    private string _busyMessage = string.Empty;

    private int selectedTabIndex;

    private int currentStepIndex;

    private int _selectedMemberId;

    private MemberInfoFormModel _memberFormModel = new();

    private MedicalAssessmentFormModel _formModel = new();

    private RadzenTemplateForm<MedicalAssessmentFormModel> medicalForm;

    private CommanderReviewFormModel _commanderFormModel = new();

    private LegalSJAReviewFormModel _legalFormModel = new();

    private CaseInfoModel _caseInfo = new()
    {
        CaseNumber = "Pending...",
        MemberName = "Pending...",
        Component = "Pending...",
        Rank = "Pending...",
        Unit = "Pending...",
        DateOfInjury = "Pending...",
        Status = "New"
    };

    // ──── Form Model Collection ────
    private IReadOnlyList<TrackableModel> AllFormModels => [_memberFormModel, _formModel, _commanderFormModel, _legalFormModel];

    // ──── Dirty Tracking ────
    private bool HasAnyChanges => AllFormModels.Any(m => m.IsDirty);

    // ──── Conditional Visibility ────
    private bool ShowSubstanceType => _formModel.WasUnderInfluence == true;

    // ──── Lookup Data ────
    private IEnumerable<MilitaryRank> militaryRanks = Enum.GetValues<MilitaryRank>();

    private bool ShowToxicologyResults => _formModel.ToxicologyTestDone == true;

    private bool ShowPsychEvalDetails => _formModel.PsychiatricEvalCompleted == true;

    private bool ShowOtherTestDetails => _formModel.OtherTestsDone == true;

    private bool ShowArcSection => true; // Set true for demo; in production, derive from member's ServiceComponent (AFR/ANG)
    private bool ShowArcSubFields => _formModel.IsAtDeployedLocation == false;

    private string MemberFullName
    {
        get
        {
            var mi = string.IsNullOrWhiteSpace(_memberFormModel.MiddleInitial)
                ? ""
                : $" {_memberFormModel.MiddleInitial}.";
            return $"{_memberFormModel.LastName}, {_memberFormModel.FirstName}{mi}".Trim(' ', ',');
        }
    }

    private string MemberGrade => _memberGrade ?? "";

    private string MemberDateOfBirth => _memberFormModel.DateOfBirth?.ToString("MM/dd/yyyy") ?? "";

    private bool ShowServiceAggravated => _formModel.IsEptsNsa == true;

    // ──── Commander Review Conditional Visibility ────
    private bool ShowMisconductExplanation => _commanderFormModel.ResultOfMisconduct == true;

    private bool ShowOtherSourceDescription => _commanderFormModel.OtherSourcesReviewed == true;

    // ──── Legal SJA Review Conditional Visibility ────
    private bool ShowNonConcurrenceReason => _legalFormModel.ConcurWithRecommendation == false;

    private List<WorkflowStep> workflowSteps = [];

    private WorkflowStep CurrentStep => workflowSteps.Count > 0 ? workflowSteps[currentStepIndex] : null;

    // ──── Member Search ────
    private string _memberGrade = string.Empty;
    private string memberSearchText = string.Empty;
    private List<Member> memberSearchResults = [];
    private bool isMemberSearching;
    private CancellationTokenSource _searchCts = new();
    private RadzenTextBox _memberSearchTextBox;
    private Popup _memberSearchPopup;
    private RadzenDataGrid<Member> _memberSearchGrid;
    private int _memberSearchSelectedIndex;
    private System.Timers.Timer _debounceTimer;

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

        isLoading = false;
    }

    private async Task LoadCaseAsync()
    {
        await SetBusyAsync("Loading case...", true);

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
            _formModel = dto.MedicalAssessment;
            _commanderFormModel = dto.CommanderReview;
            _legalFormModel = dto.LegalSJAReview;

            _memberGrade = _lodCase.MemberRank ?? string.Empty;

            InitializeWorkflowSteps();

            TakeSnapshots();
        }
        catch (OperationCanceledException)
        {
            // Component disposed during load — silently ignore
        }
        catch (Exception ex)
        {
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
        workflowSteps =
        [
            new() { Number = 1,  Name = "Start Line Of Duty",    Icon = "flag",                  Status = WorkflowStepStatus.InProgress, StatusText = "Completed", CompletionDate = DateTime.Now.ToString("MM/dd/yyyy h:mm tt"), Description = "Workflow initialization and initial data entry." },
            new() { Number = 2,  Name = "Member Reports",       Icon = "person",                Status = WorkflowStepStatus.Pending,    StatusText = "Completed", CompletionDate = DateTime.Now.AddDays(-1).ToString("MM/dd/yyyy h:mm tt"), Description = "Member submission of injury details and statement." },
            new() { Number = 3,  Name = "Line Of Duty Initiation",   Icon = "description",      Status = WorkflowStepStatus.Pending,    StatusText = "Approved",  CompletionDate = DateTime.Now.ToString("MM/dd/yyyy h:mm tt"), Description = "Formal initiation of the Line of Duty determination process." },
            new() { Number = 4,  Name = "Medical Assessment",   Icon = "medical_services",      Status = WorkflowStepStatus.Pending,    Description = "Medical provider review and clinical impact assessment." },
            new() { Number = 5,  Name = "Commander Review",     Icon = "edit_document",         Status = WorkflowStepStatus.Pending,    Description = "Commander's recommendation and endorsement." },
            new() { Number = 6,  Name = "Legal SJA Review",     Icon = "gavel",                 Status = WorkflowStepStatus.Pending,    Description = "Legal office review if deemed necessary." },
            new() { Number = 7,  Name = "Wing CC Review",       Icon = "stars",                 Status = WorkflowStepStatus.Pending,    Description = "Wing-level review if escalated." },
            new() { Number = 8,  Name = "Board Review",         Icon = "rate_review",           Status = WorkflowStepStatus.Pending,    Description = "Formal adjudication by the LOD Board." }
        ];
    }

    private async Task OnFormSubmit(MedicalAssessmentFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.MedicalTechnician);
    }

    private async Task OnMemberFormSubmit(MemberInfoFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.MemberInformation);
    }

    private async Task OnCommanderFormSubmit(CommanderReviewFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.CommanderReview);
    }

    private async Task OnLegalFormSubmit(LegalSJAReviewFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.LegalSJAReview);
    }

    private void OnStepSelected(WorkflowStep step)
    {
        currentStepIndex = step.Number - 1;

        foreach (var s in workflowSteps)
        {
            if (s.Number < step.Number)
            {
                s.Status = WorkflowStepStatus.Completed;
                if (string.IsNullOrEmpty(s.StatusText))
                {
                    s.StatusText = "Completed";
                }

                if (string.IsNullOrEmpty(s.CompletionDate))
                {
                    s.CompletionDate = DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
                }
            }
            else if (s.Number == step.Number)
            {
                s.Status = WorkflowStepStatus.InProgress;
            }
            else
            {
                s.Status = WorkflowStepStatus.Pending;
                s.StatusText = string.Empty;
                s.CompletionDate = string.Empty;
            }
        }

        selectedTabIndex = 0;
    }

    private async Task OnMemberForwardClick()
    {
        var confirmed = await DialogService.Confirm(
            "Are you sure you want to forward this case to the Medical Officer?",
            "Confirm Forward",
            new ConfirmOptions { OkButtonText = "Forward", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SetBusyAsync("Forwarding to Medical Officer...");

        try
        {
            NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Medical Officer",
                "Case has been forwarded to the Medical Officer.");
        }
        finally
        {
            await SetBusyAsync(isBusy: false);
        }
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

            _lodCase = saved;
            CaseId = saved.CaseId;
            _caseInfo = LineOfDutyCaseMapper.ToCaseInfoModel(saved);

            TakeSnapshots();

            NotificationService.Notify(NotificationSeverity.Success, "LOD Started",
                $"Case {saved.CaseId} created for {saved.MemberName}.");

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
        if (selectedTabIndex == 1 && medicalForm?.EditContext?.Validate() == false)
        {
            return;
        }

        // Determine which tab to save based on the currently selected tab index
        var source = selectedTabIndex switch
        {
            0 => TabNames.MemberInformation,
            1 => TabNames.MedicalTechnician,
            2 => "Medical Officer",
            3 => TabNames.CommanderReview,
            4 => TabNames.WingJAReview,
            5 => TabNames.LegalSJAReview,
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

    private async Task OnMedTechForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "cancel")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this investigation?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Yes, Cancel", CancelButtonText = "No" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Cancelling investigation...");

            try
            {
                NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled",
                    "The LOD investigation has been cancelled.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to forward this case to the Medical Officer?",
                "Confirm Forward",
                new ConfirmOptions { OkButtonText = "Forward", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Forwarding to Medical Officer...");

            try
            {
                NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Medical Officer",
                    "Case has been forwarded to the Medical Officer for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
    }

    private async Task OnMedicalForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Technician?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Med Tech...");

            try
            {
                // Return to Med Tech
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Med Tech",
                    "Case has been returned to the Medical Technician for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "cancel")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this investigation?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Yes, Cancel", CancelButtonText = "No" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Cancelling investigation...");

            try
            {
                // Cancel Investigation
                NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled",
                    "The LOD investigation has been cancelled.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to forward this case to the Unit Commander?",
                "Confirm Forward",
                new ConfirmOptions { OkButtonText = "Forward", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Forwarding to Unit CC...");

            try
            {
                // Forward to Unit CC (default action)
                NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Unit CC",
                    "Case has been forwarded to the Unit Commander.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
    }

    private async Task OnCommanderForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return-med-officer")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Officer?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Officer...");

            try
            {
                // Return to Medical Officer
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-med-tech")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Technician?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Technician...");

            try
            {
                // Return to Medical Technician
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "cancel")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this investigation?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Yes, Cancel", CancelButtonText = "No" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Cancelling investigation...");

            try
            {
                // Cancel Investigation
                NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled",
                    "The LOD investigation has been cancelled.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to forward this case to the Wing Judge Advocate?",
                "Confirm Forward",
                new ConfirmOptions { OkButtonText = "Forward", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Forwarding to Wing JA...");

            try
            {
                // Forward to Wing JA (default action)
                NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Wing JA",
                    "Case has been forwarded to the Wing Judge Advocate.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
    }

    private async Task OnWingJAForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return-unit-cc")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Unit Commander?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Unit CC...");

            try
            {
                // Return to Unit CC
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Unit CC",
                    "Case has been returned to the Unit Commander for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-med-officer")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Officer?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Officer...");

            try
            {
                // Return to Medical Officer
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-med-tech")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Technician?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Technician...");

            try
            {
                // Return to Medical Technician
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "cancel")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this investigation?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Yes, Cancel", CancelButtonText = "No" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Cancelling investigation...");

            try
            {
                // Cancel Investigation
                NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled",
                    "The LOD investigation has been cancelled.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to forward this case to the Wing Commander?",
                "Confirm Forward",
                new ConfirmOptions { OkButtonText = "Forward", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Forwarding to Wing CC...");

            try
            {
                // Forward to Wing CC (default action)
                NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Wing CC",
                    "Case has been forwarded to the Wing Commander.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
    }

    private async Task OnLegalForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return-wing-ja")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Wing Judge Advocate?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Wing JA...");

            try
            {
                // Return to Wing JA
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Wing JA",
                    "Case has been returned to the Wing Judge Advocate for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Unit Commander?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Unit CC...");

            try
            {
                // Return to Unit CC
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Unit CC",
                    "Case has been returned to the Unit Commander for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-med-officer")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Officer?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Officer...");

            try
            {
                // Return to Medical Officer
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-med-tech")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Technician?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Technician...");

            try
            {
                // Return to Medical Technician
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "cancel")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this investigation?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Yes, Cancel", CancelButtonText = "No" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Cancelling investigation...");

            try
            {
                // Cancel Investigation
                NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled",
                    "The LOD investigation has been cancelled.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to forward this case to the Wing Commander?",
                "Confirm Forward",
                new ConfirmOptions { OkButtonText = "Forward", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Forwarding to Wing CC...");

            try
            {
                // Forward to Wing CC (default action)
                NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Wing CC",
                    "Case has been forwarded to the Wing Commander.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
    }

    private async Task OnWingForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return-wing-cc")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Wing Commander?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Wing CC...");

            try
            {
                // Return to Wing CC
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Wing CC",
                    "Case has been returned to the Wing Commander for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-wing-ja")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Wing Judge Advocate?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Wing JA...");

            try
            {
                // Return to Wing JA
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Wing JA",
                    "Case has been returned to the Wing Judge Advocate for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-unit-cc")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Unit Commander?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Unit CC...");

            try
            {
                // Return to Unit CC
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Unit CC",
                    "Case has been returned to the Unit Commander for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-med-officer")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Officer?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Officer...");

            try
            {
                // Return to Medical Officer
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-med-tech")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Technician?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Technician...");

            try
            {
                // Return to Medical Technician
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "cancel")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this investigation?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Yes, Cancel", CancelButtonText = "No" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Cancelling investigation...");

            try
            {
                // Cancel Investigation
                NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled",
                    "The LOD investigation has been cancelled.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to forward this case to the Board for review?",
                "Confirm Forward",
                new ConfirmOptions { OkButtonText = "Forward", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Forwarding to Board Review...");

            try
            {
                // Forward to Board Review (default action)
                NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Board Review",
                    "Case has been forwarded to the Board for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
    }

    private async Task OnBoardCompleteClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return-appointing-authority")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Appointing Authority?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Appointing Authority...");

            try
            {
                // Return to Appointing Authority
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Appointing Authority",
                    "Case has been returned to the Appointing Authority for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-wing-cc")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Wing Commander?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Wing CC...");

            try
            {
                // Return to Wing CC
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Wing CC",
                    "Case has been returned to the Wing Commander for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-wing-ja")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Wing Judge Advocate?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Wing JA...");

            try
            {
                // Return to Wing JA
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Wing JA",
                    "Case has been returned to the Wing Judge Advocate for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-unit-cc")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Unit Commander?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Unit CC...");

            try
            {
                // Return to Unit CC
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Unit CC",
                    "Case has been returned to the Unit Commander for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-med-officer")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Officer?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Officer...");

            try
            {
                // Return to Medical Officer
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Officer",
                    "Case has been returned to the Medical Officer for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "return-med-tech")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to the Medical Technician?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Medical Technician...");

            try
            {
                // Return to Medical Technician
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Medical Technician",
                    "Case has been returned to the Medical Technician for review.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else if (item?.Value == "cancel")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to cancel this investigation?",
                "Confirm Cancellation",
                new ConfirmOptions { OkButtonText = "Yes, Cancel", CancelButtonText = "No" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Cancelling investigation...");

            try
            {
                // Cancel Investigation
                NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled",
                    "The LOD investigation has been cancelled.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
        else
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to complete the Board review?",
                "Confirm Complete",
                new ConfirmOptions { OkButtonText = "Complete", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Completing Board review...");

            try
            {
                // Complete Review (default action)
                NotificationService.Notify(NotificationSeverity.Success, "Review Completed",
                    "The Board review has been completed.");
            }
            finally
            {
                await SetBusyAsync(isBusy: false);
            }
        }
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

    private async Task SaveCurrentTabAsync(string source)
    {
        if (isSaving)
        {
            return;
        }

        isSaving = true;
        await SetBusyAsync("Saving...");

        try
        {
            // Apply only the specific tab's view model to the entity
            switch (source)
            {
                case TabNames.MemberInformation:
                    LineOfDutyCaseMapper.ApplyMemberInfo(_memberFormModel, _lodCase);
                    break;
                case TabNames.MedicalTechnician:
                    LineOfDutyCaseMapper.ApplyMedicalAssessment(_formModel, _lodCase);
                    break;
                case TabNames.CommanderReview:
                    LineOfDutyCaseMapper.ApplyCommanderReview(_commanderFormModel, _lodCase);
                    break;
                case TabNames.LegalSJAReview:
                    LineOfDutyCaseMapper.ApplyLegalSJAReview(_legalFormModel, _lodCase);
                    break;
                default:
                    // Draft / save-all: apply everything
                    LineOfDutyCaseMapper.ApplyAll(
                        new CaseViewModelsDto
                        {
                            CaseInfo = _caseInfo,
                            MemberInfo = _memberFormModel,
                            MedicalAssessment = _formModel,
                            CommanderReview = _commanderFormModel,
                            LegalSJAReview = _legalFormModel
                        },
                        _lodCase);
                    break;
            }

            // Save the entity
            _lodCase = await CaseService.SaveCaseAsync(_lodCase, _cts.Token);

            // Refresh the read-only case info from the saved entity
            _caseInfo = LineOfDutyCaseMapper.ToCaseInfoModel(_lodCase);

            // Re-snapshot only the saved model so other tabs retain their dirty state
            switch (source)
            {
                case TabNames.MemberInformation:
                    _memberFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.MedicalTechnician:
                    _formModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.CommanderReview:
                    _commanderFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.LegalSJAReview:
                    _legalFormModel.TakeSnapshot(JsonOptions);
                    break;
                default:
                    TakeSnapshots();
                    break;
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
            isSaving = false;
            await SetBusyAsync(isBusy: false);
        }
    }

    private async Task SetBusyAsync(string message = "Working...", bool? isBusy = true)
    {
        _busyMessage = message;
        _isBusy = isBusy.GetValueOrDefault(true);
        StateHasChanged();
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

    private void OnSsnChanged(ChangeEventArgs args)
    {
        var value = args.Value?.ToString();

        if (string.IsNullOrEmpty(value))
        {
            _memberFormModel.SSN = string.Empty;

            return;
        }

        var digits = new string([.. value.Where(char.IsDigit)]);

        if (digits.Length > 4)
        {
            digits = digits[..4];
        }

        _memberFormModel.SSN = digits;

        StateHasChanged();
    }

    // ──── Child-Clearing Handlers ────

    private void OnIsMilitaryFacilityChanged()
    {
        if (_formModel.IsMilitaryFacility != true)
        {
            _formModel.TreatmentFacilityName = null;
        }
    }

    private void OnWasUnderInfluenceChanged()
    {
        if (_formModel.WasUnderInfluence != true)
        {
            _formModel.SubstanceType = null;
        }
    }

    private void OnToxicologyTestDoneChanged()
    {
        if (_formModel.ToxicologyTestDone != true)
        {
            _formModel.ToxicologyTestResults = null;
        }
    }

    private void OnPsychiatricEvalCompletedChanged()
    {
        if (_formModel.PsychiatricEvalCompleted != true)
        {
            _formModel.PsychiatricEvalDate = null;
            _formModel.PsychiatricEvalResults = null;
        }
    }

    private void OnOtherTestsDoneChanged()
    {
        if (_formModel.OtherTestsDone != true)
        {
            _formModel.OtherTestDate = null;
            _formModel.OtherTestResults = null;
        }
    }

    private void OnIsEptsNsaChanged()
    {
        if (_formModel.IsEptsNsa != true)
        {
            _formModel.IsServiceAggravated = null;
        }
    }

    private void OnIsAtDeployedLocationChanged()
    {
        if (_formModel.IsAtDeployedLocation != false)
        {
            _formModel.RequiresArcBoard = null;
        }
    }

    private async Task OnMemberSearchKeyDown(KeyboardEventArgs args)
    {
        var items = memberSearchResults;
        var popupOpened = await JSRuntime.InvokeAsync<bool>("Radzen.popupOpened", "member-search-popup");

        var key = args.Code ?? args.Key;

        if (!args.AltKey && (key == "ArrowDown" || key == "ArrowUp"))
        {
            var result = await JSRuntime.InvokeAsync<int[]>("Radzen.focusTableRow", "member-search-grid", key, _memberSearchSelectedIndex, null, false);
            _memberSearchSelectedIndex = result.First();
        }
        else if (args.AltKey && key == "ArrowDown" || key == "Enter" || key == "NumpadEnter")
        {
            if (popupOpened && (key == "Enter" || key == "NumpadEnter"))
            {
                var selected = items.ElementAtOrDefault(_memberSearchSelectedIndex);
                if (selected != null)
                {
                    await OnMemberSelected(selected);
                    return;
                }
            }

            await _memberSearchPopup.ToggleAsync(_memberSearchTextBox.Element);
        }
        else if (key == "Escape" || key == "Tab")
        {
            await _memberSearchPopup.CloseAsync();
        }
    }

    private async Task OnMemberSearchInput(ChangeEventArgs args)
    {
        _memberSearchSelectedIndex = 0;
        memberSearchText = args.Value?.ToString() ?? string.Empty;

        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();

        if (string.IsNullOrWhiteSpace(memberSearchText))
        {
            memberSearchResults = [];

            StateHasChanged();

            return;
        }

        _debounceTimer = new System.Timers.Timer(300)
        {
            AutoReset = false
        };

        _debounceTimer.Elapsed += async (_, _) =>
        {
            await InvokeAsync(async () =>
            {
                await _searchCts.CancelAsync();

                _searchCts.Dispose();
                _searchCts = new CancellationTokenSource();

                isMemberSearching = true;
                StateHasChanged();

                try
                {
                    memberSearchResults = await CaseService.SearchMembersAsync(memberSearchText, _searchCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Search was superseded — ignore
                }
                catch (Exception ex)
                {
                    NotificationService.Notify(NotificationSeverity.Error, "Search Failed", ex.Message);
                    memberSearchResults = [];
                }
                finally
                {
                    isMemberSearching = false;

                    StateHasChanged();
                }
            });
        };

        _debounceTimer.Start();
    }

    private async Task OnMemberSelected(Member member)
    {
        memberSearchText = string.Empty;
        await _memberSearchPopup.CloseAsync();

        _selectedMemberId = member.Id;

        _memberFormModel.FirstName = member.FirstName;
        _memberFormModel.LastName = member.LastName;
        _memberFormModel.MiddleInitial = member.MiddleInitial;
        _memberFormModel.OrganizationUnit = member.Unit;
        _memberFormModel.SSN = member.ServiceNumber;
        _memberFormModel.DateOfBirth = member.DateOfBirth;

        _memberFormModel.Rank = LineOfDutyCaseMapper.ParseMilitaryRank(member.Rank);

        var parsedRank = _memberFormModel.Rank;
        _memberGrade = parsedRank.HasValue
            ? LineOfDutyCaseMapper.FormatRankToPayGrade(parsedRank.Value)
            : member.Rank;

        _caseInfo.Component = Regex.Replace(member.Component.ToString(), "(\\B[A-Z])", " $1");
        _caseInfo.Rank = parsedRank.HasValue
            ? LineOfDutyCaseMapper.FormatRankToFullName(parsedRank.Value)
            : member.Rank;
        _caseInfo.MemberName = $"{member.LastName}, {member.FirstName}";
        _caseInfo.Unit = member.Unit;

        selectedTabIndex = 0;
        StateHasChanged();
    }

    private async Task CreateCaseForMemberAsync(Member member)
    {
        await SetBusyAsync("Creating case...");

        try
        {
            var newCase = new LineOfDutyCase
            {
                CaseId = $"{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
                MemberName = $"{member.LastName}, {member.FirstName}",
                MemberRank = member.Rank,
                MemberDateOfBirth = member.DateOfBirth,
                ServiceNumber = member.ServiceNumber,
                Unit = member.Unit,
                Component = member.Component,
                MemberId = member.Id,
                InitiationDate = DateTime.UtcNow,
                IncidentDate = DateTime.UtcNow
            };

            var saved = await CaseService.SaveCaseAsync(newCase, _cts.Token);

            NotificationService.Notify(NotificationSeverity.Success, "Case Created", $"Case {saved.CaseId} created for {member.Rank} {member.LastName}.");

            Navigation.NavigateTo($"/case/{saved.CaseId}");
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

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _debounceTimer?.Stop();
        _debounceTimer?.Dispose();
        _searchCts.Cancel();
        _searchCts.Dispose();
    }
}
