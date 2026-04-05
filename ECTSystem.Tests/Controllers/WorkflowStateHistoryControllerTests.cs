using Microsoft.AspNetCore.Mvc;
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

        _sut = new WorkflowStateHistoryController(_mockContextFactory.Object, _mockLog.Object);
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

        var result = await _sut.Post(dto, CancellationToken.None);

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
        var dto = BuildEntryDto();

        await _sut.Post(dto, CancellationToken.None);

        using var ctx = new EctDbContext(_dbOptions);
        var saved = await ctx.WorkflowStateHistories.FirstOrDefaultAsync();
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

        await _sut.Post(new WorkflowStateHistory(), CancellationToken.None);

        using var ctx = new EctDbContext(_dbOptions);
        var count = await ctx.WorkflowStateHistories.CountAsync();
        Assert.Equal(0, count);
    }

    // ─────────────────────────────── Helpers ─────────────────────────────────

    /// <summary>
    /// Builds a <see cref="WorkflowStateHistory"/> representing an initial
    /// entry into the <see cref="WorkflowState.MemberInformationEntry"/> state with
    /// <see cref="WorkflowTransitionAction.Enter"/> and <see cref="WorkflowStepStatus.InProgress"/>.
    /// </summary>
    private static WorkflowStateHistory BuildEntryDto() => new()
    {
        LineOfDutyCaseId = 1,
        WorkflowState    = WorkflowState.MemberInformationEntry,
    };
}
