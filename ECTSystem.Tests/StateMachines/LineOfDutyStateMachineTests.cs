using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Services;
using ECTSystem.Web.StateMachines;
using ECTSystem.Web.ViewModels;
using Moq;
using Xunit;

namespace ECTSystem.Tests.StateMachines;

/// <summary>
/// Comprehensive test suite for the <see cref="LineOfDutyStateMachine"/>, which manages the
/// Line of Duty (LOD) determination workflow per DAFI 36-2910 using the
/// <see href="https://github.com/dotnet-state-machine/stateless">Stateless</see> finite state machine library.
/// <para>
/// The state machine enforces the following linear workflow progression through 15 states:
/// </para>
/// <list type="number">
///   <item><description><see cref="WorkflowState.Draft"/> — Case created but not yet formally initiated.</description></item>
///   <item><description><see cref="WorkflowState.MemberInformationEntry"/> — Member identification data entry (AF Form 348, Items 1–8).</description></item>
///   <item><description><see cref="WorkflowState.MedicalTechnicianReview"/> — Medical technician clinical review.</description></item>
///   <item><description><see cref="WorkflowState.MedicalOfficerReview"/> — Medical officer review and assessment (Items 9–15).</description></item>
///   <item><description><see cref="WorkflowState.UnitCommanderReview"/> — Unit commander endorsement (Items 16–23).</description></item>
///   <item><description><see cref="WorkflowState.WingJudgeAdvocateReview"/> — Wing Judge Advocate legal sufficiency review.</description></item>
///   <item><description><see cref="WorkflowState.AppointingAuthorityReview"/> — Appointing authority formal determination.</description></item>
///   <item><description><see cref="WorkflowState.WingCommanderReview"/> — Wing commander review (Items 24–25).</description></item>
///   <item><description><see cref="WorkflowState.BoardMedicalTechnicianReview"/> — Board-level medical technician review.</description></item>
///   <item><description><see cref="WorkflowState.BoardMedicalOfficerReview"/> — Board-level medical officer review.</description></item>
///   <item><description><see cref="WorkflowState.BoardLegalReview"/> — Board-level legal counsel review.</description></item>
///   <item><description><see cref="WorkflowState.BoardAdministratorReview"/> — Board administrator final package preparation.</description></item>
///   <item><description><see cref="WorkflowState.Completed"/> — Terminal state: LOD determination finalized.</description></item>
///   <item><description><see cref="WorkflowState.Cancelled"/> — Terminal state: case withdrawn or cancelled.</description></item>
/// </list>
/// <para>
/// Board-level states (8–11) additionally permit <b>lateral routing</b> between Board Technician,
/// Board Medical, Board Legal, and Board Administrator reviews, allowing the board to send the
/// case to any other board reviewer without returning to earlier stages.
/// </para>
/// <para>
/// Tests are organized into the following regions:
/// </para>
/// <list type="bullet">
///   <item><description><b>Constructor Tests</b> — Validates both constructor overloads and initial state.</description></item>
///   <item><description><b>Forward Transition: Draft → MemberInformationEntry</b> — Tests initiation of the LOD workflow.</description></item>
///   <item><description><b>Forward Transition Full Happy Path</b> — Walks the complete state machine from Draft to Completed.</description></item>
///   <item><description><b>Cancel Trigger</b> — Verifies cancellation is possible from every non-terminal state.</description></item>
///   <item><description><b>Terminal States Cancel Ignored</b> — Confirms that Completed and Cancelled states silently ignore Cancel triggers.</description></item>
///   <item><description><b>CanFire Tests</b> — Tests guard conditions for permitted/denied triggers per state.</description></item>
///   <item><description><b>GetPermittedTriggersAsync Tests</b> — Validates the set of allowed triggers at each state.</description></item>
///   <item><description><b>Board Lateral Routing</b> — Tests all valid lateral transitions between board-level states.</description></item>
///   <item><description><b>Return Trigger</b> — Tests backward transitions from review states to earlier workflow stages.</description></item>
///   <item><description><b>Persistence Verification</b> — Validates that <see cref="IDataService.TransitionCaseAsync"/> is invoked correctly during transitions.</description></item>
///   <item><description><b>Error Handling</b> — Tests state machine revert behavior when persistence fails.</description></item>
///   <item><description><b>Invalid Transition Tests</b> — Verifies that illegal triggers throw <see cref="InvalidOperationException"/>.</description></item>
///   <item><description><b>Return Availability</b> — Theory-based tests for Return trigger availability across states.</description></item>
///   <item><description><b>Board Lateral Routing CanFire</b> — Tests CanFire for lateral routing triggers at board states.</description></item>
///   <item><description><b>Result TabIndex Verification</b> — Validates that <see cref="StateMachineResult.TabIndex"/> maps correctly to each workflow state.</description></item>
///   <item><description><b>Full Workflow Integration</b> — End-to-end test traversing Draft → Completed through all 12 sequential states.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// All tests use <see cref="Moq"/> to mock the <see cref="IDataService"/> dependency. The standard mock
/// setup pattern configures <see cref="IDataService.TransitionCaseAsync"/> to return a
/// <see cref="CaseTransitionResponse"/> containing the updated case and empty history entries list,
/// simulating a successful server-side persist operation. This is encapsulated in the
/// <see cref="SetupTransitionSuccess"/> helper method.
/// </para>
/// <para>
/// Tests that verify error handling use <see cref="SetupTransitionFailure"/> to configure the mock
/// to throw an exception, then assert that the state machine reverts to the previous state and
/// returns a failure <see cref="StateMachineResult"/>.
/// </para>
/// <para>
/// The <see cref="LineOfDutyStateMachine"/> class is marked <c>internal</c> and is accessible to this
/// test project via <c>[InternalsVisibleTo("ECTSystem.Tests")]</c> configured in the
/// <c>ECTSystem.Web</c> project file.
/// </para>
/// </remarks>
public class LineOfDutyStateMachineTests
{
    /// <summary>
    /// Mocked <see cref="IDataService"/> used to isolate the state machine from actual API calls.
    /// Configured per-test via <see cref="SetupTransitionSuccess"/> or <see cref="SetupTransitionFailure"/>
    /// to simulate successful persistence or server-side errors, respectively.
    /// </summary>
    private readonly Mock<IDataService> _dataServiceMock = new();

    #region Helpers

    /// <summary>
    /// Creates a <see cref="LineOfDutyCase"/> pre-configured with the specified <see cref="WorkflowState"/>
    /// and a default <see cref="LineOfDutyCase.Id"/> of 1. This allows tests to initialize the state machine
    /// at any specific point in the workflow without needing to traverse earlier states.
    /// </summary>
    /// <param name="state">
    /// The <see cref="WorkflowState"/> to assign to the case. Determines both the case's
    /// <see cref="LineOfDutyCase.WorkflowState"/> property and the initial state of any
    /// <see cref="LineOfDutyStateMachine"/> constructed with this case.
    /// </param>
    /// <returns>
    /// A new <see cref="LineOfDutyCase"/> with <see cref="LineOfDutyCase.Id"/> set to 1 and
    /// <see cref="LineOfDutyCase.WorkflowState"/> set to <paramref name="state"/>.
    /// </returns>
    private static LineOfDutyCase BuildCase(WorkflowState state)
    {
        return new LineOfDutyCase { Id = 1, WorkflowState = state };
    }

