namespace ECTSystem.Shared.Models;

/// <summary>
/// Represents a user's bookmark on an LOD case.
/// </summary>
public class CaseBookmark
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int LineOfDutyCaseId { get; set; }
    public DateTime BookmarkedDate { get; set; }
}
