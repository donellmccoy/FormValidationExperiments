using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Models;
using ECTSystem.Web.Extensions;
using Xunit;

namespace ECTSystem.Tests.Extensions;

/// <summary>
/// Unit tests for the <see cref="Shared.Extensions.LineOfDutyExtensions"/> (Shared) and
/// <see cref="Web.Extensions.LineOfDutyExtensions"/> (Web) extension methods that manage
/// workflow state history entries and state transitions on <see cref="LineOfDutyCase"/>.
/// </summary>
/// <remarks>
/// <para>
/// The Shared extensions provide three methods for managing workflow history:
/// <see cref="Shared.Extensions.LineOfDutyExtensions.AddHistoryEntry"/>,
/// <see cref="Shared.Extensions.LineOfDutyExtensions.AddInitialHistory"/>, and
/// <see cref="Shared.Extensions.LineOfDutyExtensions.AddSignedHistory"/>.
/// </para>
/// <para>
/// The Web extension provides <see cref="Web.Extensions.LineOfDutyExtensions.UpdateWorkflowState"/>
/// for conditionally updating the case workflow state with timestamp tracking.
/// </para>
/// </remarks>
public class LineOfDutyExtensionsTests
{
    // ── AddHistoryEntry Tests ───────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="Shared.Extensions.LineOfDutyExtensions.AddHistoryEntry"/>
    /// initializes the <see cref="LineOfDutyCase.WorkflowStateHistories"/> collection when
    /// it is <c>null</c> and adds the provided entry.
    /// </summary>
    [Fact]
    public void AddHistoryEntry_NullCollection_InitializesAndAddsEntry()
    {
        var lodCase = new LineOfDutyCase { Id = 1, WorkflowStateHistories = null };
        var entry = new WorkflowStateHistory
        {
            LineOfDutyCaseId = 1,
            WorkflowState = WorkflowState.Draft,
            Action = TransitionAction.Enter,
            Status = WorkflowStepStatus.InProgress
        };

        lodCase.AddHistoryEntry(entry);

        Assert.NotNull(lodCase.WorkflowStateHistories);
        Assert.Single(lodCase.WorkflowStateHistories);
        Assert.Contains(entry, lodCase.WorkflowStateHistories);
    }

    /// <summary>
    /// Verifies that <see cref="Shared.Extensions.LineOfDutyExtensions.AddHistoryEntry"/>
    /// appends to an existing non-null collection without removing prior entries.
    /// </summary>
    [Fact]
    public void AddHistoryEntry_ExistingCollection_AppendsWithoutOverwriting()
    {
        var existingEntry = new WorkflowStateHistory
        {
            LineOfDutyCaseId = 1,
            WorkflowState = WorkflowState.Draft,
            Action = TransitionAction.Enter,
            Status = WorkflowStepStatus.Completed
        };
        var lodCase = new LineOfDutyCase
        {
            Id = 1,
            WorkflowStateHistories = new HashSet<WorkflowStateHistory> { existingEntry }
        };
        var newEntry = new WorkflowStateHistory
        {
            LineOfDutyCaseId = 1,
            WorkflowState = WorkflowState.MemberInformationEntry,
            Action = TransitionAction.Enter,
            Status = WorkflowStepStatus.InProgress
        };

        lodCase.AddHistoryEntry(newEntry);

        Assert.Equal(2, lodCase.WorkflowStateHistories.Count);
        Assert.Contains(existingEntry, lodCase.WorkflowStateHistories);
        Assert.Contains(newEntry, lodCase.WorkflowStateHistories);
    }

    // ── AddInitialHistory Tests ─────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="Shared.Extensions.LineOfDutyExtensions.AddInitialHistory"/>
    /// uses the case's <see cref="AuditableEntity.CreatedDate"/> as the start date when
    /// no explicit start date is provided.
    /// </summary>
    [Fact]
    public void AddInitialHistory_NoStartDate_UsesCreatedDate()
    {
        var createdDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var lodCase = new LineOfDutyCase
        {
            Id = 42,
            WorkflowState = WorkflowState.MemberInformationEntry,
            CreatedDate = createdDate
        };

        lodCase.AddInitialHistory();

        Assert.Single(lodCase.WorkflowStateHistories);
        var entry = lodCase.WorkflowStateHistories.First();
        Assert.Equal(42, entry.LineOfDutyCaseId);
        Assert.Equal(WorkflowState.MemberInformationEntry, entry.WorkflowState);
        Assert.Equal(TransitionAction.Enter, entry.Action);
        Assert.Equal(WorkflowStepStatus.InProgress, entry.Status);
        Assert.Equal(createdDate, entry.StartDate);
    }

