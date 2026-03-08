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
internal class LodStateMachine
{
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

    /// <summary>
    /// Optional callback invoked when the state machine enters
    /// <see cref="WorkflowState.MemberInformationEntry"/> from the
    /// <see cref="LineOfDutyTrigger.StartLineOfDutyCase"/> trigger. Allows the UI layer
    /// (e.g., <c>EditCase</c>) to react to case creation without the state machine
    /// holding a reference to UI components.
    /// </summary>
    public Func<LineOfDutyCase, Task> OnMemberInformationEntered { get; set; }

    /// <summary>
    /// Gets or sets the callback that is invoked when the medical technician review is entered for a Line of Duty case.
    /// </summary>
    /// <remarks>The assigned delegate should perform any actions required when the medical technician review
    /// step is completed, such as saving data or updating workflow status. The callback receives the current Line of
    /// Duty case as its parameter and executes asynchronously.</remarks>
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
    /// Gets the current <see cref="LineOfDutyCase"/> managed by this state machine.
    /// </summary>
    public LineOfDutyCase Case => _lineOfDutyCase;

    #region Tab/State Mapping

    /// <summary>
    /// Provides a mapping between workflow tab names and their corresponding workflow states for the Line of Duty
    /// determination process.
    /// </summary>
    /// <remarks>This array is used to associate each UI tab in the workflow wizard with its logical workflow
    /// state, enabling navigation and state tracking throughout the multi-step process.</remarks>
    private static readonly (string TabName, WorkflowState State)[] WorkflowTabMap =
    [
        ("Member Information",       WorkflowState.MemberInformationEntry),        
        ("Medical Technician",       WorkflowState.MedicalTechnicianReview),       
        ("Medical Officer",          WorkflowState.MedicalOfficerReview),       
        ("Unit CC Review",           WorkflowState.UnitCommanderReview),           
        ("Wing JA Review",           WorkflowState.WingJudgeAdvocateReview),       
        ("Wing CC Review",           WorkflowState.WingCommanderReview),           
        ("Appointing Authority",     WorkflowState.AppointingAuthorityReview),     
        ("Board Technician Review",  WorkflowState.BoardMedicalTechnicianReview),  
        ("Board Medical Review",     WorkflowState.BoardMedicalOfficerReview),     
        ("Board Legal Review",       WorkflowState.BoardLegalReview),              
        ("Board Admin Review",       WorkflowState.BoardAdministratorReview),     
    ];

    /// <summary>
    /// Returns the tab index that corresponds to the given <see cref="WorkflowState"/>.
    /// Terminal states (Completed, Cancelled) map to the last workflow tab; Draft maps to 0.
    /// </summary>
    public static int GetTabIndexForState(WorkflowState state)
    {
        for (var i = 0; i < WorkflowTabMap.Length; i++)
        {
            if (WorkflowTabMap[i].State == state)
            {
                return i;
            }
        }

        return state switch
        {
            WorkflowState.Completed => WorkflowTabMap.Length - 1,
            WorkflowState.Draft => 0,
            _ => 0
        };
    }

    /// <summary>
    /// Determines whether a tab at the given index should be disabled based on the current workflow state.
    /// Tabs beyond index 10 (e.g., Documents, Timeline) are always enabled.
    /// </summary>
    public bool IsTabDisabled(int tabIndex)
    {
        return tabIndex < WorkflowTabMap.Length && tabIndex > GetTabIndexForState(_lineOfDutyCase?.WorkflowState ?? WorkflowState.Draft);
    }

    #endregion

    #region Transition Metadata

