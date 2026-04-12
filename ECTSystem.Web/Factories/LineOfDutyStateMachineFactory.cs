using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using ECTSystem.Web.StateMachines;
using Microsoft.Extensions.Logging;

namespace ECTSystem.Web.Factories;

/// <summary>
/// Factory for creating <see cref="LineOfDutyStateMachine"/> instances.
/// Registered as a scoped service so that <see cref="IWorkflowHistoryService"/> and
/// <see cref="ILogger"/> are injected once and reused for every state machine
/// created during the component's lifetime.
/// </summary>
internal class LineOfDutyStateMachineFactory
{
    private readonly IWorkflowHistoryService _historyService;
    private readonly ILogger<LineOfDutyStateMachineFactory> _logger;

    public LineOfDutyStateMachineFactory(IWorkflowHistoryService historyService, ILogger<LineOfDutyStateMachineFactory> logger)
    {
        _historyService = historyService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new <see cref="LineOfDutyStateMachine"/> initialized with the specified
    /// LOD case. The state machine starts in the case's current
    /// workflow state (via <see cref="LineOfDutyExtensions.GetCurrentWorkflowState"/>)ns.GetCurrentWorkflowState"/>)ns.GetCurrentWorkflowState"/>).
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case to manage.</param>
    /// <returns>A fully configured <see cref="LineOfDutyStateMachine"/>.</returns>
    public LineOfDutyStateMachine Create(LineOfDutyCase lineOfDutyCase)
    {
        _logger.LogDebug("Creating state machine for case {CaseId} in state {State}", lineOfDutyCase.CaseId, lineOfDutyCase.GetCurrentWorkflowState());

        return new LineOfDutyStateMachine(lineOfDutyCase, _historyService);
    }

    /// <summary>
    /// Creates a new <see cref="LineOfDutyStateMachine"/> in the <see cref="ECTSystem.Shared.Enums.WorkflowState.Draft"/>
    /// state for a brand-new LOD case.
    /// </summary>
    /// <returns>A fully configured <see cref="LineOfDutyStateMachine"/> in Draft state.</returns>
    public LineOfDutyStateMachine CreateDefault()
    {
        _logger.LogDebug("Creating state machine for new case (Draft)");

        return new LineOfDutyStateMachine(_historyService);
    }

    /// <summary>
    /// Creates a new <see cref="LineOfDutyStateMachine"/> in the specified starting state
    /// for a brand-new LOD case that has already been persisted with an initial workflow
    /// state history entry.
    /// </summary>
    /// <param name="workflowState">The workflow state to start the state machine in.</param>
    /// <returns>A fully configured <see cref="LineOfDutyStateMachine"/> in the specified state.</returns>
    public LineOfDutyStateMachine CreateAtState(WorkflowState workflowState)
    {
        _logger.LogDebug("Creating state machine for new case at state {State}", workflowState);

        return new LineOfDutyStateMachine(_historyService, workflowState);
    }
}
