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
/// <see cref="LineOfDutyExtensions.AddHistoryEntry"/>, and
/// <see cref="LineOfDutyExtensions.AddInitialHistory"/>.
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
        Assert.Equal(createdDate, entry.EnteredDate);
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
        Assert.Equal(explicitStart, entry.EnteredDate);
    }
}
