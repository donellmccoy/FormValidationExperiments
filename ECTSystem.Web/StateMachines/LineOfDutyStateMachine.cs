using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Helpers;
using ECTSystem.Web.Services;
using ECTSystem.Web.ViewModels;
using Stateless;

namespace ECTSystem.Web.StateMachines;

/// <summary>
/// Encapsulates the Line of Duty (LOD) workflow state machine using the
/// <see href="https://github.com/dotnet-state-machine/stateless">Stateless</see> library.
/// Manages all valid transitions between <see cref="WorkflowState"/> values via
/// <see cref="LineOfDutyTrigger"/> actions and persists transition side-effects (workflow state
/// history entries) through <see cref="IWorkflowHistoryService"/>.
/// </summary>
internal class LineOfDutyStateMachine
{
    #region Fields

    /// <summary>
    /// The underlying Stateless state machine that enforces legal transitions
    /// between <see cref="WorkflowState"/> values triggered by <see cref="LineOfDutyTrigger"/> actions.
    /// </summary>
    private readonly StateMachine<WorkflowState, LineOfDutyTrigger> _sm;

    /// <summary>
    /// Parameterized trigger for <see cref="LineOfDutyTrigger.Return"/> that carries
    /// both the <see cref="LineOfDutyCase"/> and the target <see cref="WorkflowState"/>
    /// to return to. Configured via
    /// <see cref="StateMachine{TState,TTrigger}.SetTriggerParameters{TArg0,TArg1}"/> and
    /// used with <see cref="StateMachine{TState,TTrigger}.PermitDynamicIf{TArg0,TArg1}"/>
    /// so a single trigger replaces all <c>ReturnTo*</c> variants.
    /// </summary>
    private readonly StateMachine<WorkflowState, LineOfDutyTrigger>.TriggerWithParameters<LineOfDutyCase, WorkflowState> _returnTrigger;

    /// <summary>
    /// The LOD case whose <see cref="LineOfDutyCase.WorkflowState"/> is managed by this
    /// state machine. Updated in place during transitions and used to retrieve
    /// <see cref="LineOfDutyCase.WorkflowStateHistories"/>.
    /// </summary>
    private LineOfDutyCase _lineOfDutyCase;

    /// <summary>
    /// The workflow history service used to persist workflow state history entries
    /// during transitions (updating EndDate on the previous entry and adding new entries).
    /// </summary>
    private readonly IWorkflowHistoryService _historyService;

    /// <summary>
    /// Stores the result of the most recent transition, allowing the
    /// <see cref="HandleTransitionAsync"/> callback to communicate success or failure
    /// back to the <see cref="FireAsync(LineOfDutyTrigger)"/> and
    /// <see cref="FireAsync(LineOfDutyCase, LineOfDutyTrigger)"/> methods.
    /// </summary>
    private StateMachineResult _lastTransitionResult;

    #endregion

    #region Properties

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
    /// Gets the current <see cref="LineOfDutyCase"/> managed by this state machine.
    /// Updated internally by <see cref="HandleTransitionAsync"/> during transitions.
    /// </summary>
    public LineOfDutyCase Case => _lineOfDutyCase;

    #endregion

    #region Persistence

