using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;

namespace ECTSystem.Shared.Extensions;

/// <summary>
/// Extension methods for <see cref="LineOfDutyCase"/> workflow history management.
/// </summary>
public static class LineOfDutyExtensions
{
    /// <summary>
    /// Adds a pre-built <see cref="WorkflowStateHistory"/> entry to the case's history collection,
    /// initializing the collection if necessary.
    /// </summary>
    /// <param name="lodCase">The LOD case to add the history entry to.</param>
    /// <param name="entry">The workflow state history entry to add.</param>
    public static void AddHistoryEntry(this LineOfDutyCase lodCase, WorkflowStateHistory entry)
    {
        lodCase.WorkflowStateHistories ??= new HashSet<WorkflowStateHistory>();
        lodCase.WorkflowStateHistories.Add(entry);
    }

    /// <summary>
    /// Creates and adds an <see cref="Enums.TransitionAction.Enter"/> history entry
    /// for the specified <paramref name="state"/>.
    /// </summary>
    /// <param name="lodCase">The LOD case to add the history entry to.</param>
    /// <param name="state">The workflow state to record in the history entry.</param>
    /// <param name="startDate">
    /// The start date for the workflow step. Defaults to <see cref="AuditableEntity.CreatedDate"/> if not specified.
    /// </param>
    public static void AddInitialHistory(this LineOfDutyCase lodCase, WorkflowState state, DateTime? startDate = null)
    {
        lodCase.AddHistoryEntry(WorkflowStateHistoryFactory.CreateInitialHistory(lodCase.Id, state, startDate ?? lodCase.CreatedDate));
    }

    /// <summary>
    /// Derives the current workflow state from the most recent <see cref="WorkflowStateHistory"/>
    /// entry by <see cref="WorkflowStateHistory.Id"/> (descending).
    /// Returns <see cref="WorkflowState.Draft"/> if the case is <c>null</c> or has no history.
    /// </summary>
    public static WorkflowState GetCurrentWorkflowState(this LineOfDutyCase lodCase)
    {
        return lodCase?.WorkflowStateHistories?
            .OrderByDescending(h => h.Id)
            .Select(h => h.WorkflowState)
            .FirstOrDefault() ?? WorkflowState.Draft;
    }
}
