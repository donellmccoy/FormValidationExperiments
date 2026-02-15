using System.Text.Json;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ECTSystem.Web.Services;
using ECTSystem.Shared.ViewModels;
using ECTSystem.Web.Shared;
using Radzen;
using Radzen.Blazor;

namespace ECTSystem.Web.Pages;

public partial class EditCase : ComponentBase, IDisposable
{
    // ──── Tab Name Constants ────
    private static class TabNames
    {
        public const string MemberInformation = "Member Information";
        public const string MedicalAssessment = "Medical Assessment";
        public const string CommanderReview = "Commander Review";
        public const string LegalSJAReview = "Legal SJA Review";
        public const string Draft = "Draft";
    }

    [Inject]
    private ILineOfDutyCaseService CaseService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Inject]
    private DialogService DialogService { get; set; }

    [Inject]
    private JsonSerializerOptions JsonOptions { get; set; }

    [Parameter]
    public string CaseId { get; set; }

    private readonly CancellationTokenSource _cts = new();

    private LineOfDutyCase _lodCase;

    private bool isLoading = true;

    private bool isSaving;

    private bool isBusy;
    private string busyMessage = string.Empty;

    private string memberSearchText = string.Empty;

    private int selectedTabIndex;

    private int currentStepIndex;

    private MemberInfoFormModel memberFormModel = new();

    private MedicalAssessmentFormModel formModel = new();

    private CommanderReviewFormModel commanderFormModel = new();

    private LegalSJAReviewFormModel legalFormModel = new();

    private CaseInfoModel caseInfo = new();

    // ──── Form Model Collection ────
    private IReadOnlyList<TrackableModel> AllFormModels => [memberFormModel, formModel, commanderFormModel, legalFormModel];

    // ──── Dirty Tracking ────
    private bool HasAnyChanges => AllFormModels.Any(m => m.IsDirty);

    // ──── Conditional Visibility ────
    private bool ShowSubstanceType => formModel.WasUnderInfluence == true;

    // ──── Lookup Data ────
    private IEnumerable<MilitaryRank> militaryRanks = Enum.GetValues<MilitaryRank>();

    private bool ShowToxicologyResults => formModel.ToxicologyTestDone == true;

    private bool ShowPsychEvalDetails => formModel.PsychiatricEvalCompleted == true;

    private bool ShowOtherTestDetails => formModel.OtherTestsDone == true;

    private bool ShowArcSection => true; // Set true for demo; in production, derive from member's ServiceComponent (AFR/ANG)
    private bool ShowArcSubFields => formModel.IsAtDeployedLocation == false;

    private bool ShowServiceAggravated => formModel.IsEptsNsa == true;

    // ──── Commander Review Conditional Visibility ────
    private bool ShowMisconductExplanation => commanderFormModel.ResultOfMisconduct == true;

    private bool ShowOtherSourceDescription => commanderFormModel.OtherSourcesReviewed == true;

    // ──── Legal SJA Review Conditional Visibility ────
    private bool ShowNonConcurrenceReason => legalFormModel.ConcurWithRecommendation == false;

    private List<WorkflowStep> workflowSteps = [];

    private WorkflowStep CurrentStep => workflowSteps.Count > 0 ? workflowSteps[currentStepIndex] : null;

    protected override async Task OnInitializedAsync()
    {
        await LoadCaseAsync();
        isLoading = false;
    }

    private async Task LoadCaseAsync()
    {
        busyMessage = "Loading case...";
        isBusy = true;

        try
        {
            _lodCase = await CaseService.GetCaseAsync(CaseId, _cts.Token);

            if (_lodCase is null)
            {
                InitializeWorkflowSteps();
                return;
            }

            var dto = LineOfDutyCaseMapper.ToCaseViewModelsDto(_lodCase);

            caseInfo = dto.CaseInfo;
            memberFormModel = dto.MemberInfo;
            formModel = dto.MedicalAssessment;
            commanderFormModel = dto.CommanderReview;
            legalFormModel = dto.LegalSJAReview;

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
            isBusy = false;
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
            new() { Number = 1,  Name = "Start",                Icon = "flag",                  Status = WorkflowStepStatus.InProgress, StatusText = "Completed", CompletionDate = DateTime.Now.ToString("MM/dd/yyyy h:mm tt"), Description = "Workflow initialization and initial data entry." },
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
        await SaveCurrentTabAsync(TabNames.MedicalAssessment);
    }

    private async Task OnMemberFormSubmit(MemberInfoFormModel model)
    {
        await SaveCurrentTabAsync(TabNames.MemberInformation);
    }

    private async Task OnMemberSearch()
    {
        if (string.IsNullOrWhiteSpace(memberSearchText))
        {
            NotificationService.Notify(NotificationSeverity.Info, "Search", "Please enter a name or last 4 SSN.");
            return;
        }

        await SetBusyAsync("Searching for member...");

        try
        {
            // TODO: Replace with actual API call to search for members
            // For now, notify that search is not yet connected to a data source
            NotificationService.Notify(NotificationSeverity.Warning, "Not Available",
                "Member search is not yet connected to a data source.");
        }
        finally
        {
            isBusy = false;
            StateHasChanged();
        }
    }

    private async Task OnMemberSearchKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Enter")
        {
            await OnMemberSearch();
        }
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
            isBusy = false;
            StateHasChanged();
        }
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

    private async Task OnSaveDraft()
    {
        var confirmed = await DialogService.Confirm(
            "Are you sure you want to save?",
            "Confirm Save",
            new ConfirmOptions { OkButtonText = "Save", CancelButtonText = "Cancel" });

        if (confirmed != true)
        {
            return;
        }

        await SaveCurrentTabAsync(TabNames.Draft);
    }

    private async Task OnSplitButtonClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "revert")
        {
            await OnRevertChanges();
            return;
        }

        // Determine which tab to save based on the currently selected tab index
        var source = selectedTabIndex switch
        {
            0 => TabNames.MemberInformation,
            1 => TabNames.MedicalAssessment,
            2 => TabNames.CommanderReview,
            3 => TabNames.LegalSJAReview,
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
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
            }
        }
    }

    private async Task OnCommanderForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
        {
            var confirmed = await DialogService.Confirm(
                "Are you sure you want to return this case to Board Medical?",
                "Confirm Return",
                new ConfirmOptions { OkButtonText = "Return", CancelButtonText = "Cancel" });

            if (confirmed != true)
            {
                return;
            }

            await SetBusyAsync("Returning to Board Medical...");

            try
            {
                // Return to Board Medical
                NotificationService.Notify(NotificationSeverity.Info, "Returned to Board Medical",
                    "Case has been returned to the Board Medical for review.");
            }
            finally
            {
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
            }
        }
    }

    private async Task OnLegalForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
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
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
            }
        }
    }

    private async Task OnWingForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
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
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
            }
        }
    }

    private async Task OnBoardCompleteClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
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
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
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
                isBusy = false;
                StateHasChanged();
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
            isBusy = false;
            StateHasChanged();
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
                    LineOfDutyCaseMapper.ApplyMemberInfo(memberFormModel, _lodCase);
                    break;
                case TabNames.MedicalAssessment:
                    LineOfDutyCaseMapper.ApplyMedicalAssessment(formModel, _lodCase);
                    break;
                case TabNames.CommanderReview:
                    LineOfDutyCaseMapper.ApplyCommanderReview(commanderFormModel, _lodCase);
                    break;
                case TabNames.LegalSJAReview:
                    LineOfDutyCaseMapper.ApplyLegalSJAReview(legalFormModel, _lodCase);
                    break;
                default:
                    // Draft / save-all: apply everything
                    LineOfDutyCaseMapper.ApplyAll(
                        new CaseViewModelsDto
                        {
                            CaseInfo = caseInfo,
                            MemberInfo = memberFormModel,
                            MedicalAssessment = formModel,
                            CommanderReview = commanderFormModel,
                            LegalSJAReview = legalFormModel
                        },
                        _lodCase);
                    break;
            }

            // Save the entity
            _lodCase = await CaseService.SaveCaseAsync(_lodCase, _cts.Token);

            // Refresh the read-only case info from the saved entity
            caseInfo = LineOfDutyCaseMapper.ToCaseInfoModel(_lodCase);

            // Re-snapshot only the saved model so other tabs retain their dirty state
            switch (source)
            {
                case TabNames.MemberInformation:
                    memberFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.MedicalAssessment:
                    formModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.CommanderReview:
                    commanderFormModel.TakeSnapshot(JsonOptions);
                    break;
                case TabNames.LegalSJAReview:
                    legalFormModel.TakeSnapshot(JsonOptions);
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
            isBusy = false;
            StateHasChanged();
        }
    }

    private async Task SetBusyAsync(string message = "Working...")
    {
        busyMessage = message;
        isBusy = true;
        StateHasChanged();
        await Task.Delay(500);
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
            memberFormModel.SSN = string.Empty;
            return;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length > 4)
        {
            digits = digits[..4];
        }

        memberFormModel.SSN = digits;
        StateHasChanged();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
