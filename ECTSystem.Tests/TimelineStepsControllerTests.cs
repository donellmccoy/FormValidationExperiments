using Microsoft.AspNetCore.Mvc;
using Moq;
using ECTSystem.Api.Controllers;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests;

public class TimelineStepsControllerTests : ControllerTestBase
{
    private readonly Mock<ILineOfDutyTimelineService> _mockTimelineService;
    private readonly TimelineStepsController          _sut;

    public TimelineStepsControllerTests()
    {
        _mockTimelineService = new Mock<ILineOfDutyTimelineService>();
        _sut = new TimelineStepsController(_mockTimelineService.Object);
        _sut.ControllerContext = CreateControllerContext();
    }

    // ─────────────────────────────── Sign ────────────────────────────────────

    [Fact]
    public async Task Sign_WhenStepExists_ReturnsOkWithSignedStep()
    {
        var step = new TimelineStep { Id = 1, LineOfDutyCaseId = 1, SignedBy = TestUserId, SignedDate = DateTime.UtcNow };
        _mockTimelineService.Setup(s => s.SignTimelineStepAsync(1, TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(step);

        var result = await _sut.Sign(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(step, ok.Value);
    }

    [Fact]
    public async Task Sign_WhenStepNotFound_ReturnsNotFound()
    {
        _mockTimelineService.Setup(s => s.SignTimelineStepAsync(999, TestUserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TimelineStep with Id 999 not found."));

        var result = await _sut.Sign(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Sign_PassesCurrentUserIdToService()
    {
        var step = new TimelineStep { Id = 3, LineOfDutyCaseId = 1, SignedBy = TestUserId };
        _mockTimelineService.Setup(s => s.SignTimelineStepAsync(3, TestUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(step);

        await _sut.Sign(3, CancellationToken.None);

        _mockTimelineService.Verify(s => s.SignTimelineStepAsync(3, TestUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─────────────────────────────── Start ───────────────────────────────────

    [Fact]
    public async Task Start_WhenStepExists_ReturnsOkWithStep()
    {
        var step = new TimelineStep { Id = 2, LineOfDutyCaseId = 1, StartDate = DateTime.UtcNow };
        _mockTimelineService.Setup(s => s.StartTimelineStepAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(step);

        var result = await _sut.Start(2, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(step, ok.Value);
    }

    [Fact]
    public async Task Start_WhenStepNotFound_ReturnsNotFound()
    {
        _mockTimelineService.Setup(s => s.StartTimelineStepAsync(999, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TimelineStep with Id 999 not found."));

        var result = await _sut.Start(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
