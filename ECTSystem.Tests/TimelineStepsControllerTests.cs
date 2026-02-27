using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class TimelineStepsControllerTests : ControllerTestBase
{
    private readonly Mock<IDbContextFactory<EctDbContext>> _mockContextFactory;
    private readonly DbContextOptions<EctDbContext>        _dbOptions;
    private readonly TimelineStepsController          _sut;

    public TimelineStepsControllerTests()
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

        _sut = new TimelineStepsController(_mockContextFactory.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    private void SeedStep(TimelineStep step)
    {
        using var ctx = new EctDbContext(_dbOptions);
        ctx.TimelineSteps.Add(step);
        ctx.SaveChanges();
    }

    // ─────────────────────────────── Sign ────────────────────────────────────

    [Fact]
    public async Task Sign_WhenStepExists_ReturnsOkWithSignedStep()
    {
        var step = new TimelineStep { Id = 1, LineOfDutyCaseId = 1 };
        SeedStep(step);

        var result = await _sut.Sign(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<TimelineStep>(ok.Value);
        Assert.Equal(TestUserId, returned.SignedBy);
        Assert.NotNull(returned.SignedDate);
    }

    [Fact]
    public async Task Sign_WhenStepNotFound_ReturnsNotFound()
    {
        var result = await _sut.Sign(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Sign_PassesCurrentUserIdToService()
    {
        var step = new TimelineStep { Id = 3, LineOfDutyCaseId = 1 };
        SeedStep(step);

        await _sut.Sign(3, CancellationToken.None);

        using var ctx = new EctDbContext(_dbOptions);
        var updated = await ctx.TimelineSteps.FindAsync(3);
        Assert.Equal(TestUserId, updated.SignedBy);
    }

    // ─────────────────────────────── Start ───────────────────────────────────

    [Fact]
    public async Task Start_WhenStepExists_ReturnsOkWithStep()
    {
        var step = new TimelineStep { Id = 2, LineOfDutyCaseId = 1 };
        SeedStep(step);

        var result = await _sut.Start(2, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var returned = Assert.IsType<TimelineStep>(ok.Value);
        Assert.NotNull(returned.StartDate);
    }

    [Fact]
    public async Task Start_WhenStepNotFound_ReturnsNotFound()
    {
        var result = await _sut.Start(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
