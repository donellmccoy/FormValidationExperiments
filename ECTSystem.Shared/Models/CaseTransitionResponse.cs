namespace ECTSystem.Shared.Models;

/// <summary>
/// Response payload from atomically transitioning a LOD case, containing
/// both the updated case and the server-persisted history entries with assigned IDs.
/// </summary>
public class CaseTransitionResponse
{
    /// <summary>The updated LOD case with the new workflow state applied.</summary>
    public LineOfDutyCase Case { get; set; } = null!;

    /// <summary>The persisted workflow state history entries with server-assigned IDs.</summary>
    public List<WorkflowStateHistory> HistoryEntries { get; set; } = [];
}
