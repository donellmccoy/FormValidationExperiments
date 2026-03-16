using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Extensions;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Extensions;

/// <summary>
/// Unit tests for the <see cref="LineOfDutyExtensions"/> extension methods that manage
/// workflow state history entries on <see cref="LineOfDutyCase"/>.
/// </summary>
/// <remarks>
/// The Shared extensions provide three methods for managing workflow history:
/// <see cref="LineOfDutyExtensions.AddHistoryEntry"/>,
/// <see cref="LineOfDutyExtensions.AddInitialHistory"/>, and
/// <see cref="LineOfDutyExtensions.AddSignedHistory"/>.
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
            CreatedDate = createdDate
        };

        lodCase.AddInitialHistory(WorkflowState.MemberInformationEntry);

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
            CreatedDate = createdDate
        };

        lodCase.AddInitialHistory(WorkflowState.Draft, explicitStart);

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
            Id = 10
        };

        lodCase.AddSignedHistory(WorkflowState.UnitCommanderReview, stepStart, signedDate, "Col Johnson");

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
            Id = 5
        };

        lodCase.AddSignedHistory(WorkflowState.WingJudgeAdvocateReview, null, null, "Maj Smith");

        var entry = lodCase.WorkflowStateHistories.First();
        Assert.Null(entry.StartDate);
        Assert.Null(entry.SignedDate);
        Assert.Equal("Maj Smith", entry.SignedBy);
    }

}
