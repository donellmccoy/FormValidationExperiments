namespace FormValidationExperiments.Shared.ViewModels;

/// <summary>
/// Data transfer object containing all view models for a single LOD case.
/// Used for API communication between the Blazor WASM client and the Web API.
/// </summary>
public class CaseViewModelsDto
{
    public CaseInfoModel CaseInfo { get; set; } = new();
    public MemberInfoFormModel MemberInfo { get; set; } = new();
    public MedicalAssessmentFormModel MedicalAssessment { get; set; } = new();
    public CommanderReviewFormModel CommanderReview { get; set; } = new();
    public LegalSJAReviewFormModel LegalSJAReview { get; set; } = new();
}
