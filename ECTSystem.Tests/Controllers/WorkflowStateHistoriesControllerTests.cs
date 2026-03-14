using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="WorkflowStateHistoriesController"/>, the OData controller that
/// creates workflow state transition history records — both individually (<c>Post</c>) and
/// in atomic batches (<c>Batch</c>).
/// </summary>
/// <remarks>
/// <para>
/// Workflow state histories are append-only audit records that capture each transition in
/// the LOD determination workflow (state, action taken, and resulting status). The controller
/// does not support update or delete operations.
/// </para>
/// <para>
/// The in-memory database is configured to suppress <c>TransactionIgnoredWarning</c> because
/// the <c>Batch</c> endpoint uses explicit transactions that the in-memory provider does not
/// support. Tests verify both API return types and persistence side-effects.
/// </para>
/// </remarks>
public class WorkflowStateHistoriesControllerTests : ControllerTestBase
{
    /// <summary>Mocked context factory returning <see cref="EctDbContext"/> instances backed by the in-memory store.</summary>
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    /// <summary>In-memory database options shared across seed, act, and verify phases.</summary>
    private readonly DbContextOptions<EctDbContext>        _dbOptions;
    /// <summary>Mocked logging service injected into the controller.</summary>
    private readonly Mock<ILoggingService>                  _mockLog;
    /// <summary>System under test — the <see cref="WorkflowStateHistoriesController"/> instance.</summary>
    private readonly WorkflowStateHistoriesController       _sut;

