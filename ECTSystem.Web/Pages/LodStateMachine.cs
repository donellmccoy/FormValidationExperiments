using Stateless;
using Stateless.Graph;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;

namespace ECTSystem.Web.Pages;

/// <summary>
/// Encapsulates the Line of Duty (LOD) workflow state machine using the
/// <see href="https://github.com/dotnet-state-machine/stateless">Stateless</see> library.
/// Manages all valid transitions between <see cref="WorkflowState"/> values via
/// <see cref="LodTrigger"/> actions and persists transition side-effects (workflow state
/// history entries, case state updates, and timeline step activation) through
/// <see cref="IDataService"/>.
/// </summary>
internal class LodStateMachine
{
    /// <summary>
    /// The underlying Stateless state machine that enforces legal transitions
    /// between <see cref="WorkflowState"/> values triggered by <see cref="LodTrigger"/> actions.
    /// </summary>
    private readonly StateMachine<WorkflowState, LodTrigger> _sm;

    /// <summary>
    /// The LOD case whose <see cref="LineOfDutyCase.WorkflowState"/> is managed by this
    /// state machine. Updated in place during transitions and used to retrieve
    /// <see cref="LineOfDutyCase.WorkflowStateHistories"/> and
    /// <see cref="LineOfDutyCase.TimelineSteps"/>.
    /// </summary>
    private LineOfDutyCase _lineOfDutyCase;

    /// <summary>
    /// The data service used to persist all transition side-effects, including
    /// workflow state history entries, updated case state, and timeline step activation.
    /// </summary>
    private readonly IDataService _dataService;

    /// <summary>
    /// Gets the current <see cref="WorkflowState"/> of the underlying state machine.
    /// Reflects the most recent state after any fired transitions.
    /// </summary>
    public WorkflowState State => _sm.State;

    /// <summary>
    /// Asynchronously retrieves the set of <see cref="LodTrigger"/> values that can
    /// be legally fired from the current <see cref="State"/>. Used by the UI to
    /// enable or disable workflow action buttons.
    /// </summary>
    /// <returns>An enumerable of permitted triggers for the current state.</returns>
    public async Task<IEnumerable<LodTrigger>> GetPermittedTriggersAsync() => await _sm.GetPermittedTriggersAsync();

    /// <summary>
    /// Replaces the internal <see cref="LineOfDutyCase"/> reference with a freshly
    /// fetched instance after a re-fetch, allowing the state machine to be reused
    /// without re-running <see cref="Configure"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The re-fetched LOD case whose state matches the current SM state.</param>
    public void UpdateCase(LineOfDutyCase lineOfDutyCase) => _lineOfDutyCase = lineOfDutyCase;

    /// <summary>
    /// Initializes a new <see cref="LodStateMachine"/> for the specified LOD case.
    /// The state machine starts in <paramref name="lineOfDutyCase"/>'s current
    /// <see cref="LineOfDutyCase.WorkflowState"/>, registers
    /// <see cref="HandleTransitionAsync"/> as the post-transition callback for
    /// persistence, and configures all permitted state transitions via <see cref="Configure"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">
    /// The LOD case to manage. Must not be <c>null</c>. The case's
    /// <see cref="LineOfDutyCase.WorkflowState"/> determines the initial state.
    /// </param>
    /// <param name="dataService">
    /// The data service used to persist history entries, save case state, and
    /// start timeline steps during transitions.
    /// </param>
    public LodStateMachine(LineOfDutyCase lineOfDutyCase, IDataService dataService)
    {
        _lineOfDutyCase = lineOfDutyCase;
        _dataService = dataService;
        _sm = new StateMachine<WorkflowState, LodTrigger>(lineOfDutyCase.WorkflowState);

        _sm.OnTransitionedAsync(HandleTransitionAsync);

        Configure();
    }

