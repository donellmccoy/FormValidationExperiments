using FormValidationExperiments.Web.Enums;

namespace FormValidationExperiments.Web.Pages.ViewModels;

public class MemberInfoFormModel
{
    // ── Item 1: Requesting Commander ──
    public string RequestingCommander { get; set; } = string.Empty;

    // ── Item 2: Medical Provider ──
    public string MedicalProvider { get; set; } = string.Empty;

    // ── Item 3: Report Date ──
    public DateTime? ReportDate { get; set; }

    // ── Item 4: Member Name ──
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string MiddleInitial { get; set; } = string.Empty;

    // ── Item 5: SSN ──
    public string SSN { get; set; } = string.Empty;

    // ── Item 6: Rank/Grade ──
    public MilitaryRank? Rank { get; set; }

    // ── Item 7: Organization/Unit ──
    public string OrganizationUnit { get; set; } = string.Empty;

    // ── Item 8: Member Status ──
    public MemberStatus? MemberStatus { get; set; }
}
