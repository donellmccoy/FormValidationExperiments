using System.Text.RegularExpressions;
using FormValidationExperiments.Web.Enums;
using Microsoft.AspNetCore.Components;
using FormValidationExperiments.Web.Mapping;
using FormValidationExperiments.Web.Services;
using FormValidationExperiments.Web.ViewModels;
using FormValidationExperiments.Web.Shared;

namespace FormValidationExperiments.Web.Pages;

public partial class Home : ComponentBase
{
    [Inject]
    private ILineOfDutyCaseService CaseService { get; set; }

    [Parameter]
    public string CaseId { get; set; }

    private bool isLoading = true;

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
        var lodCase = await CaseService.GetCaseByCaseIdAsync(CaseId);

        if (lodCase is null)
        {
            // Fallback: leave models at defaults
            InitializeWorkflowSteps();
            return;
        }

        // Map domain model → view models
        caseInfo = LineOfDutyCaseMapper.ToCaseInfoModel(lodCase);
        memberFormModel = LineOfDutyCaseMapper.ToMemberInfoFormModel(lodCase);
        formModel = LineOfDutyCaseMapper.ToMedicalAssessmentFormModel(lodCase);
        commanderFormModel = LineOfDutyCaseMapper.ToCommanderReviewFormModel(lodCase);
        legalFormModel = LineOfDutyCaseMapper.ToLegalSJAReviewFormModel(lodCase);

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

    private void OnFormSubmit(MedicalAssessmentFormModel model)
    {
        // Handle medical assessment form submission
    }

    private void OnMemberFormSubmit(MemberInfoFormModel model)
    {
        // Handle member info form submission
    }

    private void OnCommanderFormSubmit(CommanderReviewFormModel model)
    {
        // Handle commander review form submission
    }

    private void OnLegalFormSubmit(LegalSJAReviewFormModel model)
    {
        // Handle legal SJA review form submission
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

    private void OnSaveDraft()
    {
        // Handle save draft
    }
}
