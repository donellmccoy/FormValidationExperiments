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
    /// Creates and adds an <see cref="Enums.TransitionAction.Entered"/> history entry
    /// for the case's current <see cref="LineOfDutyCase.WorkflowState"/>.
    /// </summary>
    /// <param name="lodCase">The LOD case to add the history entry to.</param>
    /// <param name="startDate">
    /// The start date for the workflow step. Defaults to <see cref="AuditableEntity.CreatedDate"/> if not specified.
    /// </param>
    public static void AddInitialHistory(this LineOfDutyCase lodCase, DateTime? startDate = null)
    {
        lodCase.AddHistoryEntry(WorkflowStateHistoryFactory.CreateInitialHistory(lodCase.Id, lodCase.WorkflowState, startDate ?? lodCase.CreatedDate));
    }

    /// <summary>
    /// Creates and adds a <see cref="Enums.TransitionAction.Signed"/> history entry
    /// for the case's current <see cref="LineOfDutyCase.WorkflowState"/>.
    /// </summary>
    /// <param name="lodCase">The LOD case to add the history entry to.</param>
    /// <param name="stepStartDate">The date the current workflow step started.</param>
    /// <param name="signedDate">The date the step was digitally signed.</param>
    /// <param name="signedBy">The name or identifier of the person who signed.</param>
    public static void AddSignedHistory(this LineOfDutyCase lodCase, DateTime? stepStartDate, DateTime? signedDate, string signedBy)
    {
        lodCase.AddHistoryEntry(WorkflowStateHistoryFactory.CreateSigned(lodCase.Id, lodCase.WorkflowState, stepStartDate, signedDate, signedBy));
    }
}
