namespace ECTSystem.Shared.Models;

public class AuditComment
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public string Text { get; set; } = string.Empty;
}
