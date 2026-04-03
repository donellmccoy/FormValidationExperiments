using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.AlternateModels;

/// <summary>
/// AF Form 348 Part I — Member Information (Items 1–8f).
/// </summary>
public class MemberInfoSection
{
    // Item 1: TO (Immediate Commander)
    public string ToImmediateCommander { get; set; }

    // Item 2: FROM (Military Medical Provider Office Symbol)
    public string FromMedicalProvider { get; set; }

    // Item 2a: Facility Type
    public bool IsMTF { get; set; }
    public bool IsRMU { get; set; }
    public bool IsGMU { get; set; }
    public bool IsDeployedLocation { get; set; }

    // Item 3: Report Date
    public DateTime? ReportDate { get; set; }

    // Item 4: Name (Last, First, Middle Initial)
    public string LastName { get; set; }

    public string FirstName { get; set; }

    public string MiddleInitial { get; set; }

    // Item 5: SSN
    public string SSN { get; set; }

    // Item 6: Rank
    public MilitaryRank? Rank { get; set; }

    // Item 7: Organization/Unit
    public string OrganizationUnit { get; set; }

    // Item 8a-e: Member's Status
    public ServiceComponent? Component { get; set; }

    public bool IsUSAFA { get; set; }
    public bool IsAFROTC { get; set; }

    // Item 8f: Duration of Orders or IDT Date and Time (ARC only)
    public DateTime? OrdersStartDate { get; set; }
    public string OrdersStartTime { get; set; }
    public DateTime? OrdersEndDate { get; set; }
    public string OrdersEndTime { get; set; }
}
