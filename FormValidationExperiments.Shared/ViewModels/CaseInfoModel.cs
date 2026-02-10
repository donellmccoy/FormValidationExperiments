namespace FormValidationExperiments.Shared.ViewModels;

/// <summary>
/// Read-only view model representing the case header and summary information
/// displayed at the top of the LOD case workflow.
/// </summary>
public class CaseInfoModel
{
    /// <summary>
    /// Gets or sets the unique case tracking number (e.g., "23-884").
    /// </summary>
    public string CaseNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full name of the service member associated with this case.
    /// </summary>
    public string MemberName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member's rank abbreviation (e.g., "SrA", "TSgt").
    /// </summary>
    public string Rank { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member's assigned unit or organization (e.g., "452 AMW (AFRC)").
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date the injury or illness occurred.
    /// </summary>
    public string DateOfInjury { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the masked Social Security Number (e.g., "***-**-6789").
    /// </summary>
    public string SSN { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member's duty status at the time of the incident
    /// (e.g., "Active Duty for Training").
    /// </summary>
    public string DutyStatus { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current workflow status of the case (e.g., "In Progress", "Completed").
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a brief description of the circumstances surrounding the incident.
    /// </summary>
    public string IncidentCircumstances { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the reported injury or illness description.
    /// </summary>
    public string ReportedInjury { get; set; } = string.Empty;
}