    /// <summary>
    /// Centralized post-transition callback registered via
    /// <see cref="StateMachine{TState,TTrigger}.OnTransitionedAsync"/>. Called automatically
    /// after every successful state transition.
    /// <para>
    /// Performs two persistence operations:
    /// <list type="number">
    /// <item>Updates the <c>EndDate</c> of the previous state's <c>InProgress</c>
    /// <see cref="WorkflowStateHistory"/> entry (if one exists) via PATCH.</item>
    /// <item>Adds a new <c>InProgress</c> <see cref="WorkflowStateHistory"/> entry for
    /// the destination state.</item>
    /// </list>
    /// </para>
    /// If either persistence call fails, the error is captured in
    /// <see cref="_lastTransitionResult"/> and no further history entries are persisted.
    /// </summary>
    /// <param name="transition">
    /// The transition metadata provided by the Stateless library, containing
    /// <see cref="StateMachine{TState,TTrigger}.Transition.Source"/> and
    /// <see cref="StateMachine{TState,TTrigger}.Transition.Destination"/>.
    /// </param>
    private async Task HandleTransitionAsync(StateMachine<WorkflowState, LineOfDutyTrigger>.Transition transition)
    {
        try
        {
            // Step 1: Close out the previous state's InProgress entry by setting its EndDate.
            var previousEntry = _lineOfDutyCase.WorkflowStateHistories
                .Where(h => h.WorkflowState == transition.Source && h.Status == WorkflowStepStatus.InProgress)
                .OrderByDescending(h => h.Id)
                .FirstOrDefault();

            if (previousEntry is not null)
            {
                var now = DateTime.UtcNow;
                previousEntry.EndDate = now;
                previousEntry.Status = WorkflowStepStatus.Completed;
                await _historyService.UpdateHistoryEndDateAsync(previousEntry.Id, now);
            }

            // Step 2: Add a new InProgress entry for the destination state.
            var newEntry = WorkflowStateHistoryFactory.CreateInitialHistory(_lineOfDutyCase.Id, transition.Destination);
            var savedEntry = await _historyService.AddHistoryEntryAsync(newEntry);
            _lineOfDutyCase.WorkflowStateHistories.Add(savedEntry);

            _lastTransitionResult = StateMachineResult.Ok(
                _lineOfDutyCase,
                WorkflowTabHelper.GetTabIndexForState(transition.Destination));
        }
        catch (Exception ex)
        {
            _lastTransitionResult = StateMachineResult.Fail(ex.Message);
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="LineOfDutyStateMachine"/> for the specified LOD case.
    /// The state machine starts in <paramref name="lineOfDutyCase"/>'s current
    /// workflow state (derived from the most recent <see cref="WorkflowStateHistory"/> entry),
    /// registers <see cref="HandleTransitionAsync"/> as the post-transition callback for
    /// persistence, and configures all permitted state transitions via <see cref="Configure"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">
    /// The LOD case to manage. Must not be <c>null</c>. The case's current workflow state
    /// (via <see cref="LineOfDutyExtensions.GetCurrentWorkflowState"/>)rkflowState"/>)rkflowState"/>) determines the initial state.
    /// </param>
    /// <param name="historyService">
    /// The workflow history service used to persist history entries during transitions.
    /// </param>
    public LineOfDutyStateMachine(LineOfDutyCase lineOfDutyCase, IWorkflowHistoryService historyService)
    {
        _lineOfDutyCase = lineOfDutyCase;
        _historyService = historyService;

        _sm = new StateMachine<WorkflowState, LineOfDutyTrigger>(lineOfDutyCase.GetCurrentWorkflowState(), FiringMode.Queued);

        _returnTrigger = _sm.SetTriggerParameters<LineOfDutyCase, WorkflowState>(LineOfDutyTrigger.Return);

        Configure();

        _sm.OnTransitionedAsync(HandleTransitionAsync);
    }

    /// <summary>
    /// Initializes a new <see cref="LineOfDutyStateMachine"/> in the <see cref="WorkflowState.Draft"/>
    /// state without an existing case. Use this constructor when creating a new LOD case from
    /// scratch; call <see cref="FireAsync(LineOfDutyCase, LineOfDutyTrigger)"/> with
    /// <see cref="LineOfDutyTrigger.ForwardToMemberInformationEntry"/> to advance past the draft state.
    /// </summary>
    /// <param name="historyService">
    /// The workflow history service used to persist history entries during transitions.
    /// </param>
    public LineOfDutyStateMachine(IWorkflowHistoryService historyService, WorkflowState workflowState = WorkflowState.Draft)
    {
        _historyService = historyService;
        _lineOfDutyCase = new LineOfDutyCase();

        _sm = new StateMachine<WorkflowState, LineOfDutyTrigger>(workflowState, FiringMode.Queued);

        _returnTrigger = _sm.SetTriggerParameters<LineOfDutyCase, WorkflowState>(LineOfDutyTrigger.Return);

        Configure();

        _sm.OnTransitionedAsync(HandleTransitionAsync);
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Configures all permitted state transitions on the underlying
    /// <see cref="StateMachine{TState,TTrigger}"/>. Each <see cref="WorkflowState"/> is
    /// configured with its valid forward (<c>ForwardTo*</c>) triggers and a single
    /// parameterized <see cref="LineOfDutyTrigger.Return"/> trigger for returning to
    /// earlier stages, plus <see cref="LineOfDutyTrigger.Cancel"/> to move to
    /// <see cref="WorkflowState.Cancelled"/>. Board-level states allow lateral
    /// routing between technician, medical, legal, and administrator reviews.
    /// Terminal states (<see cref="WorkflowState.Completed"/>,
    /// <see cref="WorkflowState.Cancelled"/>) ignore further cancellation triggers.
    /// </summary>
    private void Configure()
    {
        // Step 0: Draft — case created but not yet initiated into the LOD workflow
        _sm.Configure(WorkflowState.Draft)
            .PermitIf(LineOfDutyTrigger.ForwardToMemberInformationEntry, WorkflowState.MemberInformationEntry, CanStartLodAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnDraftExitAsync);

        // Step 1: Member Information Entry — initial state; forward-only to Med Tech or cancel
        _sm.Configure(WorkflowState.MemberInformationEntry)
            .PermitIf(LineOfDutyTrigger.ForwardToMedicalTechnician, WorkflowState.MedicalTechnicianReview, CanForwardToMedicalTechnicianAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnMemberInformationExitAsync);

        // Step 2: Medical Technician Review — forward to Med Officer, return destination, or cancel
        _sm.Configure(WorkflowState.MedicalTechnicianReview)
            .PermitIf(LineOfDutyTrigger.ForwardToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanForwardToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnMedicalTechnicianReviewExitAsync);

        // Step 3: Medical Officer Review — can forward to Unit CC or return to Med Tech
        _sm.Configure(WorkflowState.MedicalOfficerReview)
            .PermitIf(LineOfDutyTrigger.ForwardToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanForwardToUnitCommanderReviewAsync)
            .PermitDynamicIf(_returnTrigger, (_, destination) => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnMedicalOfficerReviewExitAsync);

        // Step 4: Unit Commander Review — can forward to Wing JA or return to earlier stages
        _sm.Configure(WorkflowState.UnitCommanderReview)
            .PermitIf(LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, CanForwardToWingJudgeAdvocateReviewAsync)
            .PermitDynamicIf(_returnTrigger, (_, destination) => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnUnitCommanderReviewExitAsync);

        // Step 5: Wing Judge Advocate Review — can forward to Appointing Authority or return to earlier stages
        _sm.Configure(WorkflowState.WingJudgeAdvocateReview)
            .PermitIf(LineOfDutyTrigger.ForwardToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview, CanForwardToAppointingAuthorityReviewAsync)
            .PermitDynamicIf(_returnTrigger, (_, destination) => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnWingJudgeAdvocateReviewExitAsync);

        // Step 6: Appointing Authority Review — can forward to Wing CC or return to earlier stages
        _sm.Configure(WorkflowState.AppointingAuthorityReview)
            .PermitIf(LineOfDutyTrigger.ForwardToWingCommanderReview, WorkflowState.WingCommanderReview, CanForwardToWingCommanderReviewAsync)
            .PermitDynamicIf(_returnTrigger, (_, destination) => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnAppointingAuthorityReviewExitAsync);

        // Step 7: Wing Commander Review — can forward to Board Tech or return to earlier stages
        _sm.Configure(WorkflowState.WingCommanderReview)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitDynamicIf(_returnTrigger, (_, destination) => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnWingCommanderReviewExitAsync);

        // Step 8: Board Medical Technician Review — lateral routing to Board Med/Legal/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardMedicalTechnicianReview)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview, CanForwardToBoardMedicalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview, CanForwardToBoardLegalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview, CanForwardToBoardAdministratorReviewAsync)
            .PermitDynamicIf(_returnTrigger, (_, destination) => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardMedicalTechnicianReviewExitAsync);

        // Step 9: Board Medical Officer — lateral routing to Board Tech/Legal/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardMedicalOfficerReview)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview, CanForwardToBoardLegalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview, CanForwardToBoardAdministratorReviewAsync)
            .PermitDynamicIf(_returnTrigger, (_, destination) => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardMedicalOfficerReviewExitAsync);

        // Step 10: Board Legal Review — lateral routing to Board Tech/Med/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardLegalReview)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview, CanForwardToBoardAdministratorReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview, CanForwardToBoardMedicalReviewAsync)
            .PermitDynamicIf(_returnTrigger, (_, destination) => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardLegalReviewExitAsync);

        // Step 11: Board Administrator Review — final review stage; can complete the case, route laterally, or return to any earlier stage
        _sm.Configure(WorkflowState.BoardAdministratorReview)
            .PermitIf(LineOfDutyTrigger.Complete, WorkflowState.Completed, CanCompleteAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview, CanForwardToBoardMedicalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview, CanForwardToBoardLegalReviewAsync)
            .PermitDynamicIf(_returnTrigger, (_, destination) => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardAdministratorReviewExitAsync);

        // Terminal state: Completed — no further transitions; Cancel is silently ignored
        _sm.Configure(WorkflowState.Completed)
            .Ignore(LineOfDutyTrigger.Cancel);

        // Terminal state: Cancelled — no further transitions; Cancel is silently ignored
        _sm.Configure(WorkflowState.Cancelled)
            .Ignore(LineOfDutyTrigger.Cancel);
    }

    #endregion

    #region Step 0: Draft

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.ForwardToMemberInformationEntry"/>
    /// trigger may be fired from <see cref="WorkflowState.Draft"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case satisfies all preconditions to advance to
    /// <see cref="WorkflowState.MemberInformationEntry"/>; otherwise <c>false</c>.
    /// </returns>
    private bool CanStartLodAsync()
    {
        return true;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.Draft"/>.
    /// Reserved for cleanup or finalization logic before the case transitions to the next
    /// workflow step.
    /// </summary>
    private Task OnDraftExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 1: Member Information Entry

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
    /// Reserved for cleanup or finalization logic before the case transitions to the next
    /// workflow step.
    /// </summary>
    private Task OnMemberInformationExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 2: Medical Technician Review

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
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the medical technician review step.
    /// </summary>
    private Task OnMedicalTechnicianReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 3: Medical Officer Review

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
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the medical officer review step.
    /// </summary>
    private Task OnMedicalOfficerReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 4: Unit Commander Review

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
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the unit commander review step.
    /// </summary>
    private Task OnUnitCommanderReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 5: Wing Judge Advocate Review

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
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the Wing Judge Advocate review step.
    /// </summary>
    private Task OnWingJudgeAdvocateReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 7: Wing Commander Review

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
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the Wing Commander review step.
    /// </summary>
    private Task OnWingCommanderReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 6: Appointing Authority Review

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.AppointingAuthorityReview"/>.
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the appointing authority review step, either forward into board review or
    /// backward to an earlier stage.
    /// </summary>
    private Task OnAppointingAuthorityReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 8: Board Medical Technician Review

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the board medical technician review, either laterally to another board reviewer,
    /// backward to a pre-board stage, or forward toward completion.
    /// </summary>
    private Task OnBoardMedicalTechnicianReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 9: Board Medical Officer Review

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.BoardMedicalOfficerReview"/>.
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the board medical officer review, either laterally to another board reviewer,
    /// backward to a pre-board stage, or forward toward completion.
    /// </summary>
    private Task OnBoardMedicalOfficerReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 10: Board Legal Review

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.BoardLegalReview"/>.
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the board legal review, either laterally to another board reviewer, backward to a
    /// pre-board stage, or forward toward completion.
    /// </summary>
    private Task OnBoardLegalReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 11: Board Administrator Review

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
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the board administrator review, whether forward to completion, laterally to another
    /// board reviewer, or backward to an earlier stage.
    /// </summary>
    private Task OnBoardAdministratorReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Terminal States

    #endregion

    #region Shared Guard Methods

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.Cancel"/> trigger
    /// may be fired from the current state. Used by all 11 non-terminal workflow states
    /// to gate cancellation.
    /// </summary>
    /// <remarks>
    /// Despite the <c>Async</c> suffix (retained for naming consistency with the Stateless
    /// guard API), this method is synchronous and returns <see cref="bool"/> directly.
    /// Currently returns <c>true</c> unconditionally; add business rules (e.g., role or
    /// justification checks) as needed.
    /// </remarks>
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
    /// Guard that determines whether the parameterized <see cref="LineOfDutyTrigger.Return"/>
    /// trigger may be fired from the current state. Used by all states that support
    /// returning the case to an earlier workflow step for correction or revision.
    /// </summary>
    /// <remarks>
    /// Despite the <c>Async</c> suffix (retained for naming consistency with the other
    /// guard methods), this method is synchronous. Currently returns <c>true</c>
    /// unconditionally.
    /// </remarks>
    /// <param name="lineOfDutyCase">The LOD case being returned.</param>
    /// <param name="destinationState">The <see cref="WorkflowState"/> being returned to.</param>
    /// <returns>
    /// <c>true</c> if the case may be returned to the requested destination state;
    /// otherwise <c>false</c>.
    /// </returns>
    private bool CanReturnAsync(LineOfDutyCase lineOfDutyCase, WorkflowState destinationState)
    {
        return true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Fires a simple (non-parameterized) trigger to advance the workflow.
    /// Use this overload for triggers that do not carry a <see cref="LineOfDutyCase"/>
    /// payload (e.g., <see cref="LineOfDutyTrigger.Cancel"/>).
    /// </summary>
    /// <param name="trigger">The workflow trigger to fire.</param>
    /// <returns>
    /// A <see cref="StateMachineResult"/> indicating success (with the updated case and tab
    /// index) or failure (with the error message from the failed save operation).
    /// </returns>
    public async Task<StateMachineResult> FireAsync(LineOfDutyTrigger trigger)
    {
        _lastTransitionResult = null;

        await _sm.FireAsync(trigger);

        return _lastTransitionResult ?? StateMachineResult.Ok(_lineOfDutyCase, WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase.GetCurrentWorkflowState()));
    }

    /// <summary>
    /// Fires a trigger to advance the workflow, updating the internal case reference
    /// before firing so that <see cref="HandleTransitionAsync"/> persists the latest data.
    /// </summary>
    /// <param name="newCase">The LOD case with any pending form edits applied.</param>
    /// <param name="trigger">The workflow trigger to fire.</param>
    /// <returns>
    /// A <see cref="StateMachineResult"/> indicating success (with the updated case and tab
    /// index) or failure (with the error message from the failed save operation).
    /// </returns>
    public async Task<StateMachineResult> FireAsync(LineOfDutyCase newCase, LineOfDutyTrigger trigger)
    {
        _lineOfDutyCase = newCase;
        _lastTransitionResult = null;

        await _sm.FireAsync(trigger);

        return _lastTransitionResult ?? StateMachineResult.Ok(_lineOfDutyCase, WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase.GetCurrentWorkflowState()));
    }

    /// <summary>
    /// Fires the parameterized <see cref="LineOfDutyTrigger.Return"/> trigger, transitioning the
    /// workflow back to the specified <paramref name="targetState"/>. Updates the internal case
    /// reference before firing so that <see cref="HandleTransitionAsync"/> persists the latest data.
    /// </summary>
    /// <param name="lodCase">The LOD case with any pending form edits applied.</param>
    /// <param name="targetState">The <see cref="WorkflowState"/> to return to.</param>
    /// <returns>
    /// A <see cref="StateMachineResult"/> indicating success (with the saved case and tab index)
    /// or failure (with the error message from the failed save operation).
    /// </returns>
    public async Task<StateMachineResult> FireReturnAsync(LineOfDutyCase lodCase, WorkflowState targetState)
    {
        _lineOfDutyCase = lodCase;
        _lastTransitionResult = null;

        await _sm.FireAsync(_returnTrigger, lodCase, targetState);

        return _lastTransitionResult ?? StateMachineResult.Ok(_lineOfDutyCase, WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase.GetCurrentWorkflowState()));
    }

    /// <summary>
    /// Determines whether the specified trigger can be fired from the current state,
    /// taking guard conditions into account.
    /// </summary>
    /// <param name="trigger">The trigger to test.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="trigger"/> is permitted in the current state and all
    /// associated guard conditions pass; otherwise <c>false</c>.
    /// </returns>
    public bool CanFire(LineOfDutyTrigger trigger)
    {
        return _sm.CanFire(trigger);
    }

    #endregion
}