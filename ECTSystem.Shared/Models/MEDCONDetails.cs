namespace ECTSystem.Shared.Models;

/// <summary>
/// Class representing MEDCON (Medical Continuation) details.
/// </summary>
public class MEDCONDetail : AuditableEntity
{
    public int Id { get; set; }
    public bool IsEligible { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int ExtensionDays { get; set; } // Extensions beyond 90 days require final LOD
    public bool UsesInterimLOD { get; set; }
    public string TreatmentPlan { get; set; } = string.Empty;
    public bool OutOfLocalAreaLeaveApproved { get; set; }
    public string PhysicianMemo { get; set; } = string.Empty; // For out-of-area leave
}
