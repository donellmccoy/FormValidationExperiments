using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Extensions;
using ECTSystem.Web.Services;
using Radzen;
using Stateless;
using Stateless.Graph;

namespace ECTSystem.Web.Pages;

/// <summary>
/// Encapsulates the Line of Duty (LOD) workflow state machine using the
/// <see href="https://github.com/dotnet-state-machine/stateless">Stateless</see> library.
/// Manages all valid transitions between <see cref="WorkflowState"/> values via
/// <see cref="LineOfDutyTrigger"/> actions and persists transition side-effects (workflow state
/// history entries, case state updates, and timeline step activation) through
/// <see cref="IDataService"/>.
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
    /// the target <see cref="WorkflowState"/> to return to. Configured via
    /// <see cref="StateMachine{TState,TTrigger}.SetTriggerParameters{TArg0}"/> and
    /// used with <see cref="StateMachine{TState,TTrigger}.PermitDynamicIf{TArg0}"/>
    /// so a single trigger replaces all <c>ReturnTo*</c> variants.
    /// </summary>
    private StateMachine<WorkflowState, LineOfDutyTrigger>.TriggerWithParameters<WorkflowState> _returnTrigger;

    /// <summary>
    /// Dictionary mapping each <see cref="LineOfDutyTrigger"/> to its corresponding
    /// <see cref="StateMachine{TState,TTrigger}.TriggerWithParameters{TArg0}"/> carrying
    /// a <see cref="LineOfDutyCase"/>. Populated by <see cref="RegisterCaseTriggers"/>.
    /// </summary>
    private Dictionary<LineOfDutyTrigger, StateMachine<WorkflowState, LineOfDutyTrigger>.TriggerWithParameters<LineOfDutyCase>> _caseTriggers;

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

    #endregion

    #region Callbacks

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.MemberInformationEntry"/> from the
    /// <see cref="LineOfDutyTrigger.StartLineOfDutyCase"/> trigger. Allows the UI layer
    /// (e.g., <c>EditCase</c>) to react to case creation without the state machine
    /// holding a reference to UI components.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnMemberInformationEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.MedicalTechnicianReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnMedicalTechnicianReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.MedicalOfficerReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnMedicalOfficerReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.UnitCommanderReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnUnitCommanderReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.WingJudgeAdvocateReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnWingJudgeAdvocateReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.WingCommanderReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnWingCommanderReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.AppointingAuthorityReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnAppointingAuthorityReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnBoardMedicalTechnicianReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.BoardMedicalOfficerReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnBoardMedicalOfficerReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.BoardLegalReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnBoardLegalReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnBoardAdministratorReviewEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.Completed"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnCompletedEntered { get; set; }

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.Cancelled"/>.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnCancelledEntered { get; set; }

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
    /// </summary>
    public LineOfDutyCase Case => _lineOfDutyCase;

    #endregion

    #region Persistence

    /// <summary>
    /// Persists the current case entity via the data service and returns the saved entity
    /// with its current tab index.
    /// </summary>
    /// <param name="ct">A cancellation token that can be used to cancel the save operation.</param>
    /// <returns>
    /// A <see cref="StateMachineResult"/> indicating success (with the saved case and its tab
    /// index) or failure (with the exception message).
    /// </returns>
    public async Task<StateMachineResult> SaveCaseAsync(CancellationToken ct = default)
    {
        try
        {
            _lineOfDutyCase = await _dataService.SaveCaseAsync(_lineOfDutyCase, ct);
            var tabIndex = WorkflowTabHelper.GetTabIndexForState(_lineOfDutyCase.WorkflowState);
            return StateMachineResult.Ok(_lineOfDutyCase, tabIndex);
        }
        catch (Exception ex)
        {
            return StateMachineResult.Fail(ex.Message);
        }
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new <see cref="LineOfDutyStateMachine"/> for the specified LOD case.
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
    public LineOfDutyStateMachine(LineOfDutyCase lineOfDutyCase, IDataService dataService)
    {
        _lineOfDutyCase = lineOfDutyCase;
        _dataService = dataService;

        _sm = new StateMachine<WorkflowState, LineOfDutyTrigger>(lineOfDutyCase.WorkflowState, FiringMode.Queued);

        _returnTrigger = _sm.SetTriggerParameters<WorkflowState>(LineOfDutyTrigger.Return);

        RegisterCaseTriggers();

        Configure();
    }

    /// <summary>
    /// Initializes a new <see cref="LineOfDutyStateMachine"/> in the <see cref="WorkflowState.Draft"/>
    /// state without an existing case. Use this constructor when creating a new LOD case from
    /// scratch; call <see cref="FireAsync(LineOfDutyCase, LineOfDutyTrigger)"/> with
    /// <see cref="LineOfDutyTrigger.StartLineOfDutyCase"/> to advance past the draft state.
    /// </summary>
    /// <param name="dataService">
    /// The data service used to persist history entries, save case state, and
    /// start timeline steps during transitions.
    /// </param>
    public LineOfDutyStateMachine(IDataService dataService)
    {
        _dataService = dataService;

        _sm = new StateMachine<WorkflowState, LineOfDutyTrigger>(WorkflowState.Draft, FiringMode.Queued);

        _returnTrigger = _sm.SetTriggerParameters<WorkflowState>(LineOfDutyTrigger.Return);

        RegisterCaseTriggers();

        Configure();
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Registers all <see cref="LineOfDutyCase"/>-parameterized triggers in the
    /// <see cref="_caseTriggers"/> dictionary. Called from both constructors to avoid
    /// duplicating the trigger registration list.
    /// </summary>
    private void RegisterCaseTriggers()
    {
        _caseTriggers = new Dictionary<LineOfDutyTrigger, StateMachine<WorkflowState, LineOfDutyTrigger>.TriggerWithParameters<LineOfDutyCase>>
        {
            [LineOfDutyTrigger.StartLineOfDutyCase] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.StartLineOfDutyCase),
            [LineOfDutyTrigger.ForwardToMedicalTechnician] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToMedicalTechnician),
            [LineOfDutyTrigger.ForwardToMedicalOfficerReview] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToMedicalOfficerReview),
            [LineOfDutyTrigger.ForwardToUnitCommanderReview] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToUnitCommanderReview),
            [LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview),
            [LineOfDutyTrigger.ForwardToWingCommanderReview] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToWingCommanderReview),
            [LineOfDutyTrigger.ForwardToAppointingAuthorityReview] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToAppointingAuthorityReview),
            [LineOfDutyTrigger.ForwardToBoardTechnicianReview] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToBoardTechnicianReview),
            [LineOfDutyTrigger.ForwardToBoardMedicalReview] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToBoardMedicalReview),
            [LineOfDutyTrigger.ForwardToBoardLegalReview] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToBoardLegalReview),
            [LineOfDutyTrigger.ForwardToBoardAdministratorReview] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.ForwardToBoardAdministratorReview),
            [LineOfDutyTrigger.Complete] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.Complete),
            [LineOfDutyTrigger.Cancel] = _sm.SetTriggerParameters<LineOfDutyCase>(LineOfDutyTrigger.Cancel),
        };
    }

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
            .PermitIf(LineOfDutyTrigger.StartLineOfDutyCase, WorkflowState.MemberInformationEntry, CanStartLodAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnDraftExitAsync);

        // Step 1: Member Information Entry — initial state; forward-only to Med Tech or cancel
        _sm.Configure(WorkflowState.MemberInformationEntry)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.StartLineOfDutyCase], OnMemberInformationEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToMedicalTechnician, WorkflowState.MedicalTechnicianReview, CanForwardToMedicalTechnicianAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnMemberInformationExitAsync);

        // Step 2: Medical Technician Review — forward-only to Med Officer or cancel
        _sm.Configure(WorkflowState.MedicalTechnicianReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToMedicalTechnician], OnMedicalTechnicianReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, CanForwardToMedicalOfficerReviewAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnMedicalTechnicianReviewExitAsync);

        // Step 3: Medical Officer Review — can forward to Unit CC or return to Med Tech
        _sm.Configure(WorkflowState.MedicalOfficerReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToMedicalOfficerReview], OnMedicalOfficerReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToUnitCommanderReview, WorkflowState.UnitCommanderReview, CanForwardToUnitCommanderReviewAsync)
            .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnMedicalOfficerReviewExitAsync);

        // Step 4: Unit Commander Review — can forward to Wing JA or return to earlier stages
        _sm.Configure(WorkflowState.UnitCommanderReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToUnitCommanderReview], OnUnitCommanderReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, CanForwardToWingJudgeAdvocateReviewAsync)
            .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnUnitCommanderReviewExitAsync);

        // Step 5: Wing Judge Advocate Review — can forward to Wing CC or return to earlier stages
        _sm.Configure(WorkflowState.WingJudgeAdvocateReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview], OnWingJudgeAdvocateReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToWingCommanderReview, WorkflowState.WingCommanderReview, CanForwardToWingCommanderReviewAsync)
            .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnWingJudgeAdvocateReviewExitAsync);

        // Step 6: Wing Commander Review — can forward to Appointing Authority or return to earlier stages
        _sm.Configure(WorkflowState.WingCommanderReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToWingCommanderReview], OnWingCommanderReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview, CanForwardToAppointingAuthorityReviewAsync)
            .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnWingCommanderReviewExitAsync);

        // Step 7: Appointing Authority Review — can forward to Board Tech or return to earlier stages
        _sm.Configure(WorkflowState.AppointingAuthorityReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToAppointingAuthorityReview], OnAppointingAuthorityReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnAppointingAuthorityReviewExitAsync);

        // Step 8: Board Medical Technician Review — lateral routing to Board Med/Legal/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardMedicalTechnicianReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToBoardTechnicianReview], OnBoardMedicalTechnicianReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview, CanForwardToBoardMedicalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview, CanForwardToBoardLegalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview, CanForwardToBoardAdministratorReviewAsync)
            .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardMedicalTechnicianReviewExitAsync);

        // Step 9: Board Medical Officer — lateral routing to Board Tech/Legal/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardMedicalOfficerReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToBoardMedicalReview], OnBoardMedicalOfficerReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview, CanForwardToBoardLegalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview, CanForwardToBoardAdministratorReviewAsync)
            .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardMedicalOfficerReviewExitAsync);

        // Step 10: Board Legal Review — lateral routing to Board Tech/Med/Admin; can return to any earlier stage
        _sm.Configure(WorkflowState.BoardLegalReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToBoardLegalReview], OnBoardLegalReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardAdministratorReview, WorkflowState.BoardAdministratorReview, CanForwardToBoardAdministratorReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview, CanForwardToBoardMedicalReviewAsync)
            .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardLegalReviewExitAsync);

        // Step 11: Board Administrator Review — final review stage; can complete the case, route laterally, or return to any earlier stage
        _sm.Configure(WorkflowState.BoardAdministratorReview)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.ForwardToBoardAdministratorReview], OnBoardAdministratorReviewEntryAsync)
            .PermitIf(LineOfDutyTrigger.Complete, WorkflowState.Completed, CanCompleteAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, CanForwardToBoardTechnicianReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardMedicalReview, WorkflowState.BoardMedicalOfficerReview, CanForwardToBoardMedicalReviewAsync)
            .PermitIf(LineOfDutyTrigger.ForwardToBoardLegalReview, WorkflowState.BoardLegalReview, CanForwardToBoardLegalReviewAsync)
            .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
            .PermitIf(LineOfDutyTrigger.Cancel, WorkflowState.Cancelled, CanCancelAsync)
            .OnExitAsync(OnBoardAdministratorReviewExitAsync);

        // Terminal state: Completed — no further transitions; Cancel is silently ignored
        _sm.Configure(WorkflowState.Completed)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.Complete], OnCompletedEntryAsync)
            .Ignore(LineOfDutyTrigger.Cancel);

        // Terminal state: Cancelled — no further transitions; Cancel is silently ignored
        _sm.Configure(WorkflowState.Cancelled)
            .OnEntryFromAsync(_caseTriggers[LineOfDutyTrigger.Cancel], OnCancelledEntryAsync)
            .Ignore(LineOfDutyTrigger.Cancel);
    }

    #endregion

    #region Step 0: Draft

    /// <summary>
    /// Guard that determines whether the <see cref="LineOfDutyTrigger.StartLineOfDutyCase"/>
    /// trigger may be fired from <see cref="WorkflowState.Draft"/>.
    /// Validates that the case satisfies all preconditions to begin the LOD workflow.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be initiated into the LOD workflow;
    /// otherwise <c>false</c>.
    /// </returns>
    private bool CanStartLodAsync()
    {
        return true;
    }

    /// <summary>
    /// Called when the state machine exits <see cref="WorkflowState.Draft"/>.
    /// Persists the case to the API for the first time, transitioning it from
    /// an unsaved draft to a tracked LOD case in the system.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when exit side-effects are needed.</returns>
    private Task OnDraftExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion
    
    #region Step 1: Member Information Entry

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.MemberInformationEntry"/>
    /// from the <see cref="LineOfDutyTrigger.StartLineOfDutyCase"/> trigger. Updates the case's
    /// workflow state, records a history entry, persists the case, and invokes
    /// <see cref="OnMemberInformationEntered"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case being initiated into the workflow.</param>
    private async Task OnMemberInformationEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.MemberInformationEntry);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.MemberInformationEntry);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnMemberInformationEntered?.Invoke(saved);
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
    /// Called when the state machine enters <see cref="WorkflowState.MedicalTechnicianReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnMedicalTechnicianReviewEntered"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into medical technician review.</param>
    private async Task OnMedicalTechnicianReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.MedicalTechnicianReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.MedicalTechnicianReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnMedicalTechnicianReviewEntered?.Invoke(saved);
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
    /// Called when the state machine enters <see cref="WorkflowState.MedicalOfficerReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnMedicalOfficerReviewEntered"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into medical officer review.</param>
    private async Task OnMedicalOfficerReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.MedicalOfficerReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.MedicalOfficerReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnMedicalOfficerReviewEntered?.Invoke(saved);
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
    /// Called when the state machine enters <see cref="WorkflowState.UnitCommanderReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnUnitCommanderReviewEntered"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into unit commander review.</param>
    private async Task OnUnitCommanderReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.UnitCommanderReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.UnitCommanderReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnUnitCommanderReviewEntered?.Invoke(saved);
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
    /// Called when the state machine enters <see cref="WorkflowState.WingJudgeAdvocateReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnWingJudgeAdvocateReviewEntered"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into Wing Judge Advocate review.</param>
    private async Task OnWingJudgeAdvocateReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.WingJudgeAdvocateReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.WingJudgeAdvocateReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnWingJudgeAdvocateReviewEntered?.Invoke(saved);
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
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the Wing Judge Advocate review step.
    /// </summary>
    private Task OnWingJudgeAdvocateReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 6: Wing Commander Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.WingCommanderReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnWingCommanderReviewEntered"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into Wing Commander review.</param>
    private async Task OnWingCommanderReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.WingCommanderReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.WingCommanderReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnWingCommanderReviewEntered?.Invoke(saved);
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
    /// Reserved for cleanup or finalization logic before the case transitions away from
    /// the Wing Commander review step.
    /// </summary>
    private Task OnWingCommanderReviewExitAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Step 7: Appointing Authority Review

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.AppointingAuthorityReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnAppointingAuthorityReviewEntered"/>. This step serves as the
    /// gateway between the pre-board sequential workflow and the board-level lateral
    /// review process.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into appointing authority review.</param>
    private async Task OnAppointingAuthorityReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.AppointingAuthorityReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.AppointingAuthorityReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnAppointingAuthorityReviewEntered?.Invoke(saved);
    }

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
    /// Called when the state machine enters <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnBoardMedicalTechnicianReviewEntered"/>. Board states support
    /// lateral routing, so this entry may be reached from the appointing authority, another
    /// board reviewer, or a return.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into board medical technician review.</param>
    private async Task OnBoardMedicalTechnicianReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.BoardMedicalTechnicianReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.BoardMedicalTechnicianReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnBoardMedicalTechnicianReviewEntered?.Invoke(saved);
    }

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
    /// Called when the state machine enters <see cref="WorkflowState.BoardMedicalOfficerReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnBoardMedicalOfficerReviewEntered"/>. Board states support
    /// lateral routing, so this entry may be reached from another board reviewer or
    /// from a return.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into board medical officer review.</param>
    private async Task OnBoardMedicalOfficerReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.BoardMedicalOfficerReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.BoardMedicalOfficerReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnBoardMedicalOfficerReviewEntered?.Invoke(saved);
    }

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
    /// Called when the state machine enters <see cref="WorkflowState.BoardLegalReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnBoardLegalReviewEntered"/>. Board states support lateral
    /// routing, so this entry may be reached from another board reviewer or from a return.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into board legal review.</param>
    private async Task OnBoardLegalReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.BoardLegalReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.BoardLegalReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnBoardLegalReviewEntered?.Invoke(saved);
    }

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
    /// Called when the state machine enters <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnBoardAdministratorReviewEntered"/>. This is the final active
    /// review step; from here the case can be completed, cancelled, routed laterally, or
    /// returned to an earlier stage.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning into board administrator review.</param>
    private async Task OnBoardAdministratorReviewEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.BoardAdministratorReview);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.BoardAdministratorReview);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnBoardAdministratorReviewEntered?.Invoke(saved);
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

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.Completed"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnCompletedEntered"/>. Once entered, no further transitions
    /// are permitted except silently ignoring <see cref="LineOfDutyTrigger.Cancel"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning to the completed terminal state.</param>
    private async Task OnCompletedEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.Completed);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.Completed);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnCompletedEntered?.Invoke(saved);
    }

    /// <summary>
    /// Called when the state machine enters <see cref="WorkflowState.Cancelled"/>.
    /// Updates the case's workflow state, records a history entry, persists the case, and
    /// invokes <see cref="OnCancelledEntered"/>. Once entered, no further transitions
    /// are permitted except silently ignoring <see cref="LineOfDutyTrigger.Cancel"/>.
    /// </summary>
    /// <param name="lineOfDutyCase">The LOD case transitioning to the cancelled terminal state.</param>
    private async Task OnCancelledEntryAsync(LineOfDutyCase lineOfDutyCase)
    {
        _lineOfDutyCase = lineOfDutyCase;

        _lineOfDutyCase.UpdateWorkflowState(WorkflowState.Cancelled);

        _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.Cancelled);

        var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

        await OnCancelledEntered?.Invoke(saved);
    }

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
    /// <returns>
    /// <c>true</c> if the case may be returned to the requested destination state;
    /// otherwise <c>false</c>.
    /// </returns>
    private bool CanReturnAsync()
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
    /// <returns>A task that completes when the transition and all entry/exit handlers have finished.</returns>
    public async Task FireAsync(LineOfDutyTrigger trigger)
    {
        await _sm.FireAsync(trigger);
    }

    /// <summary>
    /// Fires a parameterized trigger, passing the specified <see cref="LineOfDutyCase"/> into
    /// the entry handler of the destination state. Use this overload for triggers registered
    /// in <see cref="RegisterCaseTriggers"/> (e.g., forward and route triggers).
    /// </summary>
    /// <param name="newCase">The LOD case to pass to the destination state's entry handler.</param>
    /// <param name="trigger">The workflow trigger to fire. Must be a key in <see cref="_caseTriggers"/>.</param>
    /// <returns>A task that completes when the transition and all entry/exit handlers have finished.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="trigger"/> is not registered as a parameterized case trigger.
    /// </exception>
    public async Task FireAsync(LineOfDutyCase newCase, LineOfDutyTrigger trigger)
    {
        if (!_caseTriggers.TryGetValue(trigger, out var paramTrigger))
        {
            throw new InvalidOperationException($"Trigger '{trigger}' is not configured as a parameterized case trigger.");
        }

        await _sm.FireAsync(paramTrigger, newCase);
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