using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

public class Member : AuditableEntity
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string MiddleInitial { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    public string ServiceNumber { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public ServiceComponent Component { get; set; }
    public DateTime? DateOfBirth { get; set; }

    public ICollection<LineOfDutyCase> LineOfDutyCases { get; set; } = new HashSet<LineOfDutyCase>();
}
