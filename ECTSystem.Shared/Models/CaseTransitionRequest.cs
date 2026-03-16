namespace ECTSystem.Shared.Models;

/// <summary>
/// Request payload for persisting workflow state history entries during a LOD case
/// state transition. The current workflow state is derived from the most recent
/// history entry — no explicit state property is needed.
/// </summary>
public class CaseTransitionRequest
{
    /// <summary>The workflow state history entries to persist alongside the state change.</summary>
    public List<WorkflowStateHistory> HistoryEntries { get; set; } = [];
}
