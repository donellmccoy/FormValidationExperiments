namespace ECTSystem.Shared.ViewModels;

/// <summary>
/// Data transfer object containing all view models for a single LOD case.
/// Used for API communication between the Blazor WASM client and the Web API.
/// </summary>
public class CaseViewModelsDto
{
    public CaseInfoModel CaseInfo { get; set; } = new();
    public MemberInfoFormModel MemberInfo { get; set; } = new();
    public MedicalTechnicianFormModel MedicalTechnician { get; set; } = new();
    public MedicalAssessmentFormModel MedicalAssessment { get; set; } = new();
    public UnitCommanderFormModel UnitCommander { get; set; } = new();
    public WingJudgeAdvocateFormModel WingJudgeAdvocate { get; set; } = new();
    public WingCommanderFormModel WingCommander { get; set; } = new();
    public AppointingAuthorityFormModel AppointingAuthority { get; set; } = new();
    public BoardTechnicianFormModel BoardTechnician { get; set; } = new();
    public BoardMedicalFormModel BoardMedical { get; set; } = new();
    public BoardLegalFormModel BoardLegal { get; set; } = new();
    public BoardAdminFormModel BoardAdmin { get; set; } = new();
}
