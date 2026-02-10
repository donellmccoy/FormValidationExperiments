using FormValidationExperiments.Shared.Enums;

namespace FormValidationExperiments.Shared.ViewModels;

/// <summary>
/// Form model for the Member Information step (AF Form 348, Items 1–8).
/// Captures the requesting commander, medical provider, member identification,
/// and member status at the time of the incident.
/// </summary>
public class MemberInfoFormModel
{
    // ── Item 1: Requesting Commander ──

    /// <summary>
    /// Gets or sets the name of the requesting commander. (Item 1)
    /// </summary>
    public string RequestingCommander { get; set; } = string.Empty;

    // ── Item 2: Medical Provider ──

    /// <summary>
    /// Gets or sets the name of the medical provider. (Item 2)
    /// </summary>
    public string MedicalProvider { get; set; } = string.Empty;

    // ── Item 3: Report Date ──

    /// <summary>
    /// Gets or sets the date the LOD report was initiated. (Item 3)
    /// </summary>
    public DateTime? ReportDate { get; set; }

    // ── Item 4: Member Name ──

    /// <summary>
    /// Gets or sets the member's last name. (Item 4)
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member's first name. (Item 4)
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the member's middle initial. (Item 4)
    /// </summary>
    public string MiddleInitial { get; set; } = string.Empty;

    // ── Item 5: SSN ──

    /// <summary>
    /// Gets or sets the last four digits of the member's Social Security Number. (Item 5)
    /// </summary>
    public string SSN { get; set; } = string.Empty;

    // ── Item 6: Rank/Grade ──

    /// <summary>
    /// Gets or sets the member's military rank or grade. (Item 6)
    /// </summary>
    public MilitaryRank? Rank { get; set; }

    // ── Item 7: Organization/Unit ──

    /// <summary>
    /// Gets or sets the member's assigned organization or unit. (Item 7)
    /// </summary>
    public string OrganizationUnit { get; set; } = string.Empty;

    // ── Item 8: Member Status ──

    /// <summary>
    /// Gets or sets the member's status at the time of the incident
    /// (e.g., AFR, ANG). (Item 8)
    /// </summary>
    public MemberStatus? MemberStatus { get; set; }
}