    /// <summary>
    /// Initializes the in-memory database (with transaction-warning suppression),
    /// configures mocked dependencies, and creates the
    /// <see cref="WorkflowStateHistoriesController"/> with a fake authenticated user context.
    /// </summary>
    public WorkflowStateHistoriesControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _mockContextFactory = new Mock<IDbContextFactory<EctDbContext>>();

        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(_dbOptions));

        _mockContextFactory
            .Setup(f => f.CreateDbContext())
            .Returns(() => new EctDbContext(_dbOptions));

        _mockLog = new Mock<ILoggingService>();

        _sut = new WorkflowStateHistoriesController(_mockContextFactory.Object, _mockLog.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="WorkflowStateHistoriesController.Post"/> returns
    /// <see cref="CreatedODataResult{WorkflowStateHistory}"/> with the submitted entry
    /// when the model is valid.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelValid_ReturnsCreatedWithEntry()
    {
        var entry = BuildEntry();

        var result = await _sut.Post(entry, CancellationToken.None);

        var r = Assert.IsType<CreatedODataResult<WorkflowStateHistory>>(result);
        Assert.Equal(entry, r.Value);
    }

    /// <summary>
    /// Verifies that <c>Post</c> returns <see cref="BadRequestObjectResult"/> when the
    /// model state contains validation errors.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("WorkflowState", "Required");

        var result = await _sut.Post(new WorkflowStateHistory(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Verifies that a valid <c>Post</c> call persists the entry in the database with
    /// the correct <see cref="WorkflowStateHistory.LineOfDutyCaseId"/>.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelValid_InvokesServiceWithEntry()
    {
        var entry = BuildEntry();

        await _sut.Post(entry, CancellationToken.None);

        using var ctx = new EctDbContext(_dbOptions);
        var saved = await ctx.WorkflowStateHistories.FirstOrDefaultAsync();
        Assert.NotNull(saved);
        Assert.Equal(entry.LineOfDutyCaseId, saved.LineOfDutyCaseId);
    }

    /// <summary>
    /// Verifies that an invalid <c>Post</c> call does not persist any data, ensuring
    /// model validation gates the database write.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelInvalid_DoesNotInvokeService()
    {
        _sut.ModelState.AddModelError("key", "error");

        await _sut.Post(new WorkflowStateHistory(), CancellationToken.None);

        using var ctx = new EctDbContext(_dbOptions);
        var count = await ctx.WorkflowStateHistories.CountAsync();
        Assert.Equal(0, count);
    }

    // ─────────────────────────────── Batch ─────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="WorkflowStateHistoriesController.Batch"/> returns
    /// <see cref="OkObjectResult"/> containing all entries with server-assigned IDs
    /// when the input list is valid.
    /// </summary>
    [Fact]
    public async Task Batch_WhenValidEntries_ReturnsOkWithEntries()
    {
        var entries = new List<WorkflowStateHistory> { BuildEntry(), BuildEntry() };

        var result = await _sut.Batch(entries, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<List<WorkflowStateHistory>>(ok.Value);
        Assert.Equal(2, returned.Count);
        Assert.All(returned, e => Assert.True(e.Id > 0));
    }

    /// <summary>
    /// Verifies that a valid batch call persists all entries atomically — the total
    /// count in the database matches the number of entries submitted.
    /// </summary>
    [Fact]
    public async Task Batch_WhenValidEntries_PersistsAllAtomically()
    {
        var entries = new List<WorkflowStateHistory> { BuildEntry(), BuildEntry(), BuildEntry() };

        await _sut.Batch(entries, CancellationToken.None);

        using var ctx = new EctDbContext(_dbOptions);
        var count = await ctx.WorkflowStateHistories.CountAsync();
        Assert.Equal(3, count);
    }

    /// <summary>
    /// Verifies that <c>Batch</c> returns <see cref="BadRequestODataResult"/> when an
    /// empty list is submitted, rejecting no-op bulk operations.
    /// </summary>
    [Fact]
    public async Task Batch_WhenEmptyList_ReturnsBadRequest()
    {
        var result = await _sut.Batch(new List<WorkflowStateHistory>(), CancellationToken.None);

        Assert.IsType<BadRequestODataResult>(result);
    }

    /// <summary>
    /// Verifies that <c>Batch</c> returns <see cref="BadRequestObjectResult"/> when the
    /// model state is invalid.
    /// </summary>
    [Fact]
    public async Task Batch_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("key", "error");

        var result = await _sut.Batch(new List<WorkflowStateHistory> { BuildEntry() }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    /// <summary>
    /// Verifies that an invalid-model <c>Batch</c> call does not persist any records,
    /// ensuring validation gates the entire batch write.
    /// </summary>
    [Fact]
    public async Task Batch_WhenModelInvalid_DoesNotPersist()
    {
        _sut.ModelState.AddModelError("key", "error");

        await _sut.Batch(new List<WorkflowStateHistory> { BuildEntry() }, CancellationToken.None);

        using var ctx = new EctDbContext(_dbOptions);
        var count = await ctx.WorkflowStateHistories.CountAsync();
        Assert.Equal(0, count);
    }

    /// <summary>
    /// Verifies that <c>Batch</c> returns <see cref="BadRequestODataResult"/> when any
    /// entry in the list has an invalid <c>LineOfDutyCaseId</c> (e.g., zero), rejecting
    /// the entire batch.
    /// </summary>
    [Fact]
    public async Task Batch_WhenEntryHasInvalidCaseId_ReturnsBadRequest()
    {
        var entries = new List<WorkflowStateHistory>
        {
            BuildEntry(),
            new WorkflowStateHistory
            {
                LineOfDutyCaseId = 0,
                WorkflowState = WorkflowState.MemberInformationEntry,
                Action = TransitionAction.Enter,
                Status = WorkflowStepStatus.InProgress,
            }
        };

        var result = await _sut.Batch(entries, CancellationToken.None);

        Assert.IsType<BadRequestODataResult>(result);
    }

    /// <summary>
    /// Verifies that when a batch contains an entry with an invalid case ID, no records
    /// are persisted — even valid entries in the same batch are rolled back, preserving
    /// atomicity.
    /// </summary>
    [Fact]
    public async Task Batch_WhenEntryHasInvalidCaseId_DoesNotPersist()
    {
        var entries = new List<WorkflowStateHistory>
        {
            BuildEntry(),
            new WorkflowStateHistory
            {
                LineOfDutyCaseId = 0,
                WorkflowState = WorkflowState.MemberInformationEntry,
                Action = TransitionAction.Enter,
                Status = WorkflowStepStatus.InProgress,
            }
        };

        await _sut.Batch(entries, CancellationToken.None);

        using var ctx = new EctDbContext(_dbOptions);
        var count = await ctx.WorkflowStateHistories.CountAsync();
        Assert.Equal(0, count);
    }

    // ─────────────────────────────── Helpers ─────────────────────────────────

    /// <summary>
    /// Builds a <see cref="WorkflowStateHistory"/> test entity representing an initial
    /// entry into the <see cref="WorkflowState.MemberInformationEntry"/> state with
    /// <see cref="TransitionAction.Enter"/> and <see cref="WorkflowStepStatus.InProgress"/>.
    /// </summary>
    /// <returns>A populated <see cref="WorkflowStateHistory"/> instance with <c>LineOfDutyCaseId = 1</c>.</returns>
    private static WorkflowStateHistory BuildEntry() => new WorkflowStateHistory
    {
        LineOfDutyCaseId = 1,
        WorkflowState    = WorkflowState.MemberInformationEntry,
        Action           = TransitionAction.Enter,
        Status           = WorkflowStepStatus.InProgress,
    };
}
