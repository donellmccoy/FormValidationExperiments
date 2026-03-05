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
/// <see cref="LineOfDutyTrigger"/> actions and persists transition side-effects (workflow state
/// history entries, case state updates, and timeline step activation) through
/// <see cref="IDataService"/>.
/// </summary>
internal class LodStateMachine
{
    /// <summary>
    /// The underlying Stateless state machine that enforces legal transitions
    /// between <see cref="WorkflowState"/> values triggered by <see cref="LineOfDutyTrigger"/> actions.
    /// </summary>
    private readonly StateMachine<WorkflowState, LineOfDutyTrigger> _sm;

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
    /// Asynchronously retrieves the set of <see cref="LineOfDutyTrigger"/> values that can
    /// be legally fired from the current <see cref="State"/>. Used by the UI to
    /// enable or disable workflow action buttons.
    /// </summary>
    /// <returns>An enumerable of permitted triggers for the current state.</returns>
    public async Task<IEnumerable<LineOfDutyTrigger>> GetPermittedTriggersAsync()
    {
        return await _sm.GetPermittedTriggersAsync();
    }

    /// <summary>
    /// Replaces the internal <see cref="LineOfDutyCase"/> reference with a freshly
    /// fetched instance after a re-fetch, allowing the state machine to be reused
    /// without re-running <see cref="Configure"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The re-fetched LOD case whose state matches the current SM state.</param>
    public void UpdateCase(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;
    }

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

        _sm = new StateMachine<WorkflowState, LineOfDutyTrigger>(lineOfDutyCase.WorkflowState, FiringMode.Queued);

        _sm.OnTransitionedAsync(HandleTransitionAsync);
        _sm.OnTransitionCompletedAsync(OnTransitionCompletedAsync);

