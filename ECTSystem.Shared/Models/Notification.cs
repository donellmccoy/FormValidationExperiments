namespace ECTSystem.Shared.Models;

public class Notification : AuditableEntity
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public LineOfDutyCase LineOfDutyCase { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadDate { get; set; }
}
