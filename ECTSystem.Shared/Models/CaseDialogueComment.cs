namespace ECTSystem.Shared.Models;

public class CaseDialogueComment : AuditableEntity
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }

    public string Text { get; set; } = string.Empty;
    public int? ParentCommentId { get; set; }

    public string AuthorName { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = string.Empty;

    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
    public string AcknowledgedBy { get; set; } = string.Empty;
}