    /// <summary>
    /// Post-transition callback that persists all side-effects of a state transition.
    /// Executes three operations in order:
    /// <list type="number">
    ///   <item>Records an exit <see cref="WorkflowStateHistory"/> entry for the source state
    ///         (marked <see cref="TransitionAction.Completed"/> for forward transitions or
    ///         <see cref="TransitionAction.Returned"/> for backward transitions). Skipped
    ///         for terminal states (<see cref="WorkflowState.Completed"/>,
    ///         <see cref="WorkflowState.Cancelled"/>).</item>
    ///   <item>Persists the destination <see cref="WorkflowState"/> on the
    ///         <see cref="_lineOfDutyCase"/> via <see cref="IDataService.SaveCaseAsync"/>.</item>
    ///   <item>Records an entry <see cref="WorkflowStateHistory"/> for the destination state
    ///         and starts the corresponding <see cref="TimelineStep"/> if it hasn't been
    ///         started yet (skipped for terminal states).</item>
    /// </list>
    /// </summary>
    /// <param name="transition">
    /// The Stateless transition descriptor containing the source state, destination state,
    /// and trigger that caused the transition.
    /// </param>
    private async Task HandleTransitionAsync(StateMachine<WorkflowState, LodTrigger>.Transition transition)
    {
        var now = DateTime.UtcNow;
        var isForward = (int)transition.Destination > (int)transition.Source;

        // Record exit history for the source state (terminal states have no meaningful exit)
        if (transition.Source is not (WorkflowState.Completed or WorkflowState.Cancelled))
        {
            var latestHistory = _lineOfDutyCase.WorkflowStateHistories?
                .Where(h => h.WorkflowState == transition.Source)
                .OrderByDescending(h => h.Id)
                .FirstOrDefault();

            var exitHistory = isForward
                ? WorkflowStateHistoryFactory.CreateCompleted(
                    _lineOfDutyCase.Id, transition.Source, latestHistory?.StartDate, now, string.Empty)
                : WorkflowStateHistoryFactory.CreateReturned(
                    _lineOfDutyCase.Id, transition.Source, latestHistory?.StartDate,
                    latestHistory?.SignedDate, latestHistory?.SignedBy ?? string.Empty);
            await _dataService.AddHistoryEntryAsync(exitHistory);
        }

        // Persist the new workflow state on the case
        _lineOfDutyCase.WorkflowState = transition.Destination;
        await _dataService.SaveCaseAsync(_lineOfDutyCase);

        // Record entry history for the destination state
        await _dataService.AddHistoryEntryAsync(
            WorkflowStateHistoryFactory.CreateInitialHistory(_lineOfDutyCase.Id, transition.Destination, now));

        // Start the timeline step for the target state (terminal states don't have timeline steps)
        if (transition.Destination is not (WorkflowState.Completed or WorkflowState.Cancelled))
        {
            var targetIndex = (int)transition.Destination - 1;
            var timelineSteps = _lineOfDutyCase.TimelineSteps?.ToList();
            if (timelineSteps is not null && targetIndex >= 0 && targetIndex < timelineSteps.Count)
            {
                var incomingStep = timelineSteps[targetIndex];
                if (!incomingStep.StartDate.HasValue)
                {
                    await _dataService.StartTimelineStepAsync(incomingStep.Id);
                }
            }
        }
    }