    /// <summary>
    /// Workflow transitions shared across all source states (return-* and board-* actions).
    /// </summary>
    private static readonly Dictionary<string, WorkflowTransition> SharedTransitions = new()
    {
        ["return-med-tech"] = new(
            LineOfDutyTrigger.Return,
            WorkflowState.MedicalTechnicianReview,
            "Are you sure you want to return this case to the Medical Technician?",
            "Confirm Return", "Return",
            "Returning to Medical Technician...",
            NotificationSeverity.Info, "Returned to Medical Technician",
            "Case has been returned to the Medical Technician for review."),

        ["return-med-officer"] = new(
            LineOfDutyTrigger.Return,
            WorkflowState.MedicalOfficerReview,
            "Are you sure you want to return this case to the Medical Officer?",
            "Confirm Return", "Return",
            "Returning to Medical Officer...",
            NotificationSeverity.Info, "Returned to Medical Officer",
            "Case has been returned to the Medical Officer for review."),

        ["return-unit-cc"] = new(
            LineOfDutyTrigger.Return,
            WorkflowState.UnitCommanderReview,
            "Are you sure you want to return this case to the Unit Commander?",
            "Confirm Return", "Return",
            "Returning to Unit CC...",
            NotificationSeverity.Info, "Returned to Unit CC",
            "Case has been returned to the Unit Commander for review."),

        ["return-wing-ja"] = new(
            LineOfDutyTrigger.Return,
            WorkflowState.WingJudgeAdvocateReview,
            "Are you sure you want to return this case to the Wing Judge Advocate?",
            "Confirm Return", "Return",
            "Returning to Wing JA...",
            NotificationSeverity.Info, "Returned to Wing JA",
            "Case has been returned to the Wing Judge Advocate for review."),

        ["return-wing-cc"] = new(
            LineOfDutyTrigger.Return,
            WorkflowState.WingCommanderReview,
            "Are you sure you want to return this case to the Wing Commander?",
            "Confirm Return", "Return",
            "Returning to Wing CC...",
            NotificationSeverity.Info, "Returned to Wing CC",
            "Case has been returned to the Wing Commander for review."),

        ["return-appointing-authority"] = new(
            LineOfDutyTrigger.Return,
            WorkflowState.AppointingAuthorityReview,
            "Are you sure you want to return this case to the Appointing Authority?",
            "Confirm Return", "Return",
            "Returning to Appointing Authority...",
            NotificationSeverity.Info, "Returned to Appointing Authority",
            "Case has been returned to the Appointing Authority for review."),

        ["board-tech"] = new(
            LineOfDutyTrigger.ForwardToBoardTechnicianReview,
            WorkflowState.BoardMedicalTechnicianReview,
            "Are you sure you want to forward this case to the Board Technician?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Technician...",
            NotificationSeverity.Success, "Forwarded to Board Technician",
            "Case has been forwarded to the Board Technician."),

        ["board-med"] = new(
            LineOfDutyTrigger.ForwardToBoardMedicalReview,
            WorkflowState.BoardMedicalOfficerReview,
            "Are you sure you want to forward this case to the Board Medical reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Medical...",
            NotificationSeverity.Success, "Forwarded to Board Medical",
            "Case has been forwarded to the Board Medical reviewer."),

        ["board-legal"] = new(
            LineOfDutyTrigger.ForwardToBoardLegalReview,
            WorkflowState.BoardLegalReview,
            "Are you sure you want to forward this case to the Board Legal reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Legal...",
            NotificationSeverity.Success, "Forwarded to Board Legal",
            "Case has been forwarded to the Board Legal reviewer."),

        ["board-admin"] = new(
            LineOfDutyTrigger.ForwardToBoardAdministratorReview,
            WorkflowState.BoardAdministratorReview,
            "Are you sure you want to forward this case to the Board Admin reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Admin...",
            NotificationSeverity.Success, "Forwarded to Board Admin",
            "Case has been forwarded to the Board Admin reviewer."),
    };

