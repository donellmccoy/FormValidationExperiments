using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using Microsoft.Extensions.Logging;

namespace ECTSystem.Web.Pages;

/// <summary>
/// Factory for creating <see cref="LineOfDutyStateMachine"/> instances.
/// Registered as a scoped service so that <see cref="IDataService"/> and
/// <see cref="ILogger"/> are injected once and reused for every state machine
/// created during the component's lifetime.
/// </summary>
internal class LineOfDutyStateMachineFactory
{
    private readonly IDataService _dataService;
    private readonly ILogger<LineOfDutyStateMachineFactory> _logger;

    public LineOfDutyStateMachineFactory(IDataService dataService, ILogger<LineOfDutyStateMachineFactory> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new <see cref="LineOfDutyStateMachine"/> initialized with the specified
    /// LOD case. The state machine starts in the case's current
    /// <see cref="LineOfDutyCase.WorkflowState"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case to manage.</param>
    /// <returns>A fully configured <see cref="LineOfDutyStateMachine"/>.</returns>
    public LineOfDutyStateMachine Create(LineOfDutyCase lineOfDutyCase)
    {
        _logger.LogDebug("Creating state machine for case {CaseId} in state {State}",
            lineOfDutyCase.CaseId, lineOfDutyCase.WorkflowState);

        return new LineOfDutyStateMachine(lineOfDutyCase, _dataService);
    }

    /// <summary>
    /// Creates a new <see cref="LineOfDutyStateMachine"/> in the <see cref="ECTSystem.Shared.Enums.WorkflowState.Draft"/>
    /// state for a brand-new LOD case.
    /// </summary>
    /// <returns>A fully configured <see cref="LineOfDutyStateMachine"/> in Draft state.</returns>
    public LineOfDutyStateMachine Create()
    {
        _logger.LogDebug("Creating state machine for new case (Draft)");

        return new LineOfDutyStateMachine(_dataService);
    }
}
