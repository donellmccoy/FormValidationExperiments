namespace ECTSystem.Shared.Models;

/// <summary>
/// Lookup table entity for <see cref="ECTSystem.Shared.Enums.WorkflowState"/> enum values.
/// </summary>
public class WorkflowStateLookup : AuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public int WorkflowTypeId { get; set; }
    public WorkflowType WorkflowType { get; set; } = null!;
}
