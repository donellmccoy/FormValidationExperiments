using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="WorkflowStateHistoryController"/>, the OData controller that
/// creates workflow state transition history records individually via <c>Post</c>.
/// </summary>
/// <remarks>
/// <para>
/// Workflow state histories are append-only audit records that capture each transition in
/// the LOD determination workflow (state, action taken, and resulting status). The controller
/// does not support update or delete operations.
/// </para>
/// <para>
/// The in-memory database is used to verify both API return types and persistence side-effects.
/// </para>
/// </remarks>
public class WorkflowStateHistoryControllerTests : ControllerTestBase
{
    /// <summary>Mocked context factory returning <see cref="EctDbContext"/> instances backed by the in-memory store.</summary>
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    /// <summary>In-memory database options shared across seed, act, and verify phases.</summary>
    private readonly DbContextOptions<EctDbContext>        _dbOptions;
    /// <summary>Mocked logging service injected into the controller.</summary>
    private readonly Mock<ILoggingService>                  _mockLog;
    /// <summary>System under test — the <see cref="WorkflowStateHistoryController"/> instance.</summary>
    private readonly WorkflowStateHistoryController       _sut;

    /// <summary>
    /// Initializes the in-memory database (with transaction-warning suppression),
    /// configures mocked dependencies, and creates the
    /// <see cref="WorkflowStateHistoryController"/> with a fake authenticated user context.
    /// </summary>
    public WorkflowStateHistoryControllerTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EctDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _mockContextFactory = new Mock<IDbContextFactory<EctDbContext>>();

        _mockContextFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new EctDbContext(_dbOptions));

        _mockContextFactory
            .Setup(f => f.CreateDbContext())
            .Returns(() => new EctDbContext(_dbOptions));

        _mockLog = new Mock<ILoggingService>();

        _sut = new WorkflowStateHistoryController(_mockContextFactory.Object, _mockLog.Object, TimeProvider.System);
        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    /// <summary>
    /// Verifies that <see cref="WorkflowStateHistoryController.Post"/> returns
    /// <see cref="CreatedODataResult{WorkflowStateHistory}"/> with the submitted entry
    /// when the model is valid.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelValid_ReturnsCreatedWithEntry()
    {
        var dto = BuildEntryDto();

        var result = await _sut.Post(dto, TestContext.Current.CancellationToken);

        var r = Assert.IsType<CreatedODataResult<WorkflowStateHistory>>(result);
        Assert.Equal(dto.LineOfDutyCaseId, r.Entity.LineOfDutyCaseId);
        Assert.Equal(dto.WorkflowState, r.Entity.WorkflowState);
    }

    /// <summary>
    /// Verifies that <c>Post</c> returns <see cref="BadRequestObjectResult"/> when the
    /// model state contains validation errors.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("WorkflowState", "Required");

        var result = await _sut.Post(new CreateWorkflowStateHistoryDto(), TestContext.Current.CancellationToken);

        var obj = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ValidationProblemDetails>(obj.Value);
        Assert.NotEmpty(problem.Errors);
    }

    /// <summary>
    /// Verifies that a valid <c>Post</c> call persists the entry in the database with
    /// the correct <see cref="WorkflowStateHistory.LineOfDutyCaseId"/>.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelValid_InvokesServiceWithEntry()
    {
        var dto = BuildEntryDto();

        await _sut.Post(dto, TestContext.Current.CancellationToken);

        using var ctx = new EctDbContext(_dbOptions);
        var saved = await ctx.WorkflowStateHistories.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(saved);
        Assert.Equal(dto.LineOfDutyCaseId, saved.LineOfDutyCaseId);
    }

    /// <summary>
    /// Verifies that an invalid <c>Post</c> call does not persist any data, ensuring
    /// model validation gates the database write.
    /// </summary>
    [Fact]
    public async Task Post_WhenModelInvalid_DoesNotInvokeService()
    {
        _sut.ModelState.AddModelError("key", "error");

        await _sut.Post(new CreateWorkflowStateHistoryDto(), TestContext.Current.CancellationToken);

        using var ctx = new EctDbContext(_dbOptions);
        var count = await ctx.WorkflowStateHistories.CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    /// <summary>
    /// §2.7 (N1) regression: <c>Post</c> must stamp <see cref="WorkflowStateHistory.EnteredDate"/>
    /// from the injected <see cref="TimeProvider"/>, ignoring whatever the client serialized.
    /// The DTO no longer exposes <c>EnteredDate</c>, so the persisted value must be a fresh,
    /// near-now UTC timestamp rather than <c>default</c>.
    /// </summary>
    [Fact]
    public async Task Post_StampsEnteredDateFromTimeProvider()
    {
        var before = DateTime.UtcNow;

        var result = await _sut.Post(BuildEntryDto(), TestContext.Current.CancellationToken);

        var after = DateTime.UtcNow;

        Assert.IsType<CreatedODataResult<WorkflowStateHistory>>(result);

        using var ctx = new EctDbContext(_dbOptions);
        var saved = await ctx.WorkflowStateHistories.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotEqual(default, saved.EnteredDate);
        Assert.InRange(saved.EnteredDate, before.AddSeconds(-1), after.AddSeconds(1));
        Assert.Null(saved.ExitDate);
    }

    // ─────────────────────────────── Patch ───────────────────────────────────

    /// <summary>
    /// §2.7 (N1) regression: <c>Patch</c> must overwrite any client-supplied
    /// <see cref="WorkflowStateHistory.ExitDate"/> with the current <see cref="TimeProvider"/>
    /// value. A backdated value sent in the delta must be discarded.
    /// </summary>
    [Fact]
    public async Task Patch_OverwritesClientSuppliedExitDateFromTimeProvider()
    {
        // Seed an open history entry.
        WorkflowStateHistory seeded;
        using (var ctx = new EctDbContext(_dbOptions))
        {
            seeded = new WorkflowStateHistory
            {
                LineOfDutyCaseId = 1,
                WorkflowState = WorkflowState.MemberInformationEntry,
                EnteredDate = DateTime.UtcNow.AddHours(-1),
                ExitDate = null,
            };
            ctx.WorkflowStateHistories.Add(seeded);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Build a delta with a deliberately backdated ExitDate that the server must ignore.
        var backdated = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var delta = new Delta<WorkflowStateHistory>();
        delta.TrySetPropertyValue(nameof(WorkflowStateHistory.ExitDate), backdated);

        var before = DateTime.UtcNow;
        var result = await _sut.Patch(seeded.Id, delta, TestContext.Current.CancellationToken);
        var after = DateTime.UtcNow;

        Assert.IsType<UpdatedODataResult<WorkflowStateHistory>>(result);

        using var verifyCtx = new EctDbContext(_dbOptions);
        var updated = await verifyCtx.WorkflowStateHistories.SingleAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(updated.ExitDate);
        Assert.NotEqual(backdated, updated.ExitDate);
        Assert.InRange(updated.ExitDate!.Value, before.AddSeconds(-1), after.AddSeconds(1));
    }

    // ─────────────────────────────── Helpers ─────────────────────────────────

    /// <summary>
    /// Builds a <see cref="CreateWorkflowStateHistoryDto"/> representing an initial
    /// entry into the <see cref="WorkflowState.MemberInformationEntry"/> state.
    /// </summary>
    private static CreateWorkflowStateHistoryDto BuildEntryDto() => new()
    {
        LineOfDutyCaseId = 1,
        WorkflowState    = WorkflowState.MemberInformationEntry,
    };
}