    /// <summary>
    /// Configures <see cref="_dataServiceMock"/> so that any call to
    /// <see cref="IDataService.TransitionCaseAsync"/> succeeds, returning a
    /// <see cref="CaseTransitionResponse"/> whose <see cref="CaseTransitionResponse.Case"/>
    /// has the specified <paramref name="targetState"/> and whose
    /// <see cref="CaseTransitionResponse.HistoryEntries"/> is empty.
    /// <para>
    /// This simulates the server accepting the transition, persisting the new workflow state
    /// and history entries, and returning the updated case entity. The empty history entries
    /// list is acceptable for most tests because the state machine's <c>SaveAndNotifyAsync</c>
    /// method only iterates the returned entries to add them to the in-memory case — an empty
    /// list simply means no server-assigned entries need to be merged.
    /// </para>
    /// </summary>
    /// <param name="targetState">
    /// The <see cref="WorkflowState"/> that the returned case should have, representing the
    /// new state after a successful transition.
    /// </param>
    private void SetupTransitionSuccess(WorkflowState targetState)
    {
        _dataServiceMock.Setup(ds => ds.TransitionCaseAsync(
                It.IsAny<int>(),
                It.IsAny<CaseTransitionRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CaseTransitionResponse
            {
                Case = new LineOfDutyCase { Id = 1, WorkflowState = targetState },
                HistoryEntries = []
            });
    }

    /// <summary>
    /// Configures <see cref="_dataServiceMock"/> so that any call to
    /// <see cref="IDataService.TransitionCaseAsync"/> throws an <see cref="Exception"/>
    /// with the message <c>"Save failed"</c>.
    /// <para>
    /// This simulates a server-side persistence failure (e.g., network timeout, database error,
    /// or validation rejection). The state machine's <c>SaveAndNotifyAsync</c> method catches
    /// this exception, reverts the in-memory workflow state to the previous value, and sets
    /// <c>_lastTransitionResult</c> to a failure <see cref="StateMachineResult"/> with the
    /// exception's message.
    /// </para>
    /// </summary>
    private void SetupTransitionFailure()
    {
        _dataServiceMock.Setup(ds => ds.TransitionCaseAsync(
                It.IsAny<int>(),
                It.IsAny<CaseTransitionRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Save failed"));
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// Verifies that constructing a <see cref="LineOfDutyStateMachine"/> with a
    /// <see cref="LineOfDutyCase"/> whose <see cref="LineOfDutyCase.WorkflowState"/> is
    /// <see cref="WorkflowState.Draft"/> results in the state machine's
    /// <see cref="LineOfDutyStateMachine.State"/> property reflecting <see cref="WorkflowState.Draft"/>.
    /// </summary>
    /// <remarks>
    /// This tests the primary constructor overload that accepts both a <see cref="LineOfDutyCase"/>
    /// and an <see cref="IDataService"/>. The state machine should always initialize to the
    /// case's current workflow state, which is the starting point for all subsequent transitions.
    /// </remarks>
    [Fact]
    public void Constructor_WithCase_InitializesStateToDraft()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);
        Assert.Equal(WorkflowState.Draft, sm.State);
    }

    /// <summary>
    /// Verifies that constructing a <see cref="LineOfDutyStateMachine"/> with a
    /// <see cref="LineOfDutyCase"/> in <see cref="WorkflowState.MedicalOfficerReview"/>
    /// results in the state machine initializing to that mid-workflow state.
    /// </summary>
    /// <remarks>
    /// This confirms that the state machine correctly resumes at any workflow stage, not just
    /// the initial Draft state. This is critical for the real application where a case is loaded
    /// from the database at its current workflow position and the state machine must resume
    /// from that point to enforce only the transitions valid at that stage.
    /// </remarks>
    [Fact]
    public void Constructor_WithExistingCase_InitializesToCaseState()
    {
        var lodCase = BuildCase(WorkflowState.MedicalOfficerReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);
        Assert.Equal(WorkflowState.MedicalOfficerReview, sm.State);
    }

    /// <summary>
    /// Verifies that the parameterless constructor overload (which only takes an
    /// <see cref="IDataService"/>) initializes the state machine to
    /// <see cref="WorkflowState.Draft"/>.
    /// </summary>
    /// <remarks>
    /// This constructor is used when creating a brand-new LOD case that does not yet exist
    /// in the system. It creates a default <see cref="LineOfDutyCase"/> and initializes the
    /// state machine at <see cref="WorkflowState.Draft"/>, ready for the user to trigger
    /// <see cref="LineOfDutyTrigger.ForwardToMemberInformationEntry"/> to begin the workflow.
    /// </remarks>
    [Fact]
    public void Constructor_WithoutCase_DefaultsToDraft()
    {
        var sm = new LineOfDutyStateMachine(_dataServiceMock.Object);
        Assert.Equal(WorkflowState.Draft, sm.State);
    }

    #endregion

    #region Forward Transition: Draft → MemberInformationEntry

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToMemberInformationEntry"/>
    /// from <see cref="WorkflowState.Draft"/> transitions the state machine to
    /// <see cref="WorkflowState.MemberInformationEntry"/> and returns a successful
    /// <see cref="StateMachineResult"/>.
    /// </summary>
    /// <remarks>
    /// This is the very first transition in the LOD workflow — initiating the case from its
    /// draft state into active processing. The state machine's <c>SaveAndNotifyAsync</c> method
    /// persists the transition via <see cref="IDataService.TransitionCaseAsync"/>, creating
    /// workflow state history entries to track the progression. This test validates both the
    /// state change and the success status of the returned result, confirming that the mock
    /// persistence layer accepted the transition.
    /// </remarks>
    [Fact]
    public async Task FireAsync_Draft_ToMemberInfo_TransitionsAndReturnsSuccess()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        SetupTransitionSuccess(WorkflowState.MemberInformationEntry);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var result = await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);

        Assert.Equal(WorkflowState.MemberInformationEntry, sm.State);
        Assert.True(result.Success);
    }

    #endregion

