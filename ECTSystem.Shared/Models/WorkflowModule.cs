namespace ECTSystem.Shared.Models;

/// <summary>
/// Lookup table entity representing a workflow module (e.g., AFRC, ANG).
/// </summary>
public class WorkflowModule : AuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<WorkflowType> WorkflowTypes { get; set; } = [];
}
