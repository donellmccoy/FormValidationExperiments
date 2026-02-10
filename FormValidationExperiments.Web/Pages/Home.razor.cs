using System.Text.RegularExpressions;
using FormValidationExperiments.Shared.Enums;
using Microsoft.AspNetCore.Components;
using FormValidationExperiments.Web.Services;
using FormValidationExperiments.Shared.ViewModels;
using FormValidationExperiments.Web.Shared;
using Radzen;

namespace FormValidationExperiments.Web.Pages;

public partial class Home : ComponentBase
{
    [Inject]
    private ILineOfDutyCaseService CaseService { get; set; }

    [Inject]
    private NotificationService NotificationService { get; set; }

    [Parameter]
    public string CaseId { get; set; }

    private bool isLoading = true;
    private bool isSaving;

    private int selectedTabIndex;

    private int currentStepIndex = 0;

    private MemberInfoFormModel memberFormModel = new();

    private MedicalAssessmentFormModel formModel = new();

    private CommanderReviewFormModel commanderFormModel = new();

    private LegalSJAReviewFormModel legalFormModel = new();

    private CaseInfoModel caseInfo = new();

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

    private static string FormatEnum<T>(T value) where T : Enum
    {
        return Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1");
    }

    private List<WorkflowStep> workflowSteps = [];

    private WorkflowStep CurrentStep => workflowSteps[currentStepIndex];
    
    private WorkflowStep NextStep => currentStepIndex + 1 < workflowSteps.Count ? workflowSteps[currentStepIndex + 1] : null;

    protected override async Task OnInitializedAsync()
    {
        await LoadCaseAsync();
        isLoading = false;
    }

    private async Task LoadCaseAsync()
    {
        var dto = await CaseService.GetCaseViewModelsAsync(CaseId);

        if (dto is null)
        {
            // Fallback: leave models at defaults
            InitializeWorkflowSteps();
            return;
        }

        // Populate view models from API response
        caseInfo = dto.CaseInfo;
        memberFormModel = dto.MemberInfo;
        formModel = dto.MedicalAssessment;
        commanderFormModel = dto.CommanderReview;
        legalFormModel = dto.LegalSJAReview;

        InitializeWorkflowSteps();
    }

    private void InitializeWorkflowSteps()
    {
        workflowSteps =
        [
            new() { Number = 1,  Name = "Start",                Icon = "flag",                  Status = WorkflowStepStatus.InProgress, StatusText = "Completed", CompletionDate = DateTime.Now.ToString("MM/dd/yyyy h:mm tt"), Description = "Workflow initialization and initial data entry." },
            new() { Number = 2,  Name = "Member Reports",       Icon = "person",                Status = WorkflowStepStatus.Pending,    StatusText = "Completed", CompletionDate = DateTime.Now.AddDays(-1).ToString("MM/dd/yyyy h:mm tt"), Description = "Member submission of injury details and statement." },
            new() { Number = 3,  Name = "Line Of Initiation",   Icon = "description",           Status = WorkflowStepStatus.Pending,    StatusText = "Approved",  CompletionDate = DateTime.Now.ToString("MM/dd/yyyy h:mm tt"), Description = "Formal initiation of the Line of Duty determination process." },
            new() { Number = 4,  Name = "Medical Assessment",   Icon = "medical_services",      Status = WorkflowStepStatus.Pending,    Description = "Medical provider review and clinical impact assessment." },
            new() { Number = 5,  Name = "Commander Review",     Icon = "edit_document",         Status = WorkflowStepStatus.Pending,    Description = "Commander's recommendation and endorsement." },
            new() { Number = 6,  Name = "Legal SJA Review",     Icon = "gavel",                 Status = WorkflowStepStatus.Pending,    Description = "Legal office review if deemed necessary." },
            new() { Number = 7,  Name = "Wing CC Review",       Icon = "stars",                 Status = WorkflowStepStatus.Pending,    Description = "Wing-level review if escalated." },
            new() { Number = 8,  Name = "Board Review",         Icon = "rate_review",           Status = WorkflowStepStatus.Pending,    Description = "Formal adjudication by the LOD Board." }
        ];
    }

    private async void OnFormSubmit(MedicalAssessmentFormModel model)
    {
        await SaveCurrentTabAsync("Medical Assessment");
    }

    private async void OnMemberFormSubmit(MemberInfoFormModel model)
    {
        await SaveCurrentTabAsync("Member Information");
    }

    private async void OnCommanderFormSubmit(CommanderReviewFormModel model)
    {
        await SaveCurrentTabAsync("Commander Review");
    }

    private async void OnLegalFormSubmit(LegalSJAReviewFormModel model)
    {
        await SaveCurrentTabAsync("Legal SJA Review");
    }

    private void OnStepSelected(WorkflowStep step)
    {
        currentStepIndex = step.Number - 1;

        // Update all step statuses based on the clicked step
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

    private async void OnSaveDraft()
    {
        await SaveCurrentTabAsync("Draft");
    }

    private async Task SaveCurrentTabAsync(string source)
    {
        if (isSaving)
            return;

        isSaving = true;

        try
        {
            // Build the DTO with current view model state
            var dto = new CaseViewModelsDto
            {
                CaseInfo = caseInfo,
                MemberInfo = memberFormModel,
                MedicalAssessment = formModel,
                CommanderReview = commanderFormModel,
                LegalSJAReview = legalFormModel
            };

            // Save via API — returns refreshed case info header
            var updatedInfo = await CaseService.SaveCaseAsync(CaseId, dto);
            if (updatedInfo is not null)
                caseInfo = updatedInfo;

            NotificationService.Notify(new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Saved",
                Detail = $"{source} data saved successfully.",
                Duration = 3000
            });
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
}
