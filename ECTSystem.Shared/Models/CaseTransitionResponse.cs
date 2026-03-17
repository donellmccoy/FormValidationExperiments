namespace ECTSystem.Shared.Models;

/// <summary>
/// Response payload from atomically transitioning a LOD case workflow state.
/// Contains the server-persisted history entries with assigned IDs. The client
/// merges these entries into the in-memory case rather than re-fetching.
/// </summary>
public class CaseTransitionResponse
{
    /// <summary>The persisted workflow state history entries with server-assigned IDs.</summary>
    public List<WorkflowStateHistory> HistoryEntries { get; set; } = [];
}