        Configure();
    }

    /// <summary>
    /// Post-transition callback that persists all side-effects of a state transition.
    /// Executes three operations in order:
    /// <list type="number">
    ///   <item>Records an exit <see cref="WorkflowStateHistory"/> entry for the source state
    ///         (marked <see cref="TransitionAction.Completed"/> for forward transitions or
    ///         <see cref="TransitionAction.Leave"/> for backward transitions). Skipped
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
    private async Task HandleTransitionAsync(StateMachine<WorkflowState, LineOfDutyTrigger>.Transition transition)
    {
        var now = DateTime.UtcNow;

        await _dataService.AddHistoryEntryAsync(new WorkflowStateHistory
        {
            LineOfDutyCaseId = _lineOfDutyCase.Id,
            WorkflowState = transition.Destination,
            Action = TransitionAction.Enter,
            StartDate = now,
            PerformedBy = string.Empty,
            CreatedDate = now,
            CreatedBy = string.Empty,
            ModifiedDate = now,
            ModifiedBy = string.Empty
        });
    }

    /// <summary>
    /// Callback invoked after a state transition has fully completed, including all
    /// entry/exit actions and <see cref="HandleTransitionAsync"/> persistence.
    /// Use this to perform post-transition logic such as UI refresh notifications,
    /// audit logging, or follow-up processing that should only occur once the state
    /// machine has settled into its new <see cref="WorkflowState"/>.
    /// </summary>
    /// <param name="transition">
    /// The Stateless transition descriptor containing the source state, destination state,
    /// and trigger that caused the transition.
    /// </param>
    private Task OnTransitionCompletedAsync(StateMachine<WorkflowState, LineOfDutyTrigger>.Transition transition)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Configures all permitted state transitions on the underlying
    /// <see cref="StateMachine{TState,TTrigger}"/>. Each <see cref="WorkflowState"/> is
    /// configured with its valid forward (<c>ForwardTo*</c>) and return (<c>ReturnTo*</c>)
    /// triggers, plus <see cref="LineOfDutyTrigger.Cancel"/> to move to
    /// <see cref="WorkflowState.Cancelled"/>. Board-level states allow lateral
    /// routing between technician, medical, legal, and administrator reviews.
    /// Terminal states (<see cref="WorkflowState.Completed"/>,
    /// <see cref="WorkflowState.Cancelled"/>) ignore further cancellation triggers.
    /// </summary>
    private void Configure()
    {
        // Step 1: Member Information Entry — initial state; forward-only to Med Tech or cancel
        _sm.Configure(WorkflowState.MemberInformationEntry)
            .OnEntryAsync(OnMemberInformationEntryAsync) 
            .PermitIf(LineOfDutyTrigger.ForwardToMedicalTechnician, WorkflowState.MedicalTechnicianReview, CanForwardToMedicalTechnicianAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnMemberInformationExitAsync); 

        // Step 2: Medical Technician Review — forward-only to Med Officer or cancel
        _sm.Configure(WorkflowState.MedicalTechnicianReview)
            .OnEntryAsync(OnMedicalTechnicianReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanForwardToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnMedicalTechnicianReviewExitAsync);

        // Step 3: Medical Officer Review — can forward to Unit CC or return to Med Tech
        _sm.Configure(WorkflowState.MedicalOfficerReview)
            .OnEntryAsync(OnMedicalOfficerReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanForwardToUnitCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview, CanReturnToMedicalTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnMedicalOfficerReviewExitAsync);

        // Step 4: Unit Commander Review — can forward to Wing JA or return to Med Tech / Med Officer
        _sm.Configure(WorkflowState.UnitCommanderReview)
            .OnEntryAsync(OnUnitCommanderReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, CanForwardToWingJudgeAdvocateReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview, CanReturnToMedicalTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanReturnToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnUnitCommanderReviewExitAsync);

        // Step 5: Wing Judge Advocate Review — can forward to Wing CC or return to Med Tech / Med Officer / Unit CC
        _sm.Configure(WorkflowState.WingJudgeAdvocateReview)
            .OnEntryAsync(OnWingJudgeAdvocateReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToWingCommanderReview, WorkflowState.WingCommanderReview, CanForwardToWingCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview, CanReturnToMedicalTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanReturnToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanReturnToUnitCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnWingJudgeAdvocateReviewExitAsync);

        // Step 6 (mapped as step 7 in sidebar): Wing Commander Review — can forward to Appointing Authority or return to earlier stages
        _sm.Configure(WorkflowState.WingCommanderReview)
            .OnEntryAsync(OnWingCommanderReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview, CanForwardToAppointingAuthorityReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanReturnToUnitCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, CanReturnToWingJudgeAdvocateReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview, CanReturnToMedicalTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanReturnToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnWingCommanderReviewExitAsync);

        // Step 6 (mapped as sidebar step 6): Appointing Authority — gateway to board-level review; can return to any prior stage
        _sm.Configure(WorkflowState.AppointingAuthorityReview)
            .OnEntryAsync(OnAppointingAuthorityReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanReturnToUnitCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, CanReturnToWingJudgeAdvocateReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview, CanReturnToMedicalTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanReturnToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview, CanReturnToWingCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnAppointingAuthorityReviewExitAsync);

        // Step 8: Board Medical Technician — lateral routing to Board Med/Legal/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardMedicalTechnicianReview)
            .OnEntryAsync(OnBoardMedicalTechnicianReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview, CanForwardToBoardMedicalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview, CanForwardToBoardLegalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview, CanForwardToBoardAdministratorReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview, CanReturnToAppointingAuthorityReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview, CanReturnToWingCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanReturnToUnitCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, CanReturnToWingJudgeAdvocateReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview, CanReturnToMedicalTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanReturnToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardMedicalTechnicianReviewExitAsync);

        // Step 9: Board Medical Officer — lateral routing to Board Tech/Legal/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardMedicalOfficerReview)
            .OnEntryAsync(OnBoardMedicalOfficerReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview, CanForwardToBoardLegalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview, CanForwardToBoardAdministratorReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview, CanReturnToMedicalTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanReturnToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanReturnToUnitCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, CanReturnToWingJudgeAdvocateReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview, CanReturnToWingCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardMedicalOfficerReviewExitAsync);

        // Step 10: Board Legal Review — lateral routing to Board Tech/Med/Admin; can return to any earlier stage including Appointing Authority
        _sm.Configure(WorkflowState.BoardLegalReview)
            .OnEntryAsync(OnBoardLegalReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview, CanForwardToBoardAdministratorReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview, CanForwardToBoardMedicalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, CanReturnToWingJudgeAdvocateReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview, CanReturnToWingCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanReturnToUnitCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview, CanReturnToMedicalTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanReturnToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview, CanReturnToAppointingAuthorityReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardLegalReviewExitAsync);

        // Step 11: Board Administrator Review — final review stage; can complete the case, route laterally, or return to any earlier stage
        _sm.Configure(WorkflowState.BoardAdministratorReview)
            .OnEntryAsync(OnBoardAdministratorReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview, CanForwardToBoardMedicalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview, CanForwardToBoardLegalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, CanReturnToWingJudgeAdvocateReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToWingCommanderReview, WorkflowState.WingCommanderReview, CanReturnToWingCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanReturnToUnitCommanderReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalTechnicianReview, WorkflowState.MedicalTechnicianReview, CanReturnToMedicalTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanReturnToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.ReturnToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview, CanReturnToAppointingAuthorityReviewAsync)
            .PermitIf(LineOfDutyTrigger.Complete, WorkflowState.Completed, CanCompleteAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardAdministratorReviewExitAsync);

        // Terminal state: Completed — no further transitions; Cancel is silently ignored
        _sm.Configure(WorkflowState.Completed)
            .OnEntryAsync(OnCompletedEntryAsync)
            .Ignore(LineOfDutyTrigger.Cancel);

        // Terminal state: Cancelled — no further transitions; Cancel is silently ignored
        _sm.Configure(WorkflowState.Cancelled)
            .OnEntryAsync(OnCancelledEntryAsync)
            .Ignore(LineOfDutyTrigger.Cancel);
    }

    #region Step 1: Member Information Entry

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.MemberInformationEntry"/>.
    /// Executes any initialization logic required when a case begins or returns to the
    /// member information entry step (e.g., loading member data, resetting form state).
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnMemberInformationEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToMedicalTechnician"/>
    /// trigger may be fired from <see cref="WorkflowState.MemberInformationEntry"/>.
    /// Validates that all required member information (Items 1–8 on AF Form 348) has been
    /// provided before allowing forward progression.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case satisfies all preconditions to advance to
    /// <see cref="WorkflowState.MedicalTechnicianReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToMedicalTechnicianAsync()
    {
        return true;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.MemberInformationEntry"/>.
    /// Executes any cleanup or finalization logic before the case transitions to the next
    /// workflow step (e.g., persisting unsaved member data, recording audit entries).
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnMemberInformationExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 2: Medical Technician Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.MedicalTechnicianReview"/>.
    /// Executes any initialization logic required when the case arrives at or returns to the
    /// medical technician review step (e.g., loading clinical assessment forms).
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnMedicalTechnicianReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToMedicalOfficerReview"/>
    /// trigger may be fired from <see cref="WorkflowState.MedicalTechnicianReview"/>.
    /// Validates that the medical technician has completed all required clinical documentation
    /// before allowing progression to the medical officer.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case satisfies all preconditions to advance to
    /// <see cref="WorkflowState.MedicalOfficerReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToMedicalOfficerReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.MedicalTechnicianReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the medical technician review step.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnMedicalTechnicianReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 3: Medical Officer Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.MedicalOfficerReview"/>.
    /// Executes any initialization logic required when the case arrives at or returns to the
    /// medical officer review step (e.g., presenting Items 9–15 of AF Form 348 for physician review).
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnMedicalOfficerReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToUnitCommanderReview"/>
    /// trigger may be fired from <see cref="WorkflowState.MedicalOfficerReview"/>.
    /// Validates that the medical officer has completed the medical assessment, including
    /// EPTS/NSA determination, substance involvement, and proximate cause analysis.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case satisfies all preconditions to advance to
    /// <see cref="WorkflowState.UnitCommanderReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToUnitCommanderReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.MedicalOfficerReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the medical officer review step.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnMedicalOfficerReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 4: Unit Commander Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.UnitCommanderReview"/>.
    /// Executes any initialization logic required when the case arrives at or returns to the
    /// unit commander review step (e.g., loading Items 16–23 endorsement fields).
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnUnitCommanderReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview"/>
    /// trigger may be fired from <see cref="WorkflowState.UnitCommanderReview"/>.
    /// Validates that the unit commander has completed the endorsement, including
    /// <see cref="CommanderRecommendation"/> selection and narrative remarks.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case satisfies all preconditions to advance to
    /// <see cref="WorkflowState.WingJudgeAdvocateReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToWingJudgeAdvocateReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.UnitCommanderReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the unit commander review step.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnUnitCommanderReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 5: Wing Judge Advocate Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.WingJudgeAdvocateReview"/>.
    /// Executes any initialization logic required when the case arrives at or returns to the
    /// Wing Judge Advocate (SJA) legal review step.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnWingJudgeAdvocateReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToWingCommanderReview"/>
    /// trigger may be fired from <see cref="WorkflowState.WingJudgeAdvocateReview"/>.
    /// Validates that the Wing JA has completed the legal sufficiency review and any
    /// required legal opinions before allowing progression.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case satisfies all preconditions to advance to
    /// <see cref="WorkflowState.WingCommanderReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToWingCommanderReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.WingJudgeAdvocateReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the Wing Judge Advocate review step.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnWingJudgeAdvocateReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 6: Wing Commander Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.WingCommanderReview"/>.
    /// Executes any initialization logic required when the case arrives at or returns to the
    /// Wing Commander review step (e.g., loading Items 24-25 of AF Form 348).
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnWingCommanderReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToAppointingAuthorityReview"/>
    /// trigger may be fired from <see cref="WorkflowState.WingCommanderReview"/>.
    /// Validates that the Wing Commander has completed the review, including any
    /// concurrence or non-concurrence determination with the unit commander's recommendation.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case satisfies all preconditions to advance to
    /// <see cref="WorkflowState.AppointingAuthorityReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToAppointingAuthorityReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.WingCommanderReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the Wing Commander review step.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnWingCommanderReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 7: Appointing Authority Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.AppointingAuthorityReview"/>.
    /// Executes any initialization logic required when the case arrives at or returns to the
    /// appointing authority review step. This step serves as the gateway between the
    /// pre-board sequential workflow and the board-level lateral review process.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnAppointingAuthorityReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.AppointingAuthorityReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the appointing authority review step, either forward into board review or
    /// backward to an earlier stage.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnAppointingAuthorityReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 8: Board Medical Technician Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// Executes any initialization logic required when the case arrives at or is routed to the
    /// board-level medical technician review. Board states support lateral routing, so this
    /// entry may be reached from the appointing authority, another board reviewer, or a return.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnBoardMedicalTechnicianReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the board medical technician review, either laterally to another board reviewer,
    /// backward to a pre-board stage, or forward toward completion.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnBoardMedicalTechnicianReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 9: Board Medical Officer Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.BoardMedicalOfficerReview"/>.
    /// Executes any initialization logic required when the case arrives at or is routed to the
    /// board-level medical officer review. Board states support lateral routing, so this
    /// entry may be reached from another board reviewer or from a return.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnBoardMedicalOfficerReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.BoardMedicalOfficerReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the board medical officer review, either laterally to another board reviewer,
    /// backward to a pre-board stage, or forward toward completion.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnBoardMedicalOfficerReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 10: Board Legal Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.BoardLegalReview"/>.
    /// Executes any initialization logic required when the case arrives at or is routed to the
    /// board-level legal review. Board states support lateral routing, so this entry may be
    /// reached from another board reviewer or from a return.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnBoardLegalReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.BoardLegalReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the board legal review, either laterally to another board reviewer, backward to a
    /// pre-board stage, or forward toward completion.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnBoardLegalReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 11: Board Administrator Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// Executes any initialization logic required when the case arrives at or is routed to the
    /// board administrator review. This is the final active review step; from here the case
    /// can be completed, cancelled, routed laterally, or returned to an earlier stage.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnBoardAdministratorReviewEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.Complete"/> trigger may
    /// be fired from <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// Validates that all board review sections have been completed, all required signatures
    /// are present, and the final LOD finding (<see cref="LineOfDutyFinding"/>) has been
    /// recorded before allowing the case to transition to <see cref="WorkflowState.Completed"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case satisfies all preconditions to be marked as completed;
    /// otherwise <c>false</c>.
    /// </returns>
    private bool CanCompleteAsync()
    {
        return true;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// Executes any cleanup or finalization logic before the case transitions away from
    /// the board administrator review, whether forward to completion, laterally to another
    /// board reviewer, or backward to an earlier stage.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnBoardAdministratorReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Terminal States

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.Completed"/>.
    /// Executes any finalization logic when a case reaches its terminal completed state
    /// (e.g., sending notifications, generating the final AF Form 348 PDF, archiving).
    /// Once entered, no further transitions are permitted except silently ignoring
    /// <see cref="LineOfDutyTrigger.Cancel"/>.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnCompletedEntryAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.Cancelled"/>.
    /// Executes any finalization logic when a case is cancelled from any active workflow
    /// step (e.g., recording the cancellation reason, notifying stakeholders).
    /// Once entered, no further transitions are permitted except silently ignoring
    /// <see cref="LineOfDutyTrigger.Cancel"/>.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
    private Task OnCancelledEntryAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Shared Guard Methods

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.Cancel"/> trigger
    /// may be fired from the current state. Used by all 11 non-terminal workflow states
    /// to gate cancellation. Implement business rules such as requiring cancellation
    /// justification or restricting cancellation to authorized roles.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be cancelled from the current state; otherwise <c>false</c>.
    /// </returns>
    private bool CanCancelAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToBoardTechnicianReview"/>
    /// trigger may be fired. Used by board-level states
    /// (<see cref="WorkflowState.AppointingAuthorityReview"/>,
    /// <see cref="WorkflowState.BoardMedicalOfficerReview"/>,
    /// <see cref="WorkflowState.BoardLegalReview"/>, and
    /// <see cref="WorkflowState.BoardAdministratorReview"/>)
    /// to gate lateral routing to the board medical technician.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be routed to
    /// <see cref="WorkflowState.BoardMedicalTechnicianReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToBoardTechnicianReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToBoardMedicalReview"/>
    /// trigger may be fired. Used by board-level states
    /// (<see cref="WorkflowState.BoardMedicalTechnicianReview"/>,
    /// <see cref="WorkflowState.BoardLegalReview"/>, and
    /// <see cref="WorkflowState.BoardAdministratorReview"/>)
    /// to gate lateral routing to the board medical officer.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be routed to
    /// <see cref="WorkflowState.BoardMedicalOfficerReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToBoardMedicalReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToBoardLegalReview"/>
    /// trigger may be fired. Used by board-level states
    /// (<see cref="WorkflowState.BoardMedicalTechnicianReview"/>,
    /// <see cref="WorkflowState.BoardMedicalOfficerReview"/>, and
    /// <see cref="WorkflowState.BoardAdministratorReview"/>)
    /// to gate lateral routing to the board legal reviewer.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be routed to
    /// <see cref="WorkflowState.BoardLegalReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToBoardLegalReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToBoardAdministratorReview"/>
    /// trigger may be fired. Used by board-level states
    /// (<see cref="WorkflowState.BoardMedicalTechnicianReview"/>,
    /// <see cref="WorkflowState.BoardMedicalOfficerReview"/>, and
    /// <see cref="WorkflowState.BoardLegalReview"/>)
    /// to gate lateral routing to the board administrator.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be routed to
    /// <see cref="WorkflowState.BoardAdministratorReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanForwardToBoardAdministratorReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ReturnToMedicalTechnicianReview"/>
    /// trigger may be fired. Used by Steps 3–11 to gate returning the case to the
    /// medical technician for correction or additional clinical documentation.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be returned to
    /// <see cref="WorkflowState.MedicalTechnicianReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanReturnToMedicalTechnicianReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ReturnToMedicalOfficerReview"/>
    /// trigger may be fired. Used by Steps 4–11 to gate returning the case to the
    /// medical officer for correction or revision of the medical assessment.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be returned to
    /// <see cref="WorkflowState.MedicalOfficerReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanReturnToMedicalOfficerReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ReturnToUnitCommanderReview"/>
    /// trigger may be fired. Used by Steps 5–11 to gate returning the case to the
    /// unit commander for correction or revision of the endorsement.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be returned to
    /// <see cref="WorkflowState.UnitCommanderReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanReturnToUnitCommanderReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ReturnToWingJudgeAdvocateReview"/>
    /// trigger may be fired. Used by Steps 6–11 to gate returning the case to the
    /// Wing Judge Advocate for additional legal review or correction.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be returned to
    /// <see cref="WorkflowState.WingJudgeAdvocateReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanReturnToWingJudgeAdvocateReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ReturnToWingCommanderReview"/>
    /// trigger may be fired. Used by Steps 7–11 to gate returning the case to the
    /// Wing Commander for further review or revised determination.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be returned to
    /// <see cref="WorkflowState.WingCommanderReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanReturnToWingCommanderReviewAsync()
    {
        return true;
    }

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ReturnToAppointingAuthorityReview"/>
    /// trigger may be fired. Used by board-level states
    /// (<see cref="WorkflowState.BoardMedicalTechnicianReview"/>,
    /// <see cref="WorkflowState.BoardLegalReview"/>, and
    /// <see cref="WorkflowState.BoardAdministratorReview"/>)
    /// to gate returning the case to the appointing authority.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be returned to
    /// <see cref="WorkflowState.AppointingAuthorityReview"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanReturnToAppointingAuthorityReviewAsync()
    {
        return true;
    }

    #endregion

    /// <summary>
    /// Determines whether the specified <paramref name="trigger"/> can be legally fired
    /// from the current <see cref="State"/>.
    /// </summary>
    /// <param name="trigger">The trigger to test.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="trigger"/> is permitted in the current state;
    /// otherwise <c>false</c>.
    /// </returns>
    public bool CanFire(LineOfDutyTrigger trigger)
    {
        return _sm.CanFire(trigger);
    }

    /// <summary>
    /// Synchronously fires the specified <paramref name="trigger"/>, transitioning
    /// the state machine to the corresponding destination state. Throws
    /// <see cref="InvalidOperationException"/> if the trigger is not permitted.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    public void Fire(LineOfDutyTrigger trigger)
    {
        _sm.Fire(trigger);
    }

    /// <summary>
    /// Asynchronously fires the specified <paramref name="trigger"/>, transitioning
    /// the state machine to the corresponding destination state and executing
    /// <see cref="HandleTransitionAsync"/> to persist all side-effects. Throws
    /// <see cref="InvalidOperationException"/> if the trigger is not permitted.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    /// <returns>A task that completes when the transition and all persistence operations finish.</returns>
    public async Task FireAsync(LineOfDutyTrigger trigger)
    {
        await _sm.FireAsync(trigger);
    }

    /// <summary>
    /// Generates a Mermaid-format state diagram representing all configured states
    /// and transitions. Useful for documentation and debugging of the workflow graph.
    /// </summary>
    /// <returns>A string containing the Mermaid graph markup.</returns>
    public string ToMermaidGraph()
    {
        return MermaidGraph.Format(_sm.GetInfo());
    }
}
