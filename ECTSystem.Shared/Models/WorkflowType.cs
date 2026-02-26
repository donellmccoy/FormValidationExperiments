namespace ECTSystem.Shared.Models;

/// <summary>
/// Lookup table entity for <see cref="ECTSystem.Shared.Enums.LineOfDutyProcessType"/> enum values.
/// </summary>
public class WorkflowType : AuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int WorkflowModuleId { get; set; }
    public WorkflowModule WorkflowModule { get; set; } = null!;
    public ICollection<WorkflowStateLookup> WorkflowStates { get; set; } = new HashSet<WorkflowStateLookup>();
}
