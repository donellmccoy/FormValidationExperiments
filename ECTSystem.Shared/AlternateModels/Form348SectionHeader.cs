using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;

namespace ECTSystem.Shared.AlternateModels;

/// <summary>
/// Root model for AF Form 348 — Line of Duty Determination.
/// Composes all form sections (Parts I–VIII) into a single model.
/// </summary>
public class Form348SectionHeader : AuditableEntity
{
    // Case metadata
    public int Id { get; set; }

    public string CaseNumber { get; set; }

    public ProcessType ProcessType { get; set; } = ProcessType.Informal;


    // Part I: Member Information (Items 1–8f)
    public MemberInfoSection MemberInfo { get; set; } = new();

    // Part II: Medical Provider (Items 9–15)
    public MedicalProviderSection MedicalProvider { get; set; } = new();

    // Part III: Unit Commander (Items 16–23)
    public CommanderSection Commander { get; set; } = new();

    // Part IV: Staff Judge Advocate (Items 24–25)
    public SJASection SJA { get; set; } = new();

    // Part V: Wing Commander / Appointing Authority (Items 26–27)
    public AppointingAuthoritySection AppointingAuthority { get; set; } = new();

    // Part VI: Board Review (Items 28–33)
    public BoardReviewSection BoardReview { get; set; } = new();

    // Part VII: Approving Authority (Items 34–35, ARC only)
    public ApprovingAuthoritySection ApprovingAuthority { get; set; } = new();

    // Part VIII: Remarks / Additional Information
    public string Remarks { get; set; }
}