    /// <summary>
    /// Verifies that <see cref="Shared.Extensions.LineOfDutyExtensions.AddInitialHistory"/>
    /// uses the explicitly provided start date instead of the case's <see cref="AuditableEntity.CreatedDate"/>.
    /// </summary>
    [Fact]
    public void AddInitialHistory_ExplicitStartDate_UsesProvidedDate()
    {
        var createdDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var explicitStart = new DateTime(2024, 2, 1, 8, 0, 0, DateTimeKind.Utc);
        var lodCase = new LineOfDutyCase
        {
            Id = 42,
            WorkflowState = WorkflowState.Draft,
            CreatedDate = createdDate
        };

        lodCase.AddInitialHistory(explicitStart);

        var entry = lodCase.WorkflowStateHistories.First();
        Assert.Equal(explicitStart, entry.StartDate);
    }

    // ── AddSignedHistory Tests ──────────────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="Shared.Extensions.LineOfDutyExtensions.AddSignedHistory"/>
    /// creates a history entry with the correct signed date, signed-by, and step start date values.
    /// </summary>
    [Fact]
    public void AddSignedHistory_CreatesEntryWithCorrectSignatureData()
    {
        var stepStart = new DateTime(2024, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        var signedDate = new DateTime(2024, 3, 5, 14, 30, 0, DateTimeKind.Utc);
        var lodCase = new LineOfDutyCase
        {
            Id = 10,
            WorkflowState = WorkflowState.UnitCommanderReview
        };

        lodCase.AddSignedHistory(stepStart, signedDate, "Col Johnson");

        Assert.Single(lodCase.WorkflowStateHistories);
        var entry = lodCase.WorkflowStateHistories.First();
        Assert.Equal(10, entry.LineOfDutyCaseId);
        Assert.Equal(WorkflowState.UnitCommanderReview, entry.WorkflowState);
        Assert.Equal(WorkflowStepStatus.InProgress, entry.Status);
        Assert.Equal(stepStart, entry.StartDate);
        Assert.Equal(signedDate, entry.SignedDate);
        Assert.Equal("Col Johnson", entry.SignedBy);
    }

    /// <summary>
    /// Verifies that <see cref="Shared.Extensions.LineOfDutyExtensions.AddSignedHistory"/>
    /// correctly handles <c>null</c> dates for step start and signed date parameters.
    /// </summary>
    [Fact]
    public void AddSignedHistory_NullDates_SetsNullOnEntry()
    {
        var lodCase = new LineOfDutyCase
        {
            Id = 5,
            WorkflowState = WorkflowState.WingJudgeAdvocateReview
        };

        lodCase.AddSignedHistory(null, null, "Maj Smith");

        var entry = lodCase.WorkflowStateHistories.First();
        Assert.Null(entry.StartDate);
        Assert.Null(entry.SignedDate);
        Assert.Equal("Maj Smith", entry.SignedBy);
    }

    // ── UpdateWorkflowState Tests (Web Extension) ───────────────────────────

    /// <summary>
    /// Verifies that <see cref="Web.Extensions.LineOfDutyExtensions.UpdateWorkflowState"/>
    /// updates the workflow state and sets <see cref="AuditableEntity.ModifiedDate"/> when
    /// the new state differs from the current state.
    /// </summary>
    [Fact]
    public void UpdateWorkflowState_DifferentState_UpdatesStateAndModifiedDate()
    {
        var lodCase = new LineOfDutyCase
        {
            WorkflowState = WorkflowState.Draft,
            ModifiedDate = DateTime.MinValue
        };
        var beforeUpdate = DateTime.UtcNow;

        lodCase.UpdateWorkflowState(WorkflowState.MemberInformationEntry);

        Assert.Equal(WorkflowState.MemberInformationEntry, lodCase.WorkflowState);
        Assert.True(lodCase.ModifiedDate >= beforeUpdate, "ModifiedDate should be set to approximately now");
    }

    /// <summary>
    /// Verifies that <see cref="Web.Extensions.LineOfDutyExtensions.UpdateWorkflowState"/>
    /// does not update <see cref="AuditableEntity.ModifiedDate"/> when the new state is the
    /// same as the current state.
    /// </summary>
    [Fact]
    public void UpdateWorkflowState_SameState_DoesNotUpdateModifiedDate()
    {
        var originalDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var lodCase = new LineOfDutyCase
        {
            WorkflowState = WorkflowState.UnitCommanderReview,
            ModifiedDate = originalDate
        };

        lodCase.UpdateWorkflowState(WorkflowState.UnitCommanderReview);

        Assert.Equal(WorkflowState.UnitCommanderReview, lodCase.WorkflowState);
        Assert.Equal(originalDate, lodCase.ModifiedDate);
    }
}
