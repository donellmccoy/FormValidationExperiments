using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

/// <summary>
/// Request payload for atomically transitioning a LOD case to a new workflow state
/// and persisting the associated history entries in a single database transaction.
/// </summary>
public class CaseTransitionRequest
{
    /// <summary>The new workflow state the case is transitioning to.</summary>
    public WorkflowState NewWorkflowState { get; set; }

    /// <summary>The workflow state history entries to persist alongside the state change.</summary>
    public List<WorkflowStateHistory> HistoryEntries { get; set; } = [];
}