    /// <summary>
    /// Workflow transitions that depend on the source state: default forwards and context-dependent actions.
    /// </summary>
    private static readonly Dictionary<(WorkflowState Source, string Action), WorkflowTransition> SourceTransitions = new()
    {
        [(WorkflowState.Draft, "default")] = new(
            LineOfDutyTrigger.StartLineOfDutyCase,
            WorkflowState.MemberInformationEntry,
            "Are you sure you want to start the LOD process?",
            "Start LOD", "Start",
            "Starting LOD case...",
            NotificationSeverity.Success, "LOD Started",
            "Case has been formally initiated into the LOD workflow."),

        [(WorkflowState.MemberInformationEntry, "default")] = new(
            LineOfDutyTrigger.ForwardToMedicalTechnician,
            WorkflowState.MedicalTechnicianReview,
            "Are you sure you want to forward this case to the Medical Technician?",
            "Confirm Forward", "Forward",
            "Forwarding to Medical Technician...",
            NotificationSeverity.Success, "Forwarded to Medical Technician",
            "Case has been forwarded to the Medical Technician for review."),

        [(WorkflowState.MedicalTechnicianReview, "default")] = new(
            LineOfDutyTrigger.ForwardToMedicalOfficerReview,
            WorkflowState.MedicalOfficerReview,
            "Are you sure you want to forward this case to the Medical Officer?",
            "Confirm Forward", "Forward",
            "Forwarding to Medical Officer...",
            NotificationSeverity.Success, "Forwarded to Medical Officer",
            "Case has been forwarded to the Medical Officer for review."),

        [(WorkflowState.MedicalOfficerReview, "default")] = new(
            LineOfDutyTrigger.ForwardToUnitCommanderReview,
            WorkflowState.UnitCommanderReview,
            "Are you sure you want to forward this case to the Unit Commander?",
            "Confirm Forward", "Forward",
            "Forwarding to Unit CC...",
            NotificationSeverity.Success, "Forwarded to Unit CC",
            "Case has been forwarded to the Unit Commander."),

        [(WorkflowState.UnitCommanderReview, "default")] = new(
            LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview,
            WorkflowState.WingJudgeAdvocateReview,
            "Are you sure you want to forward this case to the Wing Judge Advocate?",
            "Confirm Forward", "Forward",
            "Forwarding to Wing JA...",
            NotificationSeverity.Success, "Forwarded to Wing JA",
            "Case has been forwarded to the Wing Judge Advocate."),

        [(WorkflowState.WingJudgeAdvocateReview, "default")] = new(
            LineOfDutyTrigger.ForwardToWingCommanderReview,
            WorkflowState.WingCommanderReview,
            "Are you sure you want to forward this case to the Wing Commander?",
            "Confirm Forward", "Forward",
            "Forwarding to Wing CC...",
            NotificationSeverity.Success, "Forwarded to Wing CC",
            "Case has been forwarded to the Wing Commander."),

        [(WorkflowState.WingCommanderReview, "default")] = new(
            LineOfDutyTrigger.ForwardToAppointingAuthorityReview,
            WorkflowState.AppointingAuthorityReview,
            "Are you sure you want to forward this case to the Appointing Authority?",
            "Confirm Forward", "Forward",
            "Forwarding to Appointing Authority...",
            NotificationSeverity.Success, "Forwarded to Appointing Authority",
            "Case has been forwarded to the Appointing Authority for review."),

        [(WorkflowState.AppointingAuthorityReview, "default")] = new(
            LineOfDutyTrigger.ForwardToBoardTechnicianReview,
            WorkflowState.BoardMedicalTechnicianReview,
            "Are you sure you want to forward this case to the Board for review?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Review...",
            NotificationSeverity.Success, "Forwarded to Board Review",
            "Case has been forwarded to the Board for review."),

        [(WorkflowState.BoardMedicalTechnicianReview, "default")] = new(
            LineOfDutyTrigger.ForwardToBoardMedicalReview,
            WorkflowState.BoardMedicalOfficerReview,
            "Are you sure you want to forward this case to the Board Medical reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Medical...",
            NotificationSeverity.Success, "Forwarded to Board Medical",
            "Case has been forwarded to the Board Medical reviewer."),

        [(WorkflowState.BoardMedicalOfficerReview, "default")] = new(
            LineOfDutyTrigger.ForwardToBoardLegalReview,
            WorkflowState.BoardLegalReview,
            "Are you sure you want to forward this case to the Board Legal reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Legal...",
            NotificationSeverity.Success, "Forwarded to Board Legal",
            "Case has been forwarded to the Board Legal reviewer."),

        [(WorkflowState.BoardLegalReview, "default")] = new(
            LineOfDutyTrigger.ForwardToBoardAdministratorReview,
            WorkflowState.BoardAdministratorReview,
            "Are you sure you want to forward this case to the Board Admin reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Admin...",
            NotificationSeverity.Success, "Forwarded to Board Admin",
            "Case has been forwarded to the Board Admin reviewer."),

        [(WorkflowState.BoardAdministratorReview, "default")] = new(
            LineOfDutyTrigger.Complete,
            WorkflowState.Completed,
            "Are you sure you want to complete the Board review?",
            "Confirm Complete", "Complete",
            "Completing Board review...",
            NotificationSeverity.Success, "Review Completed",
            "The Board review has been completed."),

        [(WorkflowState.Completed, "default")] = new(
            LineOfDutyTrigger.Complete,
            WorkflowState.Completed,
            "Are you sure you want to complete the Board review?",
            "Confirm Complete", "Complete",
            "Completing Board review...",
            NotificationSeverity.Success, "Review Completed",
            "The Board review has been completed."),

        [(WorkflowState.MedicalOfficerReview, "return")] = new(
            LineOfDutyTrigger.Return,
            WorkflowState.MedicalTechnicianReview,
            "Are you sure you want to return this case to the Medical Technician?",
            "Confirm Return", "Return",
            "Returning to Med Tech...",
            NotificationSeverity.Info, "Returned to Med Tech",
            "Case has been returned to the Medical Technician for review."),

        [(WorkflowState.WingCommanderReview, "return")] = new(
            LineOfDutyTrigger.Return,
            WorkflowState.UnitCommanderReview,
            "Are you sure you want to return this case to the Unit Commander?",
            "Confirm Return", "Return",
            "Returning to Unit CC...",
            NotificationSeverity.Info, "Returned to Unit CC",
            "Case has been returned to the Unit Commander for review."),
    };

