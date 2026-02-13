using System.Text.Json;
using FormValidationExperiments.Shared.Enums;
using Microsoft.AspNetCore.Components;
using FormValidationExperiments.Web.Services;
using FormValidationExperiments.Shared.ViewModels;
using FormValidationExperiments.Web.Shared;
using Radzen;
using Radzen.Blazor;

namespace FormValidationExperiments.Web.Pages;

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

    private bool isLoading = true;

    private bool isSaving;

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

    private WorkflowStep NextStep => currentStepIndex + 1 < workflowSteps.Count ? workflowSteps[currentStepIndex + 1] : null;

    protected override async Task OnInitializedAsync()
    {
        await LoadCaseAsync();
        isLoading = false;
    }

    private async Task LoadCaseAsync()
    {
        try
        {
            var dto = await CaseService.GetCaseViewModelsAsync(CaseId, _cts.Token);

            if (dto is null)
            {
                InitializeWorkflowSteps();
                return;
            }

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
    }

    private void TakeSnapshots()
    {
        foreach (var model in AllFormModels)
            model.TakeSnapshot(JsonOptions);
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
            return;

        await SaveCurrentTabAsync(TabNames.Draft);
    }

    private async Task OnSplitButtonClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "revert")
        {
            await OnRevertChanges();
        }
        else
        {
            await OnSaveDraft();
        }
    }

    private async Task OnMedicalForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
        {
            // Return to Med Tech
            NotificationService.Notify(NotificationSeverity.Info, "Returned to Med Tech", 
                "Case has been returned to the Medical Technician for review.");
        }
        else if (item?.Value == "cancel")
        {
            // Cancel Investigation
            NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled", 
                "The LOD investigation has been cancelled.");
        }
        else
        {
            // Forward to Unit CC (default action)
            NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Unit CC", 
                "Case has been forwarded to the Unit Commander.");
            await Task.CompletedTask;
        }
    }

    private async Task OnCommanderForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
        {
            // Return to Board Medical
            NotificationService.Notify(NotificationSeverity.Info, "Returned to Board Medical", 
                "Case has been returned to the Board Medical for review.");
        }
        else if (item?.Value == "cancel")
        {
            // Cancel Investigation
            NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled", 
                "The LOD investigation has been cancelled.");
        }
        else
        {
            // Forward to Wing JA (default action)
            NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Wing JA", 
                "Case has been forwarded to the Wing Judge Advocate.");
            await Task.CompletedTask;
        }
    }

    private async Task OnLegalForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
        {
            // Return to Unit CC
            NotificationService.Notify(NotificationSeverity.Info, "Returned to Unit CC", 
                "Case has been returned to the Unit Commander for review.");
        }
        else if (item?.Value == "cancel")
        {
            // Cancel Investigation
            NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled", 
                "The LOD investigation has been cancelled.");
        }
        else
        {
            // Forward to Wing CC (default action)
            NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Wing CC", 
                "Case has been forwarded to the Wing Commander.");
            await Task.CompletedTask;
        }
    }

    private async Task OnWingForwardClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
        {
            // Return to Wing JA
            NotificationService.Notify(NotificationSeverity.Info, "Returned to Wing JA", 
                "Case has been returned to the Wing Judge Advocate for review.");
        }
        else if (item?.Value == "cancel")
        {
            // Cancel Investigation
            NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled", 
                "The LOD investigation has been cancelled.");
        }
        else
        {
            // Forward to Board Review (default action)
            NotificationService.Notify(NotificationSeverity.Success, "Forwarded to Board Review", 
                "Case has been forwarded to the Board for review.");
            await Task.CompletedTask;
        }
    }

    private async Task OnBoardCompleteClick(RadzenSplitButtonItem item)
    {
        if (item?.Value == "return")
        {
            // Return to Wing CC
            NotificationService.Notify(NotificationSeverity.Info, "Returned to Wing CC", 
                "Case has been returned to the Wing Commander for review.");
        }
        else if (item?.Value == "cancel")
        {
            // Cancel Investigation
            NotificationService.Notify(NotificationSeverity.Warning, "Investigation Cancelled", 
                "The LOD investigation has been cancelled.");
        }
        else
        {
            // Complete Review (default action)
            NotificationService.Notify(NotificationSeverity.Success, "Review Completed", 
                "The Board review has been completed.");
            await Task.CompletedTask;
        }
    }

    private async Task SaveCurrentTabAsync(string source)
    {
        if (isSaving)
            return;

        isSaving = true;

        try
        {
            var dto = new CaseViewModelsDto
            {
                CaseInfo = caseInfo,
                MemberInfo = memberFormModel,
                MedicalAssessment = formModel,
                CommanderReview = commanderFormModel,
                LegalSJAReview = legalFormModel
            };

            var updatedInfo = await CaseService.SaveCaseAsync(CaseId, dto, _cts.Token);
            if (updatedInfo is not null)
                caseInfo = updatedInfo;

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
            isSaving = false;
            StateHasChanged();
        }
    }

    private async Task OnRevertChanges()
    {
        var confirmed = await DialogService.Confirm(
            "Revert all unsaved changes? This cannot be undone.",
            "Confirm Revert",
            new ConfirmOptions { OkButtonText = "Revert", CancelButtonText = "Cancel" });

        if (confirmed != true)
            return;

        foreach (var model in AllFormModels)
            model.Revert();

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
            digits = digits[..4];

        memberFormModel.SSN = digits;
        StateHasChanged();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
