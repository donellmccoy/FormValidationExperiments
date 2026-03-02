using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;

namespace ECTSystem.Web.Pages;

/// <summary>
/// Factory for creating <see cref="LodStateMachine"/> instances from a
/// <see cref="LineOfDutyCase"/> and an <see cref="IDataService"/>.
/// </summary>
internal static class LodStateMachineFactory
{
    /// <summary>
    /// Creates a new <see cref="LodStateMachine"/> initialized with the specified
    /// LOD case and data service. The state machine starts in the case's current
    /// <see cref="LineOfDutyCase.WorkflowState"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case to manage.</param>
    /// <param name="dataService">The data service used to persist transition side-effects.</param>
    /// <returns>A fully configured <see cref="LodStateMachine"/>.</returns>
    public static LodStateMachine Create(LineOfDutyCase lineOfDutyCase, IDataService dataService)
    {
        return new LodStateMachine(lineOfDutyCase, dataService);
    }
}