    /// <summary>
    /// Resolves the <see cref="WorkflowTransition"/> metadata for a given source state and action string.
    /// Checks source-specific transitions first, then shared transitions, then falls back to the
    /// source state's default transition.
    /// </summary>
    /// <returns>The matching transition, or <c>null</c> if no transition is configured.</returns>
    public static WorkflowTransition ResolveTransition(WorkflowState sourceState, string action)
    {
        if (SourceTransitions.TryGetValue((sourceState, action), out var transition))
        {
            return transition;
        }

        if (SharedTransitions.TryGetValue(action, out transition))
        {
            return transition;
        }

        SourceTransitions.TryGetValue((sourceState, "default"), out transition);
        return transition;
    }

    #endregion

    #region High-Level Operations

    /// <summary>
    /// Fires the specified trigger, persists the state change, re-fetches the case from
    /// the API, and returns a <see cref="StateMachineResult"/> with the updated case and
    /// computed tab index. On failure, resets the SM from persisted state.
    /// </summary>
    public async Task<StateMachineResult> TransitionAsync(LineOfDutyTrigger trigger, WorkflowState targetState, CancellationToken ct = default)
    {
        try
        {
            if (trigger == LineOfDutyTrigger.Return)
            {
                await _sm.FireAsync(_returnTrigger, targetState);
            }
            else if (_caseTriggers.TryGetValue(trigger, out var paramTrigger))
            {
                await _sm.FireAsync(paramTrigger, _lineOfDutyCase);
            }
            else
            {
                await _sm.FireAsync(trigger);
            }

            _lineOfDutyCase = await _dataService.GetCaseAsync(_lineOfDutyCase.CaseId, ct);

            var tabIndex = GetTabIndexForState(targetState);
            return StateMachineResult.Ok(_lineOfDutyCase, tabIndex);
        }
        catch (Exception ex)
        {
            await ResetFromPersistedStateAsync(ct);
            return StateMachineResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Persists the current case entity via the data service and returns the saved entity
    /// with its current tab index.
    /// </summary>
    public async Task<StateMachineResult> SaveCaseAsync(CancellationToken ct = default)
    {
        try
        {
            _lineOfDutyCase = await _dataService.SaveCaseAsync(_lineOfDutyCase, ct);
            var tabIndex = GetTabIndexForState(_lineOfDutyCase.WorkflowState);
            return StateMachineResult.Ok(_lineOfDutyCase, tabIndex);
        }
        catch (Exception ex)
        {
            return StateMachineResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Signs the timeline step at the given index using the data service, records a
    /// signed history entry, and returns the updated case.
    /// </summary>
    public async Task<StateMachineResult> SignTimelineStepAsync(int stepIndex, DateTime? stepStartDate, CancellationToken ct = default)
    {
        try
        {
            var timelineSteps = _lineOfDutyCase.TimelineSteps;

            if (stepIndex >= timelineSteps.Count)
            {
                return StateMachineResult.Fail("No timeline step found for the current workflow step.");
            }

            var timelineStep = timelineSteps.ElementAt(stepIndex);

            var signed = await _dataService.SignTimelineStepAsync(timelineStep.Id, ct);

            timelineStep.SignedDate = signed.SignedDate;
            timelineStep.SignedBy = signed.SignedBy;

            var historyEntry = await _dataService.AddHistoryEntryAsync(
                WorkflowStateHistoryFactory.CreateSigned(
                    _lineOfDutyCase.Id,
                    _lineOfDutyCase.WorkflowState,
                    stepStartDate,
                    signed.SignedDate,
                    signed.SignedBy),
                ct);

            _lineOfDutyCase.WorkflowStateHistories.Add(historyEntry);

            var tabIndex = GetTabIndexForState(_lineOfDutyCase.WorkflowState);
            return StateMachineResult.Ok(_lineOfDutyCase, tabIndex);
        }
        catch (Exception ex)
        {
            return StateMachineResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Re-syncs the state machine with the persisted case state after an error,
    /// preventing SM/DB state drift.
    /// </summary>
    private async Task ResetFromPersistedStateAsync(CancellationToken ct)
    {
        try
        {
            var persisted = await _dataService.GetCaseAsync(_lineOfDutyCase.CaseId, ct);
            if (persisted is not null)
            {
                _lineOfDutyCase = persisted;
            }
        }
        catch
        {
            // Best-effort recovery — if re-fetch fails, the SM is stale but
            // the UI will show an error and the user can refresh.
        }
    }

    #endregion

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

        _returnTrigger = _sm.SetTriggerParameters<WorkflowState>(LineOfDutyTrigger.Return);

        RegisterCaseTriggers();

        Configure();
    }

    public LodStateMachine(IDataService dataService)
    {
        _dataService = dataService;

        _sm = new StateMachine<WorkflowState, LineOfDutyTrigger>(WorkflowState.Draft, FiringMode.Queued);

        _returnTrigger = _sm.SetTriggerParameters<WorkflowState>(LineOfDutyTrigger.Return);

        RegisterCaseTriggers();

        Configure();
    }

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
    /// from the <see cref="LineOfDutyTrigger.StartLineOfDutyCase"/> trigger. Receives the newly saved
    /// <see cref="LineOfDutyCase"/> passed through the parameterized trigger and sets it as
    /// the active case managed by this state machine.
    /// </summary>
    /// <param name="lineOfDutyCase">The persisted LOD case being initiated into the workflow.</param>
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
    /// Executes any finalization logic when a case is cancelled from any active workflow
    /// step (e.g., recording the cancellation reason, notifying stakeholders).
    /// Once entered, no further transitions are permitted except silently ignoring
    /// <see cref="LineOfDutyTrigger.Cancel"/>.
    /// </summary>
    /// <returns>A completed task. Override with actual logic when entry side-effects are needed.</returns>
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
    /// Guard that determines whether the parameterized <see cref="LineOfDutyTrigger.Return"/>
    /// trigger may be fired from the current state. Used by all states that support
    /// returning the case to an earlier workflow step for correction or revision.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the case may be returned to the requested destination state;
    /// otherwise <c>false</c>.
    /// </returns>
    private bool CanReturnAsync()
    {
        return true;
    }

    #endregion

    /// <summary>
    /// Fires the specified line of duty workflow trigger asynchronously.
    /// </summary>
    /// <remarks>Use this method to advance the workflow state based on the provided trigger. The operation is
    /// performed asynchronously and may update the workflow state depending on the trigger's effect.</remarks>
    /// <param name="trigger">The trigger to fire in the line of duty workflow. Specifies the action or event to advance the workflow state.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task FireAsync(LineOfDutyTrigger trigger)
    {
        await _sm.FireAsync(trigger);
    }

    /// <summary>
    /// Fires the specified trigger for the given Line of Duty case asynchronously.
    /// </summary>
    /// <param name="newCase">The Line of Duty case to which the trigger will be applied.</param>
    /// <param name="trigger">The trigger to fire for the specified Line of Duty case.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task FireAsync(LineOfDutyCase newCase, LineOfDutyTrigger trigger)
    {
        if (!_caseTriggers.TryGetValue(trigger, out var paramTrigger))
        {
            throw new InvalidOperationException($"Trigger '{trigger}' is not configured as a parameterized case trigger.");
        }

        await _sm.FireAsync(paramTrigger, newCase);
    }

    /// <summary>
    /// Determines whether the specified trigger can be fired from the current state.
    /// </summary>
    public bool CanFire(LineOfDutyTrigger trigger)
    {
        return _sm.CanFire(trigger);
    }
}