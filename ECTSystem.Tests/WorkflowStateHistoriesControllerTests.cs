using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class WorkflowStateHistoriesControllerTests : ControllerTestBase
{
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    private readonly DbContextOptions<EctDbContext>        _dbOptions;
    private readonly WorkflowStateHistoriesController    _sut;

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

        _sut = new WorkflowStateHistoriesController(_mockContextFactory.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────────── Post ────────────────────────────────────

    [Fact]
    public async Task Post_WhenModelValid_ReturnsCreatedWithEntry()
    {
        var entry = BuildEntry();

        var result = await _sut.Post(entry, CancellationToken.None);

        var r = Assert.IsType<CreatedODataResult<WorkflowStateHistory>>(result);
        Assert.Equal(entry, r.Value);
    }

    [Fact]
    public async Task Post_WhenModelInvalid_ReturnsBadRequest()
    {
        _sut.ModelState.AddModelError("WorkflowState", "Required");

        var result = await _sut.Post(new WorkflowStateHistory(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

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

    private static WorkflowStateHistory BuildEntry() => new WorkflowStateHistory
    {
        LineOfDutyCaseId = 1,
        WorkflowState    = WorkflowState.MemberInformationEntry,
        Action           = TransitionAction.Entered,
        Status           = WorkflowStepStatus.InProgress,
        OccurredAt       = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    };
}