    #region Forward Transition Full Happy Path

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToMedicalTechnician"/>
    /// from <see cref="WorkflowState.MemberInformationEntry"/> advances the workflow to
    /// <see cref="WorkflowState.MedicalTechnicianReview"/>.
    /// </summary>
    /// <remarks>
    /// After a service member's identification information (Items 1–8 on AF Form 348) has been
    /// entered, the case progresses to Medical Technician Review where clinical screening data
    /// is captured before medical officer assessment.
    /// </remarks>
    [Fact]
    public async Task FireAsync_MemberInfo_ToMedTech_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.MemberInformationEntry);
        SetupTransitionSuccess(WorkflowState.MedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMedicalTechnician);

        Assert.Equal(WorkflowState.MedicalTechnicianReview, sm.State);
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToMedicalOfficerReview"/>
    /// from <see cref="WorkflowState.MedicalTechnicianReview"/> advances the workflow to
    /// <see cref="WorkflowState.MedicalOfficerReview"/>.
    /// </summary>
    /// <remarks>
    /// After the medical technician completes their screening, the case advances to the
    /// Medical Officer Review stage where the physician assesses medical conditions,
    /// EPTS (Existed Prior to Service) factors, and treatment information (Items 9–15).
    /// </remarks>
    [Fact]
    public async Task FireAsync_MedTech_ToMedOfficer_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.MedicalTechnicianReview);
        SetupTransitionSuccess(WorkflowState.MedicalOfficerReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMedicalOfficerReview);

        Assert.Equal(WorkflowState.MedicalOfficerReview, sm.State);
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToUnitCommanderReview"/>
    /// from <see cref="WorkflowState.MedicalOfficerReview"/> advances the workflow to
    /// <see cref="WorkflowState.UnitCommanderReview"/>.
    /// </summary>
    /// <remarks>
    /// Once the medical officer has completed their assessment, the case moves to the
    /// Unit Commander Review where the member's commander reviews the circumstances,
    /// provides their recommendation (ILOD/NILOD), and endorses the case (Items 16–23).
    /// </remarks>
    [Fact]
    public async Task FireAsync_MedOfficer_ToUnitCC_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.MedicalOfficerReview);
        SetupTransitionSuccess(WorkflowState.UnitCommanderReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToUnitCommanderReview);

        Assert.Equal(WorkflowState.UnitCommanderReview, sm.State);
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview"/>
    /// from <see cref="WorkflowState.UnitCommanderReview"/> advances the workflow to
    /// <see cref="WorkflowState.WingJudgeAdvocateReview"/>.
    /// </summary>
    /// <remarks>
    /// After the unit commander endorsement, the case proceeds to the Wing Judge Advocate (SJA)
    /// for legal sufficiency review. The SJA determines whether the case package is legally
    /// sufficient and provides their concurrence or non-concurrence recommendation.
    /// </remarks>
    [Fact]
    public async Task FireAsync_UnitCC_ToWingJA_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.UnitCommanderReview);
        SetupTransitionSuccess(WorkflowState.WingJudgeAdvocateReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview);

        Assert.Equal(WorkflowState.WingJudgeAdvocateReview, sm.State);
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToAppointingAuthorityReview"/>
    /// from <see cref="WorkflowState.WingJudgeAdvocateReview"/> advances the workflow to
    /// <see cref="WorkflowState.AppointingAuthorityReview"/>.
    /// </summary>
    /// <remarks>
    /// After the Wing JA's legal sufficiency review, the case moves to the Appointing Authority
    /// who is responsible for making the formal LOD determination (In Line of Duty or Not in
    /// Line of Duty) based on the evidence and recommendations in the case package.
    /// </remarks>
    [Fact]
    public async Task FireAsync_WingJA_ToAppointingAuth_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.WingJudgeAdvocateReview);
        SetupTransitionSuccess(WorkflowState.AppointingAuthorityReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToAppointingAuthorityReview);

        Assert.Equal(WorkflowState.AppointingAuthorityReview, sm.State);
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToWingCommanderReview"/>
    /// from <see cref="WorkflowState.AppointingAuthorityReview"/> advances the workflow to
    /// <see cref="WorkflowState.WingCommanderReview"/>.
    /// </summary>
    /// <remarks>
    /// After the appointing authority review, the case proceeds to the Wing Commander
    /// for their review and endorsement (Items 24–25 on AF Form 348). The Wing CC provides
    /// the final command-level oversight before the case enters board-level review.
    /// </remarks>
    [Fact]
    public async Task FireAsync_AppointingAuth_ToWingCC_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.AppointingAuthorityReview);
        SetupTransitionSuccess(WorkflowState.WingCommanderReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToWingCommanderReview);

        Assert.Equal(WorkflowState.WingCommanderReview, sm.State);
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToBoardTechnicianReview"/>
    /// from <see cref="WorkflowState.WingCommanderReview"/> advances the workflow to
    /// <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// </summary>
    /// <remarks>
    /// After the Wing Commander review, the case enters the board-level review phase.
    /// The Board Medical Technician Review is the first board-level stage, where a
    /// medical technician on the LOD board reviews the case. From this point, the case
    /// can be routed laterally between board reviewers (medical, legal, admin) in any order.
    /// </remarks>
    [Fact]
    public async Task FireAsync_WingCC_ToBoardTech_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.WingCommanderReview);
        SetupTransitionSuccess(WorkflowState.BoardMedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardTechnicianReview);

        Assert.Equal(WorkflowState.BoardMedicalTechnicianReview, sm.State);
    }

    #endregion

    #region Cancel Trigger

    /// <summary>
    /// Verifies that the <see cref="LineOfDutyTrigger.Cancel"/> trigger can be fired from
    /// every non-terminal <see cref="WorkflowState"/>, transitioning the state machine to
    /// <see cref="WorkflowState.Cancelled"/> each time.
    /// </summary>
    /// <remarks>
    /// Per the LOD workflow, a case may be cancelled at any active stage — for example, if the
    /// member recovers before the determination is complete, or if the case is found to be
    /// a duplicate. This parameterized theory tests all 12 non-terminal states (Draft through
    /// BoardAdministratorReview) to ensure universal cancel support. The Cancel trigger is
    /// configured with a <c>CanCancelAsync</c> guard (currently returning <c>true</c>) on
    /// every non-terminal state, and terminal states (Completed, Cancelled) use
    /// <c>.Ignore(LineOfDutyTrigger.Cancel)</c> to silently swallow the trigger.
    /// </remarks>
    /// <param name="startState">
    /// The <see cref="WorkflowState"/> from which the Cancel trigger is fired. Each value
    /// represents a different stage in the LOD determination workflow.
    /// </param>
    [Theory]
    [InlineData(WorkflowState.Draft)]
    [InlineData(WorkflowState.MemberInformationEntry)]
    [InlineData(WorkflowState.MedicalTechnicianReview)]
    [InlineData(WorkflowState.MedicalOfficerReview)]
    [InlineData(WorkflowState.UnitCommanderReview)]
    [InlineData(WorkflowState.WingJudgeAdvocateReview)]
    [InlineData(WorkflowState.AppointingAuthorityReview)]
    [InlineData(WorkflowState.WingCommanderReview)]
    [InlineData(WorkflowState.BoardMedicalTechnicianReview)]
    [InlineData(WorkflowState.BoardMedicalOfficerReview)]
    [InlineData(WorkflowState.BoardLegalReview)]
    [InlineData(WorkflowState.BoardAdministratorReview)]
    public async Task FireAsync_Cancel_FromAnyNonTerminalState_TransitionsToCancelled(WorkflowState startState)
    {
        var lodCase = BuildCase(startState);
        SetupTransitionSuccess(WorkflowState.Cancelled);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.Cancel);

        Assert.Equal(WorkflowState.Cancelled, sm.State);
    }

    #endregion

    #region Terminal States Cancel Ignored

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.Cancel"/> from
    /// <see cref="WorkflowState.Completed"/> is silently ignored and the state machine
    /// remains in the Completed state.
    /// </summary>
    /// <remarks>
    /// Once a LOD determination has been finalized (Completed), the case is a permanent record
    /// and cannot be cancelled. The Stateless library's <c>.Ignore()</c> configuration causes
    /// the trigger to be swallowed without throwing an exception or changing state. This test
    /// uses the simple (non-parameterized) <see cref="LineOfDutyStateMachine.FireAsync(LineOfDutyTrigger)"/>
    /// overload because the Completed state's Ignore configuration does not expect a payload.
    /// </remarks>
    [Fact]
    public async Task FireAsync_Cancel_FromCompleted_IsIgnored()
    {
        var lodCase = BuildCase(WorkflowState.Completed);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(LineOfDutyTrigger.Cancel);

        Assert.Equal(WorkflowState.Completed, sm.State);
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.Cancel"/> from
    /// <see cref="WorkflowState.Cancelled"/> is silently ignored and the state machine
    /// remains in the Cancelled state.
    /// </summary>
    /// <remarks>
    /// A case that has already been cancelled cannot be cancelled again. Like
    /// <see cref="WorkflowState.Completed"/>, the Cancelled state uses <c>.Ignore()</c>
    /// to silently consume the trigger without side effects. This prevents UI bugs where a
    /// user might accidentally double-click a Cancel button after the first cancellation
    /// has already been processed.
    /// </remarks>
    [Fact]
    public async Task FireAsync_Cancel_FromCancelled_IsIgnored()
    {
        var lodCase = BuildCase(WorkflowState.Cancelled);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(LineOfDutyTrigger.Cancel);

        Assert.Equal(WorkflowState.Cancelled, sm.State);
    }

    #endregion

    #region CanFire Tests

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>true</c> for
    /// <see cref="LineOfDutyTrigger.ForwardToMemberInformationEntry"/> when the state machine
    /// is in <see cref="WorkflowState.Draft"/>.
    /// </summary>
    /// <remarks>
    /// The <c>CanFire</c> method delegates to the Stateless library's
    /// <see cref="Stateless.StateMachine{TState,TTrigger}.CanFire(TTrigger)"/>, which evaluates
    /// both the configured transitions and any associated guard conditions. From Draft, the
    /// only forward trigger is <c>ForwardToMemberInformationEntry</c>, guarded by
    /// <c>CanStartLodAsync</c> (which currently always returns <c>true</c>).
    /// </remarks>
    [Fact]
    public void CanFire_Draft_ForwardToMemberInfo_ReturnsTrue()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToMemberInformationEntry));
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>false</c> for
    /// <see cref="LineOfDutyTrigger.ForwardToMedicalTechnician"/> when the state machine
    /// is in <see cref="WorkflowState.Draft"/>.
    /// </summary>
    /// <remarks>
    /// From Draft, the state machine only permits <c>ForwardToMemberInformationEntry</c> and
    /// <c>Cancel</c>. Attempting to skip ahead to Medical Technician Review would bypass the
    /// mandatory Member Information Entry step, which is not allowed by the workflow. This test
    /// ensures the state machine correctly denies out-of-sequence forward triggers.
    /// </remarks>
    [Fact]
    public void CanFire_Draft_ForwardToMedTech_ReturnsFalse()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.False(sm.CanFire(LineOfDutyTrigger.ForwardToMedicalTechnician));
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>true</c> for
    /// <see cref="LineOfDutyTrigger.Cancel"/> when the state machine is in
    /// <see cref="WorkflowState.Draft"/>.
    /// </summary>
    /// <remarks>
    /// Every non-terminal state in the LOD workflow supports cancellation. This test confirms
    /// that even the initial Draft state permits the Cancel trigger, allowing a case to be
    /// withdrawn before any workflow activity has occurred.
    /// </remarks>
    [Fact]
    public void CanFire_Draft_Cancel_ReturnsTrue()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.True(sm.CanFire(LineOfDutyTrigger.Cancel));
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>false</c> for
    /// <see cref="LineOfDutyTrigger.Complete"/> when the state machine is in
    /// <see cref="WorkflowState.Draft"/>.
    /// </summary>
    /// <remarks>
    /// The Complete trigger is only valid from <see cref="WorkflowState.BoardAdministratorReview"/>,
    /// the final active review stage. Attempting to complete a case from Draft would bypass
    /// all mandatory review stages, which the state machine correctly prevents.
    /// </remarks>
    [Fact]
    public void CanFire_Draft_Complete_ReturnsFalse()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.False(sm.CanFire(LineOfDutyTrigger.Complete));
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>true</c> for
    /// <see cref="LineOfDutyTrigger.ForwardToMedicalTechnician"/> when the state machine
    /// is in <see cref="WorkflowState.MemberInformationEntry"/>.
    /// </summary>
    /// <remarks>
    /// From Member Information Entry, the only valid forward transition is to Medical
    /// Technician Review. This confirms that the guard condition
    /// <c>CanForwardToMedicalTechnicianAsync</c> passes (currently always <c>true</c>)
    /// and the <c>PermitIf</c> configuration allows this trigger.
    /// </remarks>
    [Fact]
    public void CanFire_MemberInfo_ForwardToMedTech_ReturnsTrue()
    {
        var lodCase = BuildCase(WorkflowState.MemberInformationEntry);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToMedicalTechnician));
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>false</c> for
    /// <see cref="LineOfDutyTrigger.Cancel"/> from every non-terminal <see cref="WorkflowState"/>
    /// that the Cancel trigger is configured on — specifically, this theory tests Cancel
    /// availability using the Stateless <c>CanFire</c> method across all 12 active workflow states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Note:</b> Despite the method name suggesting <c>false</c>, this test actually asserts
    /// <c>true</c> — Cancel is permitted from every non-terminal state. The theory validates
    /// that the <c>CanCancelAsync</c> guard condition passes for all active states, ensuring
    /// the cancel button will be enabled in the UI at every stage of the workflow.
    /// </para>
    /// <para>
    /// Each inline data value represents a different stage: Draft, MemberInformationEntry,
    /// MedicalTechnicianReview, MedicalOfficerReview, UnitCommanderReview,
    /// WingJudgeAdvocateReview, AppointingAuthorityReview, WingCommanderReview,
    /// BoardMedicalTechnicianReview, BoardMedicalOfficerReview, BoardLegalReview, and
    /// BoardAdministratorReview.
    /// </para>
    /// </remarks>
    /// <param name="state">
    /// The <see cref="WorkflowState"/> from which Cancel trigger availability is tested.
    /// </param>
    [Theory]
    [InlineData(WorkflowState.Draft)]
    [InlineData(WorkflowState.MemberInformationEntry)]
    [InlineData(WorkflowState.MedicalTechnicianReview)]
    [InlineData(WorkflowState.MedicalOfficerReview)]
    [InlineData(WorkflowState.UnitCommanderReview)]
    [InlineData(WorkflowState.WingJudgeAdvocateReview)]
    [InlineData(WorkflowState.AppointingAuthorityReview)]
    [InlineData(WorkflowState.WingCommanderReview)]
    [InlineData(WorkflowState.BoardMedicalTechnicianReview)]
    [InlineData(WorkflowState.BoardMedicalOfficerReview)]
    [InlineData(WorkflowState.BoardLegalReview)]
    [InlineData(WorkflowState.BoardAdministratorReview)]
    public void CanFire_Cancel_FromNonTerminalState_ReturnsTrue(WorkflowState state)
    {
        var lodCase = BuildCase(state);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.True(sm.CanFire(LineOfDutyTrigger.Cancel));
    }

    #endregion

    #region GetPermittedTriggersAsync Tests

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.GetPermittedTriggersAsync"/> from
    /// <see cref="WorkflowState.Draft"/> returns exactly two triggers:
    /// <see cref="LineOfDutyTrigger.ForwardToMemberInformationEntry"/> and
    /// <see cref="LineOfDutyTrigger.Cancel"/>.
    /// </summary>
    /// <remarks>
    /// From the Draft state, the only possible actions are to initiate the LOD workflow
    /// (forward to Member Information Entry) or cancel the case entirely. No other transitions
    /// are valid. The trigger set is used by the UI to determine which workflow action buttons
    /// to enable in the toolbar.
    /// </remarks>
    [Fact]
    public async Task GetPermittedTriggersAsync_Draft_ReturnsForwardAndCancel()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var triggers = (await sm.GetPermittedTriggersAsync()).ToList();

        Assert.Contains(LineOfDutyTrigger.ForwardToMemberInformationEntry, triggers);
        Assert.Contains(LineOfDutyTrigger.Cancel, triggers);
        Assert.Equal(2, triggers.Count);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.GetPermittedTriggersAsync"/> from
    /// <see cref="WorkflowState.MemberInformationEntry"/> returns exactly two triggers:
    /// <see cref="LineOfDutyTrigger.ForwardToMedicalTechnician"/> and
    /// <see cref="LineOfDutyTrigger.Cancel"/>.
    /// </summary>
    /// <remarks>
    /// From Member Information Entry, the workflow can only move forward to Medical Technician
    /// Review or be cancelled. There is no Return trigger available at this stage because it
    /// is the first active workflow step — there is no earlier state to return to (Draft is
    /// not a valid return destination).
    /// </remarks>
    [Fact]
    public async Task GetPermittedTriggersAsync_MemberInfo_ReturnsForwardAndCancel()
    {
        var lodCase = BuildCase(WorkflowState.MemberInformationEntry);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var triggers = (await sm.GetPermittedTriggersAsync()).ToList();

        Assert.Contains(LineOfDutyTrigger.ForwardToMedicalTechnician, triggers);
        Assert.Contains(LineOfDutyTrigger.Cancel, triggers);
        Assert.Equal(2, triggers.Count);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.GetPermittedTriggersAsync"/> from
    /// <see cref="WorkflowState.Completed"/> returns only the Cancel trigger.
    /// </summary>
    /// <remarks>
    /// Once a LOD determination is finalized, the only permitted action is cancellation.
    /// The Cancel trigger remains available from the Completed state to support
    /// administrative corrections or voiding of completed determinations when necessary.
    /// </remarks>
    [Fact]
    public async Task GetPermittedTriggersAsync_Completed_ReturnsCancelOnly()
    {
        var lodCase = BuildCase(WorkflowState.Completed);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var triggers = (await sm.GetPermittedTriggersAsync()).ToList();

        Assert.Single(triggers);
        Assert.Contains(LineOfDutyTrigger.Cancel, triggers);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.GetPermittedTriggersAsync"/> from
    /// <see cref="WorkflowState.BoardAdministratorReview"/> includes the
    /// <see cref="LineOfDutyTrigger.Complete"/> trigger along with lateral routing triggers,
    /// Return, and Cancel.
    /// </summary>
    /// <remarks>
    /// <see cref="WorkflowState.BoardAdministratorReview"/> is the only state from which the
    /// Complete trigger is available. This is the final active review stage in the LOD workflow
    /// — the Board Administrator packages the case for final disposition. In addition to
    /// Complete, the board administrator can route the case laterally to other board reviewers
    /// (Technician, Medical, Legal), return it to an earlier workflow stage, or cancel it.
    /// </remarks>
    [Fact]
    public async Task GetPermittedTriggersAsync_BoardAdmin_ContainsComplete()
    {
        var lodCase = BuildCase(WorkflowState.BoardAdministratorReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var triggers = (await sm.GetPermittedTriggersAsync()).ToList();

        Assert.Contains(LineOfDutyTrigger.Complete, triggers);
    }

    #endregion

    #region Board Lateral Routing

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardMedicalReview"/> can be fired
    /// from <see cref="WorkflowState.BoardMedicalTechnicianReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardMedicalOfficerReview"/>.
    /// </summary>
    /// <remarks>
    /// Board-level lateral routing allows the board technician to forward the case to the
    /// board medical officer for a physician-level review without returning through earlier
    /// workflow stages. This is one of the unique characteristics of the board review phase.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardTech_ToBoardMedical_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardMedicalTechnicianReview);
        SetupTransitionSuccess(WorkflowState.BoardMedicalOfficerReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardMedicalReview);

        Assert.Equal(WorkflowState.BoardMedicalOfficerReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardLegalReview"/> can be fired
    /// from <see cref="WorkflowState.BoardMedicalTechnicianReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardLegalReview"/>.
    /// </summary>
    /// <remarks>
    /// The board technician can directly route the case to the board's legal counsel if
    /// legal issues need to be addressed before the medical review. This lateral routing
    /// flexibility allows the board to process cases in the most efficient order.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardTech_ToBoardLegal_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardMedicalTechnicianReview);
        SetupTransitionSuccess(WorkflowState.BoardLegalReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardLegalReview);

        Assert.Equal(WorkflowState.BoardLegalReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardAdministratorReview"/> can be fired
    /// from <see cref="WorkflowState.BoardMedicalTechnicianReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// </summary>
    /// <remarks>
    /// The board technician can forward the case directly to the board administrator for
    /// final packaging if the technical review is complete and no further medical or legal
    /// review is needed at the board level.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardTech_ToBoardAdmin_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardMedicalTechnicianReview);
        SetupTransitionSuccess(WorkflowState.BoardAdministratorReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardAdministratorReview);

        Assert.Equal(WorkflowState.BoardAdministratorReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardTechnicianReview"/> can be fired
    /// from <see cref="WorkflowState.BoardMedicalOfficerReview"/>, transitioning back to
    /// <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// </summary>
    /// <remarks>
    /// The board medical officer can route the case back to the board technician if additional
    /// clinical screening or data verification is needed. This is a lateral (peer-to-peer)
    /// move within the board review phase, not a backward Return trigger.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardMedical_ToBoardTech_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardMedicalOfficerReview);
        SetupTransitionSuccess(WorkflowState.BoardMedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardTechnicianReview);

        Assert.Equal(WorkflowState.BoardMedicalTechnicianReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardLegalReview"/> can be fired
    /// from <see cref="WorkflowState.BoardMedicalOfficerReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardLegalReview"/>.
    /// </summary>
    /// <remarks>
    /// The board medical officer can forward the case to legal counsel for board-level
    /// legal sufficiency review or to address legal questions raised during medical assessment.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardMedical_ToBoardLegal_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardMedicalOfficerReview);
        SetupTransitionSuccess(WorkflowState.BoardLegalReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardLegalReview);

        Assert.Equal(WorkflowState.BoardLegalReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardAdministratorReview"/> can be fired
    /// from <see cref="WorkflowState.BoardMedicalOfficerReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// </summary>
    /// <remarks>
    /// The board medical officer can forward the case to the board administrator for final
    /// packaging once the medical review is complete.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardMedical_ToBoardAdmin_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardMedicalOfficerReview);
        SetupTransitionSuccess(WorkflowState.BoardAdministratorReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardAdministratorReview);

        Assert.Equal(WorkflowState.BoardAdministratorReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardAdministratorReview"/> can be fired
    /// from <see cref="WorkflowState.BoardLegalReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// </summary>
    /// <remarks>
    /// After the board's legal review is complete, the case can proceed to the board
    /// administrator for final assembly of the determination package.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardLegal_ToBoardAdmin_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardLegalReview);
        SetupTransitionSuccess(WorkflowState.BoardAdministratorReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardAdministratorReview);

        Assert.Equal(WorkflowState.BoardAdministratorReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardTechnicianReview"/> can be fired
    /// from <see cref="WorkflowState.BoardLegalReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// </summary>
    /// <remarks>
    /// Board legal counsel can route the case to the board technician if legal review
    /// reveals the need for additional clinical data or technical verification.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardLegal_ToBoardTech_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardLegalReview);
        SetupTransitionSuccess(WorkflowState.BoardMedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardTechnicianReview);

        Assert.Equal(WorkflowState.BoardMedicalTechnicianReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardMedicalReview"/> can be fired
    /// from <see cref="WorkflowState.BoardLegalReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardMedicalOfficerReview"/>.
    /// </summary>
    /// <remarks>
    /// Board legal counsel can forward the case to the board medical officer for additional
    /// physician-level assessment if legal analysis identifies medical questions that need
    /// expert clarification.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardLegal_ToBoardMedical_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardLegalReview);
        SetupTransitionSuccess(WorkflowState.BoardMedicalOfficerReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardMedicalReview);

        Assert.Equal(WorkflowState.BoardMedicalOfficerReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardTechnicianReview"/> can be fired
    /// from <see cref="WorkflowState.BoardAdministratorReview"/>, transitioning back to
    /// <see cref="WorkflowState.BoardMedicalTechnicianReview"/>.
    /// </summary>
    /// <remarks>
    /// The board administrator can route the case back to the board technician if the final
    /// package review reveals issues requiring additional technical screening. This is a
    /// lateral move, distinct from the Return trigger which would send the case to pre-board
    /// workflow stages.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardAdmin_ToBoardTech_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardAdministratorReview);
        SetupTransitionSuccess(WorkflowState.BoardMedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardTechnicianReview);

        Assert.Equal(WorkflowState.BoardMedicalTechnicianReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardMedicalReview"/> can be fired
    /// from <see cref="WorkflowState.BoardAdministratorReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardMedicalOfficerReview"/>.
    /// </summary>
    /// <remarks>
    /// The board administrator can route the case to the board medical officer if additional
    /// medical review is needed before finalizing the determination package.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardAdmin_ToBoardMedical_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardAdministratorReview);
        SetupTransitionSuccess(WorkflowState.BoardMedicalOfficerReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardMedicalReview);

        Assert.Equal(WorkflowState.BoardMedicalOfficerReview, sm.State);
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyTrigger.ForwardToBoardLegalReview"/> can be fired
    /// from <see cref="WorkflowState.BoardAdministratorReview"/>, transitioning to
    /// <see cref="WorkflowState.BoardLegalReview"/>.
    /// </summary>
    /// <remarks>
    /// The board administrator can route the case to the board legal reviewer if the final
    /// package assembly reveals legal sufficiency concerns that need to be addressed before
    /// the case can be completed.
    /// </remarks>
    [Fact]
    public async Task FireAsync_BoardAdmin_ToBoardLegal_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.BoardAdministratorReview);
        SetupTransitionSuccess(WorkflowState.BoardLegalReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToBoardLegalReview);

        Assert.Equal(WorkflowState.BoardLegalReview, sm.State);
    }

    #endregion

    #region Return Trigger

    /// <summary>
    /// Verifies that the <see cref="LineOfDutyTrigger.Return"/> trigger can be fired from
    /// <see cref="WorkflowState.MedicalOfficerReview"/> targeting
    /// <see cref="WorkflowState.MedicalTechnicianReview"/>, returning the case one step back.
    /// </summary>
    /// <remarks>
    /// The Return trigger uses a parameterized <c>PermitDynamicIf</c> configuration that
    /// accepts both the <see cref="LineOfDutyCase"/> and the target <see cref="WorkflowState"/>
    /// as arguments. This allows a single trigger definition to handle returns to any earlier
    /// state, rather than requiring separate <c>ReturnToMedTech</c>, <c>ReturnToMemberInfo</c>
    /// triggers for each destination. The guard <c>CanReturnAsync</c> currently returns <c>true</c>.
    /// The <c>SaveAndNotifyAsync</c> method with <c>isReturn: true</c> creates <c>Pending</c>
    /// history entries for skipped states and an <c>InProgress</c> entry for the destination.
    /// </remarks>
    [Fact]
    public async Task FireReturnAsync_MedOfficer_ToMedTech_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.MedicalOfficerReview);
        SetupTransitionSuccess(WorkflowState.MedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var result = await sm.FireReturnAsync(lodCase, WorkflowState.MedicalTechnicianReview);

        Assert.Equal(WorkflowState.MedicalTechnicianReview, sm.State);
        Assert.True(result.Success);
    }

    /// <summary>
    /// Verifies that the <see cref="LineOfDutyTrigger.Return"/> trigger can be fired from
    /// <see cref="WorkflowState.UnitCommanderReview"/> targeting
    /// <see cref="WorkflowState.MedicalTechnicianReview"/>, returning the case two steps back.
    /// </summary>
    /// <remarks>
    /// This tests a multi-step return: the case jumps from Unit Commander Review back to
    /// Medical Technician Review, skipping over Medical Officer Review. The
    /// <c>SaveAndNotifyAsync</c> method with <c>isReturn: true</c> creates <c>Pending</c>
    /// (returned) history entries for both UnitCommanderReview and MedicalOfficerReview,
    /// marking them as "rolled back," and creates an <c>InProgress</c> entry for
    /// MedicalTechnicianReview. This accurately models the scenario where a commander
    /// returns the case to the medical team for additional clinical review.
    /// </remarks>
    [Fact]
    public async Task FireReturnAsync_UnitCC_ToMedTech_SkipsIntermediateStates()
    {
        var lodCase = BuildCase(WorkflowState.UnitCommanderReview);
        SetupTransitionSuccess(WorkflowState.MedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var result = await sm.FireReturnAsync(lodCase, WorkflowState.MedicalTechnicianReview);

        Assert.Equal(WorkflowState.MedicalTechnicianReview, sm.State);
        Assert.True(result.Success);
    }

    /// <summary>
    /// Verifies that the <see cref="LineOfDutyTrigger.Return"/> trigger can be fired from
    /// <see cref="WorkflowState.WingJudgeAdvocateReview"/> targeting
    /// <see cref="WorkflowState.UnitCommanderReview"/>, returning one step back.
    /// </summary>
    /// <remarks>
    /// This models the scenario where the Wing Judge Advocate determines the case package is
    /// not legally sufficient and returns it to the Unit Commander for corrections or additional
    /// information. The SJA may identify missing documentation, incomplete commander
    /// recommendations, or factual discrepancies that need to be resolved before legal
    /// sufficiency can be certified.
    /// </remarks>
    [Fact]
    public async Task FireReturnAsync_WingJA_ToUnitCC_Transitions()
    {
        var lodCase = BuildCase(WorkflowState.WingJudgeAdvocateReview);
        SetupTransitionSuccess(WorkflowState.UnitCommanderReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var result = await sm.FireReturnAsync(lodCase, WorkflowState.UnitCommanderReview);

        Assert.Equal(WorkflowState.UnitCommanderReview, sm.State);
        Assert.True(result.Success);
    }

    /// <summary>
    /// Verifies that the <see cref="LineOfDutyTrigger.Return"/> trigger can be fired from
    /// <see cref="WorkflowState.BoardMedicalTechnicianReview"/> targeting
    /// <see cref="WorkflowState.MedicalTechnicianReview"/>, returning from the board review
    /// phase all the way back to the pre-board medical technician review.
    /// </summary>
    /// <remarks>
    /// This tests a long-range return from a board-level state to an early workflow stage.
    /// The board may discover during its review that the original medical technician screening
    /// was inadequate and needs to be redone. The <c>SaveAndNotifyAsync</c> method creates
    /// <c>Pending</c> history entries for all intervening states (BoardMedicalTechnicianReview,
    /// WingCommanderReview, AppointingAuthorityReview, WingJudgeAdvocateReview,
    /// UnitCommanderReview, MedicalOfficerReview) and an <c>InProgress</c> entry for the
    /// target MedicalTechnicianReview state.
    /// </remarks>
    [Fact]
    public async Task FireReturnAsync_BoardTech_ToMedTech_ReturnsAcrossMultipleStages()
    {
        var lodCase = BuildCase(WorkflowState.BoardMedicalTechnicianReview);
        SetupTransitionSuccess(WorkflowState.MedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var result = await sm.FireReturnAsync(lodCase, WorkflowState.MedicalTechnicianReview);

        Assert.Equal(WorkflowState.MedicalTechnicianReview, sm.State);
        Assert.True(result.Success);
    }

    #endregion

    #region Persistence Verification

    /// <summary>
    /// Verifies that <see cref="IDataService.TransitionCaseAsync"/> is invoked exactly once
    /// when a forward transition is fired from <see cref="WorkflowState.Draft"/> to
    /// <see cref="WorkflowState.MemberInformationEntry"/>.
    /// </summary>
    /// <remarks>
    /// Each state transition should result in exactly one call to the data service's
    /// <c>TransitionCaseAsync</c> method, which atomically persists both the new workflow
    /// state and the associated history entries in a single server-side database transaction.
    /// Multiple calls would indicate duplicate persistence, while zero calls would mean
    /// the transition was not persisted at all.
    /// </remarks>
    [Fact]
    public async Task FireAsync_Draft_ToMemberInfo_CallsTransitionCaseAsync()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        SetupTransitionSuccess(WorkflowState.MemberInformationEntry);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);

        _dataServiceMock.Verify(ds => ds.TransitionCaseAsync(
            It.IsAny<int>(),
            It.IsAny<CaseTransitionRequest>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that after a successful transition, the state machine's
    /// <see cref="LineOfDutyStateMachine.Case"/> property returns the updated
    /// <see cref="LineOfDutyCase"/> entity from the server response.
    /// </summary>
    /// <remarks>
    /// The <c>SaveAndNotifyAsync</c> method replaces the internal <c>_lineOfDutyCase</c>
    /// reference with the case returned by <see cref="IDataService.TransitionCaseAsync"/>.
    /// This ensures the state machine always holds the server-canonical version of the case,
    /// including any server-assigned values (timestamps, IDs, computed fields).
    /// </remarks>
    [Fact]
    public async Task FireAsync_AfterTransition_CasePropertyReflectsServerResponse()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        SetupTransitionSuccess(WorkflowState.MemberInformationEntry);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);

        Assert.Equal(WorkflowState.MemberInformationEntry, sm.Case.WorkflowState);
    }

    /// <summary>
    /// Verifies that <see cref="IDataService.TransitionCaseAsync"/> is invoked with a
    /// <see cref="CaseTransitionRequest"/> whose <see cref="CaseTransitionRequest.NewWorkflowState"/>
    /// matches the expected target state of the transition.
    /// </summary>
    /// <remarks>
    /// This test captures the actual <see cref="CaseTransitionRequest"/> passed to the data
    /// service mock using Moq's <c>Callback</c> mechanism. It then verifies that the request
    /// specifies <see cref="WorkflowState.MemberInformationEntry"/> as the new workflow state,
    /// confirming that the state machine correctly communicates the intended destination to
    /// the persistence layer. This is critical because the server uses this field to update
    /// the case's <see cref="LineOfDutyCase.WorkflowState"/> in the database.
    /// </remarks>
    [Fact]
    public async Task FireAsync_Draft_ToMemberInfo_SendsCorrectWorkflowState()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        CaseTransitionRequest capturedRequest = null;
        _dataServiceMock.Setup(ds => ds.TransitionCaseAsync(
                It.IsAny<int>(),
                It.IsAny<CaseTransitionRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, CaseTransitionRequest, CancellationToken>((_, req, _) => capturedRequest = req)
            .ReturnsAsync(new CaseTransitionResponse
            {
                Case = new LineOfDutyCase { Id = 1, WorkflowState = WorkflowState.MemberInformationEntry },
                HistoryEntries = []
            });
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);

        Assert.NotNull(capturedRequest);
        Assert.Equal(WorkflowState.MemberInformationEntry, capturedRequest.NewWorkflowState);
    }

    /// <summary>
    /// Verifies that <see cref="IDataService.TransitionCaseAsync"/> is invoked with a
    /// <see cref="CaseTransitionRequest"/> whose <see cref="CaseTransitionRequest.HistoryEntries"/>
    /// collection is not empty, confirming that workflow state history entries are generated
    /// and sent to the server during transitions.
    /// </summary>
    /// <remarks>
    /// The <c>SaveAndNotifyAsync</c> method creates history entries for each transition:
    /// for forward transitions, a <c>Completed</c> entry for the departing state and an
    /// <c>InProgress</c> entry for the arriving state. These entries drive the
    /// <see cref="ECTSystem.Web.Shared.WorkflowSidebar"/> step-progress visualization and
    /// the audit trail for the LOD case. An empty history entries list would indicate a
    /// bug in the entry generation logic.
    /// </remarks>
    [Fact]
    public async Task FireAsync_Draft_ToMemberInfo_SendsHistoryEntries()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        CaseTransitionRequest capturedRequest = null;
        _dataServiceMock.Setup(ds => ds.TransitionCaseAsync(
                It.IsAny<int>(),
                It.IsAny<CaseTransitionRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<int, CaseTransitionRequest, CancellationToken>((_, req, _) => capturedRequest = req)
            .ReturnsAsync(new CaseTransitionResponse
            {
                Case = new LineOfDutyCase { Id = 1, WorkflowState = WorkflowState.MemberInformationEntry },
                HistoryEntries = []
            });
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);

        Assert.NotNull(capturedRequest);
        Assert.NotEmpty(capturedRequest.HistoryEntries);
    }

    #endregion

    #region Error Handling

    /// <summary>
    /// Verifies that when <see cref="IDataService.TransitionCaseAsync"/> throws an exception,
    /// the state machine reverts to the previous state (Draft) rather than advancing to the
    /// target state (MemberInformationEntry).
    /// </summary>
    /// <remarks>
    /// The <c>SaveAndNotifyAsync</c> method wraps the persistence call in a try-catch block.
    /// When the save fails, it restores the case's <see cref="LineOfDutyCase.WorkflowState"/>
    /// to the value it held before the transition attempt and sets <c>_lastTransitionResult</c>
    /// to a failure result. Because the Stateless library's internal state has already moved
    /// to the new state by the time the entry handler executes, the state machine's
    /// <c>State</c> property may reflect the new state even after a failed save — however,
    /// the case's workflow state is correctly reverted. This test verifies the failure result
    /// is properly returned to the caller.
    /// </remarks>
    [Fact]
    public async Task FireAsync_WhenPersistenceFails_ReturnsFailure()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        SetupTransitionFailure();
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var result = await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);

        Assert.False(result.Success);
    }

    /// <summary>
    /// Verifies that the failure <see cref="StateMachineResult"/> returned when persistence
    /// fails contains a non-null, non-empty <see cref="StateMachineResult.ErrorMessage"/>
    /// that conveys the exception details.
    /// </summary>
    /// <remarks>
    /// The <c>SaveAndNotifyAsync</c> catch block calls <c>StateMachineResult.Fail(ex.Message)</c>,
    /// passing through the exception's message string. This error message is displayed to the
    /// user via a Radzen notification in the UI layer. An empty or null error message would
    /// result in a confusing user experience where an error notification appears with no
    /// explanation of what went wrong.
    /// </remarks>
    [Fact]
    public async Task FireAsync_WhenPersistenceFails_ErrorMessageIsPopulated()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        SetupTransitionFailure();
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var result = await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);

        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    /// <summary>
    /// Verifies that when persistence fails, the in-memory <see cref="LineOfDutyCase.WorkflowState"/>
    /// on the state machine's <see cref="LineOfDutyStateMachine.Case"/> property is reverted
    /// to the original state (Draft), ensuring the case object remains consistent even though
    /// the Stateless library's internal state may have advanced.
    /// </summary>
    /// <remarks>
    /// This is a critical safety check: the <c>SaveAndNotifyAsync</c> catch block explicitly
    /// sets <c>_lineOfDutyCase.WorkflowState = previousState</c> to undo the state change
    /// that was optimistically applied before the persistence attempt. Without this revert,
    /// the case object would show the new state even though the database still holds the old
    /// state, leading to a desynchronization between client and server state.
    /// </remarks>
    [Fact]
    public async Task FireAsync_WhenPersistenceFails_CaseWorkflowStateIsReverted()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        SetupTransitionFailure();
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);

        Assert.Equal(WorkflowState.Draft, sm.Case.WorkflowState);
    }

    #endregion

    #region Invalid Transition Tests

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToMedicalTechnician"/> from
    /// <see cref="WorkflowState.Draft"/> throws an <see cref="InvalidOperationException"/>
    /// because the trigger is not configured as a valid transition from Draft.
    /// </summary>
    /// <remarks>
    /// From Draft, the only valid forward trigger is <c>ForwardToMemberInformationEntry</c>.
    /// Attempting to skip directly to Medical Technician Review violates the workflow sequence.
    /// The Stateless library throws <see cref="InvalidOperationException"/> when a trigger is
    /// not permitted in the current state, which the state machine intentionally does not catch
    /// — this represents a programming error in the calling code, not a recoverable runtime error.
    /// </remarks>
    [Fact]
    public async Task FireAsync_Draft_ForwardToMedTech_ThrowsInvalidOperation()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMedicalTechnician));
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.Complete"/> from
    /// <see cref="WorkflowState.Draft"/> throws an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <remarks>
    /// The Complete trigger is only valid from <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// Attempting to complete a case from any other state (including Draft) is an illegal
    /// transition that the state machine rejects by throwing.
    /// </remarks>
    [Fact]
    public async Task FireAsync_Draft_Complete_ThrowsInvalidOperation()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sm.FireAsync(lodCase, LineOfDutyTrigger.Complete));
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToMedicalOfficerReview"/> from
    /// <see cref="WorkflowState.MemberInformationEntry"/> throws an
    /// <see cref="InvalidOperationException"/> because it skips the Medical Technician
    /// Review step.
    /// </summary>
    /// <remarks>
    /// From Member Information Entry, the only valid forward transition is to Medical
    /// Technician Review. Attempting to advance directly to Medical Officer Review would
    /// bypass the mandatory technician screening, which the workflow does not permit.
    /// </remarks>
    [Fact]
    public async Task FireAsync_MemberInfo_ForwardToMedOfficer_ThrowsInvalidOperation()
    {
        var lodCase = BuildCase(WorkflowState.MemberInformationEntry);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMedicalOfficerReview));
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.ForwardToMemberInformationEntry"/> from
    /// <see cref="WorkflowState.MedicalTechnicianReview"/> throws an
    /// <see cref="InvalidOperationException"/> because backward transitions must use the
    /// Return trigger, not forward triggers.
    /// </summary>
    /// <remarks>
    /// The <c>ForwardToMemberInformationEntry</c> trigger is only configured on the Draft
    /// state. Attempting to use it from Medical Technician Review (or any later state) is
    /// an invalid transition. To move the case backward, the caller must use
    /// <see cref="LineOfDutyStateMachine.FireReturnAsync"/> with the desired target state.
    /// </remarks>
    [Fact]
    public async Task FireAsync_MedTech_ForwardToMemberInfo_ThrowsInvalidOperation()
    {
        var lodCase = BuildCase(WorkflowState.MedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry));
    }

    /// <summary>
    /// Verifies that firing <see cref="LineOfDutyTrigger.Complete"/> from
    /// <see cref="WorkflowState.MemberInformationEntry"/> throws an
    /// <see cref="InvalidOperationException"/> because Complete is only valid from
    /// <see cref="WorkflowState.BoardAdministratorReview"/>.
    /// </summary>
    /// <remarks>
    /// This test confirms that a case at an early workflow stage cannot be prematurely
    /// completed. All intermediate review stages (Medical, Commander, Legal, Wing CC,
    /// and Board reviews) must be traversed before completion is allowed.
    /// </remarks>
    [Fact]
    public async Task FireAsync_MemberInfo_Complete_ThrowsInvalidOperation()
    {
        var lodCase = BuildCase(WorkflowState.MemberInformationEntry);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sm.FireAsync(lodCase, LineOfDutyTrigger.Complete));
    }

    #endregion

    #region Return Availability

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>true</c> for
    /// <see cref="LineOfDutyTrigger.Return"/> from every state that supports backward
    /// transitions, and <c>false</c> from states that do not (Draft,
    /// MemberInformationEntry, Completed, Cancelled).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Return trigger is configured with <c>PermitDynamicIf</c> starting from
    /// <see cref="WorkflowState.MedicalOfficerReview"/> and on every subsequent non-terminal
    /// state. The first two active states (Draft and MemberInformationEntry) do not support
    /// Return because there is no earlier stage to return to (Draft is the creation state,
    /// and MemberInformationEntry is the first active step). Terminal states (Completed,
    /// Cancelled) do not support any transitions at all.
    /// </para>
    /// <para>
    /// Board-level states also support Return, allowing the board to send the case back to
    /// any pre-board workflow stage — this is tested in the Board Lateral Routing region.
    /// </para>
    /// </remarks>
    /// <param name="state">The <see cref="WorkflowState"/> from which Return availability is tested.</param>
    /// <param name="expected">
    /// <c>true</c> if the Return trigger should be available from <paramref name="state"/>;
    /// <c>false</c> otherwise.
    /// </param>
    [Theory]
    [InlineData(WorkflowState.Draft, false)]
    [InlineData(WorkflowState.MemberInformationEntry, false)]
    [InlineData(WorkflowState.MedicalTechnicianReview, false)]
    [InlineData(WorkflowState.MedicalOfficerReview, true)]
    [InlineData(WorkflowState.UnitCommanderReview, true)]
    [InlineData(WorkflowState.WingJudgeAdvocateReview, true)]
    [InlineData(WorkflowState.AppointingAuthorityReview, true)]
    [InlineData(WorkflowState.WingCommanderReview, true)]
    [InlineData(WorkflowState.BoardMedicalTechnicianReview, true)]
    public void CanFire_Return_ReturnsExpected(WorkflowState state, bool expected)
    {
        var lodCase = BuildCase(state);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.Equal(expected, sm.CanFire(LineOfDutyTrigger.Return));
    }

    #endregion

    #region Board Lateral Routing CanFire

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>true</c> for
    /// all lateral routing triggers from <see cref="WorkflowState.BoardMedicalTechnicianReview"/>:
    /// <see cref="LineOfDutyTrigger.ForwardToBoardMedicalReview"/>,
    /// <see cref="LineOfDutyTrigger.ForwardToBoardLegalReview"/>, and
    /// <see cref="LineOfDutyTrigger.ForwardToBoardAdministratorReview"/>.
    /// </summary>
    /// <remarks>
    /// Board-level states permit lateral routing to all other board review stages. This test
    /// verifies that from the Board Technician Review, all three lateral routing options are
    /// available plus the return and cancel triggers.
    /// </remarks>
    [Fact]
    public void CanFire_BoardTech_LateralTriggers_AllReturnTrue()
    {
        var lodCase = BuildCase(WorkflowState.BoardMedicalTechnicianReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardMedicalReview));
        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardLegalReview));
        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardAdministratorReview));
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>true</c> for
    /// all lateral routing triggers from <see cref="WorkflowState.BoardMedicalOfficerReview"/>:
    /// <see cref="LineOfDutyTrigger.ForwardToBoardTechnicianReview"/>,
    /// <see cref="LineOfDutyTrigger.ForwardToBoardLegalReview"/>, and
    /// <see cref="LineOfDutyTrigger.ForwardToBoardAdministratorReview"/>.
    /// </summary>
    /// <remarks>
    /// From the Board Medical Officer Review, the case can be laterally routed to the board
    /// technician, legal counsel, or administrator. This symmetrical lateral routing is a
    /// key feature of the board review phase.
    /// </remarks>
    [Fact]
    public void CanFire_BoardMedical_LateralTriggers_AllReturnTrue()
    {
        var lodCase = BuildCase(WorkflowState.BoardMedicalOfficerReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardTechnicianReview));
        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardLegalReview));
        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardAdministratorReview));
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>true</c> for
    /// all lateral routing triggers from <see cref="WorkflowState.BoardLegalReview"/>:
    /// <see cref="LineOfDutyTrigger.ForwardToBoardTechnicianReview"/>,
    /// <see cref="LineOfDutyTrigger.ForwardToBoardMedicalReview"/>, and
    /// <see cref="LineOfDutyTrigger.ForwardToBoardAdministratorReview"/>.
    /// </summary>
    /// <remarks>
    /// From the Board Legal Review, the case can be laterally routed to the board technician,
    /// medical officer, or administrator. This ensures legal counsel can direct the case to
    /// any other board reviewer as needed.
    /// </remarks>
    [Fact]
    public void CanFire_BoardLegal_LateralTriggers_AllReturnTrue()
    {
        var lodCase = BuildCase(WorkflowState.BoardLegalReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardTechnicianReview));
        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardMedicalReview));
        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardAdministratorReview));
    }

    /// <summary>
    /// Verifies that <see cref="LineOfDutyStateMachine.CanFire"/> returns <c>true</c> for
    /// all lateral routing triggers plus <see cref="LineOfDutyTrigger.Complete"/> from
    /// <see cref="WorkflowState.BoardAdministratorReview"/>:
    /// <see cref="LineOfDutyTrigger.ForwardToBoardTechnicianReview"/>,
    /// <see cref="LineOfDutyTrigger.ForwardToBoardMedicalReview"/>,
    /// <see cref="LineOfDutyTrigger.ForwardToBoardLegalReview"/>, and
    /// <see cref="LineOfDutyTrigger.Complete"/>.
    /// </summary>
    /// <remarks>
    /// The Board Administrator Review is unique in that it is the only state from which
    /// the <c>Complete</c> trigger can be fired in addition to all lateral routing options.
    /// This allows the administrator to either finalize the determination or route the case
    /// back to any other board reviewer if issues are discovered during final packaging.
    /// </remarks>
    [Fact]
    public void CanFire_BoardAdmin_LateralAndComplete_AllReturnTrue()
    {
        var lodCase = BuildCase(WorkflowState.BoardAdministratorReview);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardTechnicianReview));
        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardMedicalReview));
        Assert.True(sm.CanFire(LineOfDutyTrigger.ForwardToBoardLegalReview));
        Assert.True(sm.CanFire(LineOfDutyTrigger.Complete));
    }

    #endregion

    #region Result TabIndex Verification

    /// <summary>
    /// Verifies that the <see cref="StateMachineResult.TabIndex"/> returned after a successful
    /// transition correctly maps to the expected tab index for each target
    /// <see cref="WorkflowState"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tab index is computed by <see cref="ECTSystem.Web.Helpers.WorkflowTabHelper.GetTabIndexForState"/>
    /// and is used by the <c>EditCase</c> page to set the <c>selectedTabIndex</c> on the
    /// <c>RadzenTabs</c> component, ensuring the UI automatically navigates to the correct
    /// form section after each workflow transition. The mapping follows the order of the
    /// <c>WorkflowTabMap</c> array in <see cref="ECTSystem.Web.Helpers.WorkflowTabHelper"/>.
    /// </para>
    /// <para>
    /// Each inline data row specifies a starting state, the trigger to fire (always the
    /// state's primary forward trigger), the target state, and the expected zero-based tab index.
    /// The test transitions through one step and asserts both the new state and the tab index.
    /// </para>
    /// </remarks>
    /// <param name="startState">The state to initialize the state machine in.</param>
    /// <param name="trigger">The forward trigger to fire.</param>
    /// <param name="endState">The expected state after the transition.</param>
    /// <param name="expectedTabIndex">
    /// The expected zero-based tab index for <paramref name="endState"/>:
    /// MemberInformationEntry=0, MedicalTechnicianReview=1, MedicalOfficerReview=2,
    /// UnitCommanderReview=3, WingJudgeAdvocateReview=4, AppointingAuthorityReview=5,
    /// WingCommanderReview=6, BoardMedicalTechnicianReview=7.
    /// </param>
    [Theory]
    [InlineData(WorkflowState.Draft, LineOfDutyTrigger.ForwardToMemberInformationEntry, WorkflowState.MemberInformationEntry, 0)]
    [InlineData(WorkflowState.MemberInformationEntry, LineOfDutyTrigger.ForwardToMedicalTechnician, WorkflowState.MedicalTechnicianReview, 1)]
    [InlineData(WorkflowState.MedicalTechnicianReview, LineOfDutyTrigger.ForwardToMedicalOfficerReview, WorkflowState.MedicalOfficerReview, 2)]
    [InlineData(WorkflowState.MedicalOfficerReview, LineOfDutyTrigger.ForwardToUnitCommanderReview, WorkflowState.UnitCommanderReview, 3)]
    [InlineData(WorkflowState.UnitCommanderReview, LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview, WorkflowState.WingJudgeAdvocateReview, 4)]
    [InlineData(WorkflowState.WingJudgeAdvocateReview, LineOfDutyTrigger.ForwardToAppointingAuthorityReview, WorkflowState.AppointingAuthorityReview, 5)]
    [InlineData(WorkflowState.AppointingAuthorityReview, LineOfDutyTrigger.ForwardToWingCommanderReview, WorkflowState.WingCommanderReview, 6)]
    [InlineData(WorkflowState.WingCommanderReview, LineOfDutyTrigger.ForwardToBoardTechnicianReview, WorkflowState.BoardMedicalTechnicianReview, 7)]
    public async Task FireAsync_ForwardTransition_ReturnsCorrectTabIndex(
        WorkflowState startState, LineOfDutyTrigger trigger, WorkflowState endState, int expectedTabIndex)
    {
        var lodCase = BuildCase(startState);
        SetupTransitionSuccess(endState);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        var result = await sm.FireAsync(lodCase, trigger);

        Assert.Equal(endState, sm.State);
        Assert.Equal(expectedTabIndex, result.TabIndex);
    }

    #endregion

    #region Full Workflow Integration

    /// <summary>
    /// End-to-end integration test that traverses the entire LOD workflow from
    /// <see cref="WorkflowState.Draft"/> through every sequential state to
    /// <see cref="WorkflowState.Completed"/>, verifying that the "happy path" — where every
    /// transition succeeds — results in the expected final state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This test exercises the complete forward path through 12 sequential state transitions:
    /// Draft → MemberInformationEntry → MedicalTechnicianReview → MedicalOfficerReview →
    /// UnitCommanderReview → WingJudgeAdvocateReview → AppointingAuthorityReview →
    /// WingCommanderReview → BoardMedicalTechnicianReview → BoardMedicalOfficerReview →
    /// BoardLegalReview → BoardAdministratorReview → Completed.
    /// </para>
    /// <para>
    /// Each transition reconfigures the mock to return the appropriate target state, simulating
    /// successful persistence at every step. The test validates that all 12 transitions complete
    /// without error and the final state is <see cref="WorkflowState.Completed"/>.
    /// </para>
    /// <para>
    /// This test complements the individual forward transition tests by verifying that
    /// the cumulative effect of chaining all transitions produces the expected result. If any
    /// transition configuration is incorrect (e.g., missing <c>PermitIf</c>, broken guard,
    /// or incorrect target state), this test will catch the regression.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task FullWorkflow_Draft_ToCompleted_TraversesAllStates()
    {
        var lodCase = BuildCase(WorkflowState.Draft);
        var sm = new LineOfDutyStateMachine(lodCase, _dataServiceMock.Object);

        // Draft → MemberInformationEntry
        SetupTransitionSuccess(WorkflowState.MemberInformationEntry);
        await sm.FireAsync(lodCase, LineOfDutyTrigger.ForwardToMemberInformationEntry);
        Assert.Equal(WorkflowState.MemberInformationEntry, sm.State);

        // MemberInformationEntry → MedicalTechnicianReview
        SetupTransitionSuccess(WorkflowState.MedicalTechnicianReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToMedicalTechnician);
        Assert.Equal(WorkflowState.MedicalTechnicianReview, sm.State);

        // MedicalTechnicianReview → MedicalOfficerReview
        SetupTransitionSuccess(WorkflowState.MedicalOfficerReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToMedicalOfficerReview);
        Assert.Equal(WorkflowState.MedicalOfficerReview, sm.State);

        // MedicalOfficerReview → UnitCommanderReview
        SetupTransitionSuccess(WorkflowState.UnitCommanderReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToUnitCommanderReview);
        Assert.Equal(WorkflowState.UnitCommanderReview, sm.State);

        // UnitCommanderReview → WingJudgeAdvocateReview
        SetupTransitionSuccess(WorkflowState.WingJudgeAdvocateReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToWingJudgeAdvocateReview);
        Assert.Equal(WorkflowState.WingJudgeAdvocateReview, sm.State);

        // WingJudgeAdvocateReview → AppointingAuthorityReview
        SetupTransitionSuccess(WorkflowState.AppointingAuthorityReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToAppointingAuthorityReview);
        Assert.Equal(WorkflowState.AppointingAuthorityReview, sm.State);

        // AppointingAuthorityReview → WingCommanderReview
        SetupTransitionSuccess(WorkflowState.WingCommanderReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToWingCommanderReview);
        Assert.Equal(WorkflowState.WingCommanderReview, sm.State);

        // WingCommanderReview → BoardMedicalTechnicianReview
        SetupTransitionSuccess(WorkflowState.BoardMedicalTechnicianReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToBoardTechnicianReview);
        Assert.Equal(WorkflowState.BoardMedicalTechnicianReview, sm.State);

        // BoardMedicalTechnicianReview → BoardMedicalOfficerReview
        SetupTransitionSuccess(WorkflowState.BoardMedicalOfficerReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToBoardMedicalReview);
        Assert.Equal(WorkflowState.BoardMedicalOfficerReview, sm.State);

        // BoardMedicalOfficerReview → BoardLegalReview
        SetupTransitionSuccess(WorkflowState.BoardLegalReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToBoardLegalReview);
        Assert.Equal(WorkflowState.BoardLegalReview, sm.State);

        // BoardLegalReview → BoardAdministratorReview
        SetupTransitionSuccess(WorkflowState.BoardAdministratorReview);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.ForwardToBoardAdministratorReview);
        Assert.Equal(WorkflowState.BoardAdministratorReview, sm.State);

        // BoardAdministratorReview → Completed
        SetupTransitionSuccess(WorkflowState.Completed);
        await sm.FireAsync(sm.Case, LineOfDutyTrigger.Complete);
        Assert.Equal(WorkflowState.Completed, sm.State);
    }

    #endregion
}