    /// <summary>
    /// Configures all permitted state transitions on the underlying
    /// <see cref="StateMachine{TState,TTrigger}"/>. Each <see cref="WorkflowState"/> is
    /// configured with its valid forward (<c>ForwardTo*</c>) and return (<c>ReturnTo*</c>)
    /// triggers, plus <see cref="LodTrigger.Cancel"/> to move to
    /// <see cref="WorkflowState.Cancelled"/>. Board-level states allow lateral
    /// routing between technician, medical, legal, and administrator reviews.
    /// Terminal states (<see cref="WorkflowState.Completed"/>,
    /// <see cref="WorkflowState.Cancelled"/>) ignore further cancellation triggers.
    /// </summary>
    private void Configure()
    {
        // Step 1: Member Information Entry — initial state; forward-only to Med Tech or cancel
        _sm.Configure(WorkflowState.MemberInformationEntry)
            .Permit(LodTrigger.ForwardToMedicalTechnician, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 2: Medical Technician Review — forward-only to Med Officer or cancel
        _sm.Configure(WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ForwardToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 3: Medical Officer Review — can forward to Unit CC or return to Med Tech
        _sm.Configure(WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ForwardToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 4: Unit Commander Review — can forward to Wing JA or return to Med Tech / Med Officer
        _sm.Configure(WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ForwardToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 5: Wing Judge Advocate Review — can forward to Wing CC or return to Med Tech / Med Officer / Unit CC
        _sm.Configure(WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ForwardToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 6 (mapped as step 7 in sidebar): Wing Commander Review — can forward to Appointing Authority or return to earlier stages
        _sm.Configure(WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ForwardToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 6 (mapped as sidebar step 6): Appointing Authority — gateway to board-level review; can return to any prior stage
        _sm.Configure(WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 8: Board Medical Technician — lateral routing to Board Med/Legal/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardMedicalTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview)
            .Permit(LodTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 9: Board Medical Officer — lateral routing to Board Tech/Legal/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardMedicalOfficerReview)
            .Permit(LodTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 10: Board Legal Review — lateral routing to Board Tech/Med/Admin; can return to any earlier stage including Appointing Authority
        _sm.Configure(WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Step 11: Board Administrator Review — final review stage; can complete the case, route laterally, or return to any earlier stage
        _sm.Configure(WorkflowState.BoardAdministratorReview)
            .Permit(LodTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview)
            .Permit(LodTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview)
            .Permit(LodTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview)
            .Permit(LodTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview)
            .Permit(LodTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview)
            .Permit(LodTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview)
            .Permit(LodTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview)
            .Permit(LodTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview)
            .Permit(LodTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview)
            .Permit(LodTrigger.Complete, WorkflowState.Completed)
            .Permit(LodTrigger.Cancel, WorkflowState.Cancelled);

        // Terminal state: Completed — no further transitions; Cancel is silently ignored
        _sm.Configure(WorkflowState.Completed)
            .Ignore(LodTrigger.Cancel);

        // Terminal state: Cancelled — no further transitions; Cancel is silently ignored
        _sm.Configure(WorkflowState.Cancelled)
            .Ignore(LodTrigger.Cancel);
    }

    /// <summary>
    /// Determines whether the specified <paramref name="trigger"/> can be legally fired
    /// from the current <see cref="State"/>.
    /// </summary>
    /// <param name="trigger">The trigger to test.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="trigger"/> is permitted in the current state;
    /// otherwise <c>false</c>.
    /// </returns>
    public bool CanFire(LodTrigger trigger) => _sm.CanFire(trigger);

    /// <summary>
    /// Synchronously fires the specified <paramref name="trigger"/>, transitioning
    /// the state machine to the corresponding destination state. Throws
    /// <see cref="InvalidOperationException"/> if the trigger is not permitted.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    public void Fire(LodTrigger trigger) => _sm.Fire(trigger);

    /// <summary>
    /// Asynchronously fires the specified <paramref name="trigger"/>, transitioning
    /// the state machine to the corresponding destination state and executing
    /// <see cref="HandleTransitionAsync"/> to persist all side-effects. Throws
    /// <see cref="InvalidOperationException"/> if the trigger is not permitted.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    /// <returns>A task that completes when the transition and all persistence operations finish.</returns>
    public async Task FireAsync(LodTrigger trigger) => await _sm.FireAsync(trigger);

    /// <summary>
    /// Generates a Mermaid-format state diagram representing all configured states
    /// and transitions. Useful for documentation and debugging of the workflow graph.
    /// </summary>
    /// <returns>A string containing the Mermaid graph markup.</returns>
    public string ToMermaidGraph() => MermaidGraph.Format(_sm.GetInfo());
}
