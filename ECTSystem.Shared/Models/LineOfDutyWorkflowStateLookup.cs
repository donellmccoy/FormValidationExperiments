namespace ECTSystem.Shared.Models;

/// <summary>
/// Lookup table entity for <see cref="ECTSystem.Shared.Enums.LineOfDutyWorkflowState"/> enum values.
/// </summary>
public class LineOfDutyWorkflowStateLookup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
